namespace Mra.Domain.Requests;

// The actor tried to approve or reject a request they raised (FR-3, A-3). Mapped to 403.
public sealed class SelfApprovalException : DomainException
{
    public SelfApprovalException()
        : base("You cannot approve or reject a request you raised.")
    {
    }
}
