namespace Mra.Domain.Requests;

// The single source of truth for which status changes are legal (FR-3, architecture §4.1).
// Creation (none -> Raised) is not a transition; it happens only in MaintenanceRequest.Raise.
// Rejected and Completed have no outgoing transitions, so they are terminal (FR-3.2).
internal static class RequestTransitions
{
    private static readonly HashSet<(RequestStatus From, RequestStatus To)> Allowed =
    [
        (RequestStatus.Raised, RequestStatus.Approved),
        (RequestStatus.Raised, RequestStatus.PendingApproval),
        (RequestStatus.PendingApproval, RequestStatus.Approved),
        (RequestStatus.PendingApproval, RequestStatus.Rejected),
        (RequestStatus.Approved, RequestStatus.Completed),
    ];

    public static void EnsureAllowed(RequestStatus from, RequestStatus to)
    {
        if (!Allowed.Contains((from, to)))
        {
            throw new InvalidTransitionException(from, to);
        }
    }
}
