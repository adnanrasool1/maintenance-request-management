namespace Mra.Domain.Requests;

// Append-only record of one transition (architecture §8): no public setters, no mutating methods.
// Created only by MaintenanceRequest; the creation path is added in T1.6.
public sealed class AuditEntry
{
    private AuditEntry()
    {
        Action = null!;
    }

    public long Id { get; private set; }

    public Guid OrganisationId { get; private set; }

    public Guid RequestId { get; private set; }

    // Null when the system performed the transition (for example, automatic approval).
    public Guid? ActorUserId { get; private set; }

    public string Action { get; private set; }

    public RequestStatus? FromStatus { get; private set; }

    public RequestStatus ToStatus { get; private set; }

    public string? Comment { get; private set; }

    public DateTime OccurredAt { get; private set; }
}
