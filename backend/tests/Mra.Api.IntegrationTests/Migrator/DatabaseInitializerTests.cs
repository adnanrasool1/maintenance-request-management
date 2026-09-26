using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Api.IntegrationTests.Persistence;
using Mra.DbMigrator;
using Mra.Domain.Organisations;
using Mra.Domain.Requests;
using Mra.Domain.Sites;
using Mra.Domain.Users;
using Mra.Infrastructure.Persistence;
using Xunit;

namespace Mra.Api.IntegrationTests.Migrator;

// T2.8 / T9.5: the migrator is idempotent, and mra_app cannot rewrite the audit trail (architecture §8).
// Uses its own database on the shared container, so the migrator creates it from scratch.
// Tests in one class run sequentially, so they never reset the mra_app password concurrently.
public sealed class DatabaseInitializerTests(SqlServerFixture fixture)
{
    private const int PermissionDenied = 229;
    private const int CreateTablePermissionDenied = 262;

    // Meets SQL Server's password policy; no quotes.
    private const string AppPassword = "Mra-app-Test-9x!Pw";

    private const string AdminEmail = "system.admin@migrator.test";
    private const string AdminPassword = "Admin-Test-7y!Pw";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string OwnerConnectionString => new SqlConnectionStringBuilder(fixture.ConnectionString)
    {
        InitialCatalog = "mra_migrator_tests",
    }.ConnectionString;

    private string AppConnectionString => new SqlConnectionStringBuilder(OwnerConnectionString)
    {
        UserID = DatabaseInitializer.AppLogin,
        Password = AppPassword,
    }.ConnectionString;

    private MigratorSettings Settings => new(OwnerConnectionString, AppPassword, AdminEmail, AdminPassword);

    [Fact]
    public async Task Running_twice_succeeds_and_seeds_the_System_Admin_exactly_once()
    {
        var firstLog = new StringWriter();
        var secondLog = new StringWriter();

        await DatabaseInitializer.RunAsync(Settings, firstLog, Ct);
        await DatabaseInitializer.RunAsync(Settings, secondLog, Ct);

        Assert.Contains("database is up to date", secondLog.ToString());
        Assert.Contains("already existed; password updated", secondLog.ToString());
        Assert.Contains("already exists; nothing seeded", secondLog.ToString());

        // Secrets never reach the log.
        foreach (var log in new[] { firstLog.ToString(), secondLog.ToString() })
        {
            Assert.DoesNotContain(AppPassword, log);
            Assert.DoesNotContain(AdminPassword, log);
        }

        var admins = await ScalarAsync<int>(
            OwnerConnectionString,
            "SELECT COUNT(*) FROM dbo.Users WHERE Email = @email AND Role = @role AND OrganisationId IS NULL",
            new SqlParameter("@email", AdminEmail),
            new SqlParameter("@role", (byte)Role.SystemAdmin));
        Assert.Equal(1, admins);
    }

    [Fact]
    public async Task Mra_app_can_insert_and_read_audit_entries_but_cannot_update_or_delete_them()
    {
        await DatabaseInitializer.RunAsync(Settings, TextWriter.Null, Ct);
        var (organisationId, requestId) = await SeedRequestAsync();

        await using var connection = new SqlConnection(AppConnectionString);
        await connection.OpenAsync(Ct);

        // INSERT and SELECT are granted.
        await ExecuteAsync(
            connection,
            """
            INSERT INTO dbo.AuditEntries (OrganisationId, RequestId, ActorUserId, Action, FromStatus, ToStatus, Comment, OccurredAt)
            VALUES (@org, @request, NULL, N'Raised', NULL, 1, NULL, SYSUTCDATETIME())
            """,
            new SqlParameter("@org", organisationId),
            new SqlParameter("@request", requestId));

        var countBefore = await CountAuditEntriesAsync(connection, requestId);
        Assert.Equal(3, countBefore); // Raised + AutoApproved from the aggregate, plus the insert above.

        // UPDATE and DELETE are denied, whatever the WHERE clause.
        var update = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(
            connection,
            "UPDATE dbo.AuditEntries SET Comment = N'tampered' WHERE RequestId = @request",
            new SqlParameter("@request", requestId)));
        Assert.Equal(PermissionDenied, update.Number);

        var delete = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(
            connection,
            "DELETE FROM dbo.AuditEntries WHERE RequestId = @request",
            new SqlParameter("@request", requestId)));
        Assert.Equal(PermissionDenied, delete.Number);

        Assert.Equal(countBefore, await CountAuditEntriesAsync(connection, requestId));
        var tampered = await ScalarAsync<int>(
            OwnerConnectionString,
            "SELECT COUNT(*) FROM dbo.AuditEntries WHERE RequestId = @request AND Comment = N'tampered'",
            new SqlParameter("@request", requestId));
        Assert.Equal(0, tampered);
    }

    [Fact]
    public async Task Mra_app_can_change_other_tables_but_has_no_DDL_rights_or_migration_history_access()
    {
        await DatabaseInitializer.RunAsync(Settings, TextWriter.Null, Ct);
        var (_, requestId) = await SeedRequestAsync();

        await using var connection = new SqlConnection(AppConnectionString);
        await connection.OpenAsync(Ct);

        // The API needs UPDATE on the other tables (request transitions, thresholds).
        await ExecuteAsync(
            connection,
            "UPDATE dbo.MaintenanceRequests SET Description = Description WHERE Id = @request",
            new SqlParameter("@request", requestId));

        var createTable = await Assert.ThrowsAsync<SqlException>(
            () => ExecuteAsync(connection, "CREATE TABLE dbo.Intruder (Id int)"));
        Assert.Equal(CreateTablePermissionDenied, createTable.Number);

        var history = await Assert.ThrowsAsync<SqlException>(
            () => ExecuteAsync(connection, "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory"));
        Assert.Equal(PermissionDenied, history.Number);

        var isOwner = await ScalarAsync<int>(AppConnectionString, "SELECT IS_ROLEMEMBER('db_owner')");
        Assert.Equal(0, isOwner);
    }

    // A tenant with one auto-approved request (two audit entries), written with the owner login.
    private async Task<(Guid OrganisationId, Guid RequestId)> SeedRequestAsync()
    {
        var organisation = Organisation.Create($"Org {Guid.NewGuid():N}", 500m);
        var site = Site.Create(organisation.Id, "Head office");
        var user = User.Create(organisation.Id, SqlServerFixture.UniqueEmail(), "hash", Role.Requester);
        var request = MaintenanceRequest.Raise(
            organisation.Id,
            site.Id,
            user.Id,
            "Leaking tap",
            estimatedCost: 100m,
            currentThreshold: 500m,
            now: new DateTime(2026, 9, 26, 9, 30, 0, DateTimeKind.Utc));

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(OwnerConnectionString).Options;
        await using var db = new AppDbContext(options, new NoOrganisation());
        db.AddRange(organisation, site, user, request);
        await db.SaveChangesAsync(Ct);

        return (organisation.Id, request.Id);
    }

    private static async Task<int> CountAuditEntriesAsync(SqlConnection connection, Guid requestId)
    {
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.AuditEntries WHERE RequestId = @request", connection);
        command.Parameters.Add(new SqlParameter("@request", requestId));
        return (int)(await command.ExecuteScalarAsync(Ct))!;
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, params SqlParameter[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return (T)(await command.ExecuteScalarAsync(Ct))!;
    }

    private sealed class NoOrganisation : ICurrentUser
    {
        public Guid? UserId => null;

        public Guid? OrganisationId => null;

        public string? Role => null;

        public bool IsAuthenticated => false;
    }
}
