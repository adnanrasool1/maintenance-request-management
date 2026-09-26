namespace Mra.Domain.Requests;

// Aggregate root for the workflow. Raise/Approve/Reject/Complete, the transition table
// and audit creation are added in T1.2-T1.6.
public sealed class MaintenanceRequest
{
    private readonly List<AuditEntry> _auditEntries = [];

    private MaintenanceRequest()
    {
        Description = null!;
    }

    public Guid Id { get; private set; }

    public Guid OrganisationId { get; private set; }

    public Guid SiteId { get; private set; }

    public Guid RaisedByUserId { get; private set; }

    public string Description { get; private set; }

    public decimal EstimatedCost { get; private set; }

    public decimal? ActualCost { get; private set; }

    public RequestStatus Status { get; private set; }

    public decimal? ThresholdAtDecision { get; private set; }

    public bool ExceededThreshold { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<AuditEntry> AuditEntries => _auditEntries.AsReadOnly();
}
