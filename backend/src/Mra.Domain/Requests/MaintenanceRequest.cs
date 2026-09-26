using System.Globalization;

namespace Mra.Domain.Requests;

// Aggregate root for the workflow (architecture §4.1). Status changes only through the methods
// below. Every transition is checked against RequestTransitions and appends exactly one
// AuditEntry; Raise also records the creation itself, so it appends two. All times passed in are
// expected to be UTC (from TimeProvider).
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

    /// <summary>
    /// Manually approves a pending request (FR-3, FR-3.4) and snapshots
    /// <paramref name="currentThreshold"/>, the organisation's threshold in force now (FR-4.2).
    /// The role check (Approver only) is the endpoint policy's job, not the domain's.
    /// </summary>
    /// <exception cref="SelfApprovalException">The actor raised this request (checked first).</exception>
    /// <exception cref="InvalidTransitionException">The request is not pending approval.</exception>
    public void Approve(Guid actorUserId, decimal currentThreshold, string? comment, DateTime now)
    {
        EnsureNotSelfDecision(actorUserId);
        ArgumentOutOfRangeException.ThrowIfNegative(currentThreshold);
        EnsureCommentLength(comment);

        TransitionTo(RequestStatus.Approved, actorUserId, AuditActions.Approved, NormaliseComment(comment), now);
        ThresholdAtDecision = currentThreshold;
    }

    /// <summary>
    /// Rejects a pending request (FR-3, FR-3.4). <see cref="ThresholdAtDecision"/> stays null.
    /// The role check (Approver only) is the endpoint policy's job, not the domain's.
    /// </summary>
    /// <exception cref="SelfApprovalException">The actor raised this request (checked first).</exception>
    /// <exception cref="InvalidTransitionException">The request is not pending approval.</exception>
    public void Reject(Guid actorUserId, string? comment, DateTime now)
    {
        EnsureNotSelfDecision(actorUserId);
        EnsureCommentLength(comment);

        TransitionTo(RequestStatus.Rejected, actorUserId, AuditActions.Rejected, NormaliseComment(comment), now);
    }

    /// <summary>
    /// Completes an approved request with its actual cost and flags an overrun (FR-4.4):
    /// an auto-approved request overruns when <c>ActualCost &gt;= ThresholdAtDecision</c>; a
    /// manually approved one when <c>ActualCost &gt; EstimatedCost</c>. On an overrun the request
    /// still completes, <see cref="ExceededThreshold"/> is set and the audit comment records it.
    /// Who may complete (the raiser or an Approver) is a handler ownership rule, not checked here.
    /// </summary>
    /// <remarks>
    /// Handlers must load <see cref="AuditEntries"/> (for example with
    /// <c>Include(r =&gt; r.AuditEntries)</c>) before calling this: auto versus manual approval is
    /// read from the <see cref="RequestStatus.Approved"/> entry, whose actor is null for
    /// auto-approval. If the entries are not loaded, this throws
    /// <see cref="InvalidOperationException"/> rather than guessing.
    /// </remarks>
    /// <exception cref="InvalidTransitionException">The request is not approved.</exception>
    public void Complete(Guid actorUserId, decimal actualCost, DateTime now)
    {
        EnsureNotEmpty(actorUserId, nameof(actorUserId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(actualCost);
        RequestTransitions.EnsureAllowed(Status, RequestStatus.Completed);

        var overrunComment = DescribeOverrun(actualCost);

        TransitionTo(RequestStatus.Completed, actorUserId, AuditActions.Completed, overrunComment, now);
        ActualCost = actualCost;
        CompletedAt = now;
        ExceededThreshold = overrunComment is not null;
    }

    // Returns the audit comment for an overrun, or null when the actual cost is within what was
    // authorised (FR-4.4).
    private string? DescribeOverrun(decimal actualCost)
    {
        var approval = _auditEntries.LastOrDefault(e => e.ToStatus == RequestStatus.Approved)
            ?? throw new InvalidOperationException(
                "The request's audit entries must be loaded before it is completed.");

        if (approval.ActorUserId is null)
        {
            var threshold = ThresholdAtDecision
                ?? throw new InvalidOperationException("An auto-approved request has no threshold snapshot.");

            return actualCost >= threshold
                ? string.Create(CultureInfo.InvariantCulture, $"Actual cost {actualCost:N2} reached or exceeded the auto-approval threshold {threshold:N2}.")
                : null;
        }

        return actualCost > EstimatedCost
            ? string.Create(CultureInfo.InvariantCulture, $"Actual cost {actualCost:N2} exceeded the approved estimate {EstimatedCost:N2}.")
            : null;
    }

    private void EnsureNotSelfDecision(Guid actorUserId)
    {
        EnsureNotEmpty(actorUserId, nameof(actorUserId));
        if (actorUserId == RaisedByUserId)
        {
            throw new SelfApprovalException();
        }
    }

    private static void EnsureCommentLength(string? comment)
    {
        if (comment is { Length: > MaxCommentLength })
        {
            throw new ArgumentException($"The comment must be at most {MaxCommentLength} characters.", nameof(comment));
        }
    }

    private static string? NormaliseComment(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? null : comment;

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
