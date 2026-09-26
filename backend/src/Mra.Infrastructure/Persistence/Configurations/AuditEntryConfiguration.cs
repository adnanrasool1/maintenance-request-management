using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mra.Domain.Requests;

namespace Mra.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries", t =>
        {
            t.HasCheckConstraint("CK_AuditEntries_FromStatus", "[FromStatus] IS NULL OR [FromStatus] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_AuditEntries_ToStatus", "[ToStatus] BETWEEN 1 AND 5");
        });

        // bigint identity, so entries are strictly ordered by insertion (architecture §6).
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityColumn();

        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Comment).HasMaxLength(2000);

        // The RequestId FK is configured on MaintenanceRequest; this index also serves it.
        builder.HasIndex(a => new { a.RequestId, a.Id });
    }
}
