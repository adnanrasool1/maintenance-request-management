using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mra.Domain.Organisations;
using Mra.Domain.Users;

namespace Mra.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    // API contract D-2 turns a violation of this index into a 400, so the name is part of the contract.
    public const string EmailIndexName = "UX_Users_Email";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] BETWEEN 1 AND 4"));

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();

        // Null only for the System Admin.
        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(u => u.OrganisationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName(EmailIndexName);
    }
}
