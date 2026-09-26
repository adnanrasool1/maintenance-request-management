using Mra.Application.Common.Abstractions;
using Mra.Domain.Requests;
using Mra.Domain.Users;

namespace Mra.Application.Requests;

// Who can see a request inside the caller's organisation (FR-5.1, FR-5.2, contract §3.7, §3.8,
// §3.11). The tenant query filter already limits the set to the caller's organisation; on top of
// that a Requester sees only the requests they raised, and an Approver sees all of them. A
// request that isn't visible is treated as not found (404), never as forbidden.
internal static class RequestVisibility
{
    public static bool IsApprover(this ICurrentUser currentUser) =>
        currentUser.Role == nameof(Role.Approver);

    public static IQueryable<MaintenanceRequest> VisibleTo(this IQueryable<MaintenanceRequest> requests, ICurrentUser currentUser)
    {
        if (currentUser.IsApprover())
        {
            return requests;
        }

        var userId = currentUser.RequireUserId();
        return requests.Where(r => r.RaisedByUserId == userId);
    }

    // The endpoint policies guarantee an authenticated caller with a subject claim.
    public static Guid RequireUserId(this ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new InvalidOperationException("The caller has no user ID.");
}
