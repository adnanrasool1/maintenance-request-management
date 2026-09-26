using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Domain.Organisations;
using Mra.Domain.Requests;
using Mra.Domain.Sites;
using Mra.Domain.Users;

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
