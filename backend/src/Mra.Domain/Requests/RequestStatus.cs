namespace Mra.Domain.Requests;

// Byte-backed so it maps to tinyint (architecture §6).
public enum RequestStatus : byte
{
    Raised = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Completed = 5,
}
