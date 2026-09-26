using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Domain.Organisations;
using Mra.Domain.Sites;
using Mra.Domain.Users;
using Mra.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Xunit;

[assembly: AssemblyFixture(typeof(Mra.Api.IntegrationTests.Persistence.SqlServerFixture))]

namespace Mra.Api.IntegrationTests.Persistence;

// One SQL Server container for the whole test assembly. Tests create their own organisations
// with unique names and emails, so they can share the database and run in parallel.
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Same pinned tag as infra/docker-compose.yml, so the image pull is shared.
    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "mra_tests",
        }.ConnectionString;

        // Tests apply the real migration; the API never does (Mra.DbMigrator owns that).
        await using var db = CreateContext(organisationId: null);
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    // A context as seen by a caller in the given organisation; null is the System Admin.
    public AppDbContext CreateContext(Guid? organisationId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppDbContext(options, new FakeCurrentUser(organisationId));
    }

    public async Task<Tenant> SeedTenantAsync()
    {
        var organisation = Organisation.Create($"Org {Guid.NewGuid():N}", 500m);
        var site = Site.Create(organisation.Id, "Head office");
        var user = User.Create(organisation.Id, UniqueEmail(), "hash", Role.Requester);

        // Created the way the System Admin creates an organisation: with no caller organisation.
        await using var db = CreateContext(organisationId: null);
        db.AddRange(organisation, site, user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new Tenant(organisation.Id, site.Id, user.Id);
    }

    // The domain has no public way to create a request or audit entry yet (lane A, T1.2-T1.6),
    // so these rows are inserted with parameterised SQL. Replace with MaintenanceRequest.Raise
    // once it exists.
    public async Task<Guid> InsertRequestWithAuditAsync(
        Tenant tenant,
        DateTime? completedAt = null,
        Guid? siteId = null)
    {
        var requestId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 9, 26, 9, 30, 0);

        await using var db = CreateContext(organisationId: null);
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO MaintenanceRequests
                (Id, OrganisationId, SiteId, RaisedByUserId, Description, EstimatedCost, Status, ExceededThreshold, CreatedAt, CompletedAt)
            VALUES
                ({requestId}, {tenant.OrganisationId}, {siteId ?? tenant.SiteId}, {tenant.UserId}, {"Leaking tap"}, {100m}, {(byte)1}, {false}, {createdAt}, {completedAt});

            INSERT INTO AuditEntries (OrganisationId, RequestId, ActorUserId, Action, FromStatus, ToStatus, Comment, OccurredAt)
            VALUES ({tenant.OrganisationId}, {requestId}, {tenant.UserId}, {"Raised"}, {null}, {(byte)1}, {null}, {createdAt});
            """,
            TestContext.Current.CancellationToken);

        return requestId;
    }

    public static string UniqueEmail() => $"{Guid.NewGuid():N}@test.local";

    private sealed class FakeCurrentUser(Guid? organisationId) : ICurrentUser
    {
        public Guid? UserId => null;

        public Guid? OrganisationId => organisationId;

        public string? Role => null;

        public bool IsAuthenticated => true;
    }
}

public sealed record Tenant(Guid OrganisationId, Guid SiteId, Guid UserId);
