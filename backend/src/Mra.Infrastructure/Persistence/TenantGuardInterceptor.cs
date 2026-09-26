using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mra.Domain.Organisations;
using Mra.Domain.Requests;

namespace Mra.Infrastructure.Persistence;

// Write-side tenant isolation and audit immutability (architecture §7 layer 5, §8).
internal sealed class TenantGuardInterceptor : SaveChangesInterceptor
{
    public static readonly TenantGuardInterceptor Instance = new();

    private const string OrganisationIdProperty = "OrganisationId";

    private TenantGuardInterceptor()
    {
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Guard(DbContext? context)
    {
        if (context is not AppDbContext db)
        {
            throw new TenantGuardException("The tenant guard only supports AppDbContext.");
        }

        // SavingChanges runs before EF's own DetectChanges, so detect here to see every change.
        db.ChangeTracker.DetectChanges();

        var callerOrganisationId = db.CurrentOrganisationId;

        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            // Applies to every caller, System Admin and migrator included.
            if (entry.Entity is AuditEntry && entry.State is not EntityState.Added)
            {
                throw new TenantGuardException("Audit entries are append-only; they cannot be modified or deleted.");
            }

            if (entry.Entity is Organisation)
            {
                GuardOrganisation(entry, callerOrganisationId);
            }
            else if (entry.Metadata.FindProperty(OrganisationIdProperty) is not null)
            {
                GuardTenantOwned(entry, callerOrganisationId);
            }
        }
    }

    private static void GuardTenantOwned(EntityEntry entry, Guid? callerOrganisationId)
    {
        var organisationId = entry.Property(OrganisationIdProperty);

        if (entry.State == EntityState.Added)
        {
            if (organisationId.CurrentValue is Guid value && value == Guid.Empty)
            {
                if (callerOrganisationId is null)
                {
                    throw Violation(entry, "has no organisation and the caller has none to stamp");
                }

                organisationId.CurrentValue = callerOrganisationId.Value;
            }
        }
        else if (organisationId.IsModified && !Equals(organisationId.OriginalValue, organisationId.CurrentValue))
        {
            // Moving a row between organisations is never legitimate, whoever the caller is.
            throw Violation(entry, "cannot change its organisation");
        }

        // Exemption: a caller with no organisation is the System Admin (creating an organisation
        // together with its first Tenant Admin) or the migrator (seeding the System Admin). Neither
        // has a tenant to compare against, and the query filters already hide all tenant rows from
        // them. This is the only case where the cross-tenant check is skipped.
        if (callerOrganisationId is null)
        {
            return;
        }

        var owner = entry.State == EntityState.Added ? organisationId.CurrentValue : organisationId.OriginalValue;
        if (owner is not Guid ownerId || ownerId != callerOrganisationId.Value)
        {
            throw Violation(entry, "belongs to another organisation");
        }
    }

    private static void GuardOrganisation(EntityEntry entry, Guid? callerOrganisationId)
    {
        // Same exemption as above: only a caller with no organisation (the System Admin) creates
        // organisations. A tenant caller may only change its own organisation (the threshold).
        if (callerOrganisationId is null)
        {
            return;
        }

        var id = entry.Property(nameof(Organisation.Id)).CurrentValue;
        if (entry.State == EntityState.Added || id is not Guid organisationId || organisationId != callerOrganisationId.Value)
        {
            throw Violation(entry, "is not the caller's organisation");
        }
    }

    private static TenantGuardException Violation(EntityEntry entry, string reason) =>
        new($"Tenant guard: {entry.Metadata.ClrType.Name} ({entry.State}) {reason}.");
}
