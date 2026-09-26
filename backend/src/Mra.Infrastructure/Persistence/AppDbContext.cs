using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;
using Mra.Domain.Organisations;
using Mra.Domain.Requests;
using Mra.Domain.Sites;
using Mra.Domain.Users;
using Mra.Infrastructure.Persistence.Configurations;

namespace Mra.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : DbContext(options), IAppDbContext
{
    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<Site> Sites => Set<Site>();

    public DbSet<User> Users => Set<User>();

    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // The caller's organisation, from the JWT only (architecture §7). Null for the System Admin
    // and for callers with no user (the migrator).
    public Guid? CurrentOrganisationId => currentUser.OrganisationId;

    // Translates unique-index violations that handlers must report as 400 (contract D-2, §3.3).
    // Every other DbUpdateException (including DbUpdateConcurrencyException) propagates unchanged.
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueIndexViolation(exception, UserConfiguration.EmailIndexName))
        {
            throw new DuplicateEmailException(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueIndexViolation(exception, SiteNameIndexName))
        {
            throw new DuplicateSiteNameException(exception);
        }
    }

    // EF's default name for the unique index on Sites (OrganisationId, Name) (SiteConfiguration).
    private const string SiteNameIndexName = "IX_Sites_OrganisationId_Name";

    // 2601: duplicate key in a unique index; 2627: unique constraint violation. SQL Server names
    // the index only in the message, e.g. "... with unique index 'UX_Users_Email'. ...".
    private static bool IsUniqueIndexViolation(DbUpdateException exception, string indexName) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sql
        && sql.Message.Contains($"'{indexName}'", StringComparison.Ordinal);

    // Registered here rather than by the caller, so every AppDbContext is guarded (architecture §7, §8).
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(TenantGuardInterceptor.Instance);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every decimal in the model is money (architecture §6).
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // D-7: datetime2 reads back as Unspecified; mark every timestamp as UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyTenantFilters(modelBuilder);
    }

    // Global query filters (architecture §7). They reference CurrentOrganisationId on the context,
    // so EF evaluates it per query rather than once at model build.
    // "CurrentOrganisationId != null" comes first so a caller without an organisation sees no
    // tenant rows: without it, the Users filter would match "null == null" (C# semantics)
    // and return the System Admin row.
    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organisation>().HasQueryFilter(
            o => CurrentOrganisationId != null && o.Id == CurrentOrganisationId);

        modelBuilder.Entity<Site>().HasQueryFilter(
            s => CurrentOrganisationId != null && s.OrganisationId == CurrentOrganisationId);

        modelBuilder.Entity<User>().HasQueryFilter(
            u => CurrentOrganisationId != null && u.OrganisationId == CurrentOrganisationId);

        modelBuilder.Entity<MaintenanceRequest>().HasQueryFilter(
            r => CurrentOrganisationId != null && r.OrganisationId == CurrentOrganisationId);

        modelBuilder.Entity<AuditEntry>().HasQueryFilter(
            a => CurrentOrganisationId != null && a.OrganisationId == CurrentOrganisationId);
    }
}
