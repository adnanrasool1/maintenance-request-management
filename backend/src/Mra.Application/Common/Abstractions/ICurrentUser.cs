namespace Mra.Application.Common.Abstractions;

/// <summary>
/// The authenticated caller, read only from the validated JWT claims (architecture §7, layer 1).
/// Never populated from the route, query string or request body.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The <c>sub</c> claim, or null when the caller is not authenticated.</summary>
    Guid? UserId { get; }

    /// <summary>The <c>org</c> claim. Null for the System Admin and for anonymous callers.</summary>
    Guid? OrganisationId { get; }

    /// <summary>
    /// The <c>role</c> claim: SystemAdmin, TenantAdmin, Requester or Approver.
    /// A string until the Domain <c>Role</c> enum lands (T1.1); it can be tightened then.
    /// </summary>
    string? Role { get; }

    bool IsAuthenticated { get; }
}
