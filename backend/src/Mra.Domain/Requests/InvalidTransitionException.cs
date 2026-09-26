namespace Mra.Domain.Requests;

// The requested status change is not in the transition table (FR-3.1). Mapped to 409.
public sealed class InvalidTransitionException : DomainException
{
    public InvalidTransitionException(RequestStatus from, RequestStatus to)
        : base($"A request cannot move from {from} to {to}.")
    {
        From = from;
        To = to;
    }

    public RequestStatus From { get; }

    public RequestStatus To { get; }
}
