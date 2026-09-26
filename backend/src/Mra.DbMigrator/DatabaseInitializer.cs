using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;
using Mra.Domain.Users;
using Mra.Infrastructure.Authentication;
using Mra.Infrastructure.Persistence;

namespace Mra.DbMigrator;

// The one-shot database set-up (architecture §4.5). Every step is idempotent, so the migrator can
// run on every `docker compose up`. It connects with the owner login; the API never does.
public static class DatabaseInitializer
{
    public const string AppLogin = "mra_app";

    // The identifiers are the fixed constants above, never input, so they are written bracketed.
    // DENY overrides the schema-wide GRANT (architecture §8). mra_app gets no DDL rights, no role
    // membership and no EXECUTE; the migrations history is hidden from it entirely.
    private const string UserAndPermissionsSql = """
        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'mra_app')
            CREATE USER [mra_app] FOR LOGIN [mra_app];
        ELSE
            ALTER USER [mra_app] WITH LOGIN = [mra_app];

        GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[dbo] TO [mra_app];
        DENY SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[__EFMigrationsHistory] TO [mra_app];
        DENY UPDATE, DELETE ON OBJECT::[dbo].[AuditEntries] TO [mra_app];
        """;

    public static async Task RunAsync(MigratorSettings settings, TextWriter log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(log);

        await ApplyMigrationsAsync(settings, log, cancellationToken);
        await EnsureAppLoginAsync(settings, log, cancellationToken);
        await SeedSystemAdminAsync(settings, log, cancellationToken);
    }

    private static async Task ApplyMigrationsAsync(MigratorSettings settings, TextWriter log, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(settings);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        // Creates the database if it does not exist yet. The only Migrate call in the codebase.
        await db.Database.MigrateAsync(cancellationToken);

        await log.WriteLineAsync(pending.Count == 0
            ? "Migrations: database is up to date."
            : $"Migrations: applied {pending.Count} ({string.Join(", ", pending)}).");
    }

    private static async Task EnsureAppLoginAsync(MigratorSettings settings, TextWriter log, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(settings);

        var loginExists = await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.server_principals WHERE name = {AppLogin}")
            .SingleAsync(cancellationToken) > 0;

        // CREATE/ALTER LOGIN cannot take parameters, so the statement is built on the server from
        // parameters: QUOTENAME brackets the name and quotes the password, doubling any quote.
        // The password is never part of the command text sent from here, and never logged.
        var password = settings.AppLoginPassword;
        if (loginExists)
        {
            // Resets the password so it always follows infra/.env.
            await db.Database.ExecuteSqlAsync(
                $"""
                DECLARE @sql nvarchar(max) = N'ALTER LOGIN ' + QUOTENAME({AppLogin}) + N' WITH PASSWORD = ' + QUOTENAME({password}, N'''');
                EXEC sys.sp_executesql @sql;
                """,
                cancellationToken);
        }
        else
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                DECLARE @sql nvarchar(max) = N'CREATE LOGIN ' + QUOTENAME({AppLogin}) + N' WITH PASSWORD = ' + QUOTENAME({password}, N'''') + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF';
                EXEC sys.sp_executesql @sql;
                """,
                cancellationToken);
        }

        await log.WriteLineAsync(loginExists
            ? $"Login {AppLogin}: already existed; password updated."
            : $"Login {AppLogin}: created.");

        await db.Database.ExecuteSqlRawAsync(UserAndPermissionsSql, cancellationToken);

        await log.WriteLineAsync(
            $"User {AppLogin}: granted SELECT, INSERT, UPDATE, DELETE on schema dbo; denied UPDATE, DELETE on AuditEntries and all access to __EFMigrationsHistory.");
    }

    private static async Task SeedSystemAdminAsync(MigratorSettings settings, TextWriter log, CancellationToken cancellationToken)
    {
        var hash = new PasswordHasherAdapter().Hash(settings.SystemAdminPassword);
        var admin = User.CreateSystemAdmin(settings.SystemAdminEmail, hash);

        // The query filters hide every user from a caller with no organisation, and
        // IgnoreQueryFilters is not allowed here, so "already seeded" is detected by the unique
        // email index instead of a lookup; AppDbContext translates that violation into
        // DuplicateEmailException. The write guard allows the insert: the migrator has no
        // organisation, and the System Admin has none.
        await using var db = CreateContext(settings);
        db.Users.Add(admin);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await log.WriteLineAsync($"System Admin: seeded {settings.SystemAdminEmail}.");
        }
        catch (DuplicateEmailException)
        {
            await log.WriteLineAsync($"System Admin: a user with email {settings.SystemAdminEmail} already exists; nothing seeded.");
        }
    }

    private static AppDbContext CreateContext(MigratorSettings settings)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(settings.OwnerConnectionString)
            .Options;

        return new AppDbContext(options, NoCurrentUser.Instance);
    }

    // The migrator acts for no user and no organisation.
    private sealed class NoCurrentUser : ICurrentUser
    {
        public static readonly NoCurrentUser Instance = new();

        public Guid? UserId => null;

        public Guid? OrganisationId => null;

        public string? Role => null;

        public bool IsAuthenticated => false;
    }
}
