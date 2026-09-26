namespace Mra.Domain.Requests;

// Aggregate root for the workflow (architecture §4.1). Status changes only through the methods
// below; each one checks RequestTransitions and appends exactly one AuditEntry. All times passed
// in are expected to be UTC (from TimeProvider).
public sealed class MaintenanceRequest
{
    public const int MaxDescriptionLength = 2000;

    public const int MaxCommentLength = 2000;

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

    /// <summary>
    /// Creates a request in <see cref="RequestStatus.Raised"/> and routes it in the same call
    /// (FR-3.3, FR-4.1): below <paramref name="currentThreshold"/> it is auto-approved and the
    /// threshold is snapshotted; at or above it (A-2) it goes to
    /// <see cref="RequestStatus.PendingApproval"/>. Two audit entries are appended: the raise
    /// (actor = raiser) and the routing (actor = null, the system).
    /// </summary>
    public static MaintenanceRequest Raise(
        Guid organisationId,
        Guid siteId,
        Guid raisedByUserId,
        string description,
        decimal estimatedCost,
        decimal currentThreshold,
        DateTime now)
    {
        EnsureNotEmpty(organisationId, nameof(organisationId));
        EnsureNotEmpty(siteId, nameof(siteId));
        EnsureNotEmpty(raisedByUserId, nameof(raisedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (description.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"The description must be at most {MaxDescriptionLength} characters.", nameof(description));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(estimatedCost);
        ArgumentOutOfRangeException.ThrowIfNegative(currentThreshold);

        var request = new MaintenanceRequest
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            SiteId = siteId,
            RaisedByUserId = raisedByUserId,
            Description = description,
            EstimatedCost = estimatedCost,
            Status = RequestStatus.Raised,
            CreatedAt = now,
        };
        request.Audit(raisedByUserId, AuditActions.Raised, null, RequestStatus.Raised, null, now);

        if (estimatedCost < currentThreshold)
        {
            request.TransitionTo(RequestStatus.Approved, null, AuditActions.AutoApproved, null, now);
            request.ThresholdAtDecision = currentThreshold;
        }
        else
        {
            request.TransitionTo(RequestStatus.PendingApproval, null, AuditActions.RoutedForApproval, null, now);
        }

        return request;
    }

    // The only place Status changes after creation: the table decides legality, then the
    // change and its audit entry are applied together.
    private void TransitionTo(RequestStatus to, Guid? actorUserId, string action, string? comment, DateTime now)
    {
        RequestTransitions.EnsureAllowed(Status, to);
        var from = Status;
        Status = to;
        Audit(actorUserId, action, from, to, comment, now);
    }

    private void Audit(Guid? actorUserId, string action, RequestStatus? from, RequestStatus to, string? comment, DateTime now) =>
        _auditEntries.Add(AuditEntry.Record(OrganisationId, Id, actorUserId, action, from, to, comment, now));

    private static void EnsureNotEmpty(Guid id, string paramName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A non-empty ID is required.", paramName);
        }
    }
}
