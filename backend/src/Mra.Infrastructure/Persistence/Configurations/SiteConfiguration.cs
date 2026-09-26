using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mra.Domain.Organisations;
using Mra.Domain.Sites;

namespace Mra.Infrastructure.Persistence.Configurations;

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("Sites");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        // Target of the composite FK from MaintenanceRequests, so the database rejects
        // a request whose site belongs to another organisation (architecture §6).
        builder.HasAlternateKey(s => new { s.Id, s.OrganisationId });

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(s => s.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.OrganisationId, s.Name }).IsUnique();
    }
}
