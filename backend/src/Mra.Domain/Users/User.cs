namespace Mra.Domain.Users;

public sealed class User
{
    private User()
    {
        Email = null!;
        PasswordHash = null!;
    }

    public Guid Id { get; private set; }

    // Null only for the System Admin, who belongs to no organisation (PRD §5).
    public Guid? OrganisationId { get; private set; }

    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public Role Role { get; private set; }

    public static User Create(Guid organisationId, string email, string passwordHash, Role role)
    {
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException("An organisation ID is required.", nameof(organisationId));
        }

        if (role is not (Role.TenantAdmin or Role.Requester or Role.Approver))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "A tenant user must be a Tenant Admin, Requester or Approver.");
        }

        return New(organisationId, email, passwordHash, role);
    }

    public static User CreateSystemAdmin(string email, string passwordHash) =>
        New(null, email, passwordHash, Role.SystemAdmin);

    private static User New(Guid? organisationId, string email, string passwordHash, Role role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
        };
    }
}
