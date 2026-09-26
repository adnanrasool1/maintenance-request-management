namespace Mra.Application.Common.Abstractions;

// Identity of the caller, read only from the validated JWT claims (architecture §7).
public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? OrganisationId { get; }

    string? Role { get; }

    bool IsAuthenticated { get; }
}
