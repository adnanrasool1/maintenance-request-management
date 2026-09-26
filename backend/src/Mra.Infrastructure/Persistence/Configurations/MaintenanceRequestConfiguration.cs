using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mra.Domain.Organisations;
using Mra.Domain.Requests;
using Mra.Domain.Sites;
using Mra.Domain.Users;

namespace Mra.Infrastructure.Persistence.Configurations;

internal sealed class MaintenanceRequestConfiguration : IEntityTypeConfiguration<MaintenanceRequest>
{
    public void Configure(EntityTypeBuilder<MaintenanceRequest> builder)
    {
        builder.ToTable("MaintenanceRequests", t => t.HasCheckConstraint("CK_MaintenanceRequests_Status", "[Status] BETWEEN 1 AND 5"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Description).HasMaxLength(2000).IsRequired();

        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(r => r.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite FK: the site must belong to the request's organisation (architecture §6).
        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(r => new { r.SiteId, r.OrganisationId })
            .HasPrincipalKey(s => new { s.Id, s.OrganisationId })
            .OnDelete(DeleteBehavior.Restrict);

        // Plain FK: the raiser always comes from the JWT (DECISIONS §2).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.RaisedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Audit rows are never deleted, so nothing cascades onto them (architecture §8).
        builder.HasMany(r => r.AuditEntries)
            .WithOne()
            .HasForeignKey(a => a.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(r => r.AuditEntries)
            .HasField("_auditEntries")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes from architecture §6.
        builder.HasIndex(r => new { r.OrganisationId, r.Status, r.CreatedAt })
            .IsDescending(false, false, true);

        builder.HasIndex(r => new { r.OrganisationId, r.RaisedByUserId, r.CreatedAt })
            .IsDescending(false, false, true);

        builder.HasIndex(r => new { r.OrganisationId, r.Status, r.CompletedAt })
            .IncludeProperties(r => new { r.SiteId, r.ActualCost });
    }
}
