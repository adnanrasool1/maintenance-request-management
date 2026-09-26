using Mra.Domain.Requests;
using Xunit;

namespace Mra.Domain.Tests;

public sealed class MaintenanceRequestTests
{
    private const decimal Threshold = 500m;

    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid RaiserId = Guid.NewGuid();
    private static readonly Guid ApproverId = Guid.NewGuid();
    private static readonly DateTime RaisedAt = new(2026, 9, 26, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DecidedAt = RaisedAt.AddHours(1);
    private static readonly DateTime CompletedAt = RaisedAt.AddDays(1);

    private static MaintenanceRequest Raise(decimal estimatedCost, decimal threshold = Threshold) =>
        MaintenanceRequest.Raise(OrgId, SiteId, RaiserId, "Fix the boiler", estimatedCost, threshold, RaisedAt);

    private static MaintenanceRequest Pending() => Raise(Threshold + 100m);

    private static MaintenanceRequest AutoApproved() => Raise(Threshold - 100m);

    private static MaintenanceRequest ManuallyApproved()
    {
        var request = Pending();
        request.Approve(ApproverId, Threshold, null, DecidedAt);
        return request;
    }

    // ---- Raise and threshold routing (FR-3.3, FR-4.1, A-2) ----

    [Fact]
    public void Raise_at_exactly_the_threshold_requires_approval()
    {
        var request = Raise(Threshold);

        Assert.Equal(RequestStatus.PendingApproval, request.Status);
        Assert.Null(request.ThresholdAtDecision);
    }

    [Fact]
    public void Raise_one_cent_below_the_threshold_is_auto_approved()
    {
        var request = Raise(Threshold - 0.01m);

        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Equal(Threshold, request.ThresholdAtDecision);
    }

    [Fact]
    public void Raise_sets_fields()
    {
        var request = Raise(123.45m);

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal(OrgId, request.OrganisationId);
        Assert.Equal(SiteId, request.SiteId);
        Assert.Equal(RaiserId, request.RaisedByUserId);
        Assert.Equal("Fix the boiler", request.Description);
        Assert.Equal(123.45m, request.EstimatedCost);
        Assert.Equal(RaisedAt, request.CreatedAt);
        Assert.Null(request.ActualCost);
        Assert.Null(request.CompletedAt);
        Assert.False(request.ExceededThreshold);
    }

    [Fact]
    public void Raise_with_auto_approval_writes_two_audit_entries()
    {
        var request = AutoApproved();

        Assert.Collection(
            request.AuditEntries,
            e => AssertEntry(e, request, RaiserId, AuditActions.Raised, null, RequestStatus.Raised, RaisedAt),
            e => AssertEntry(e, request, null, AuditActions.AutoApproved, RequestStatus.Raised, RequestStatus.Approved, RaisedAt));
    }

    [Fact]
    public void Raise_with_routing_to_approval_writes_two_audit_entries()
    {
        var request = Pending();

        Assert.Collection(
            request.AuditEntries,
            e => AssertEntry(e, request, RaiserId, AuditActions.Raised, null, RequestStatus.Raised, RaisedAt),
            e => AssertEntry(e, request, null, AuditActions.RoutedForApproval, RequestStatus.Raised, RequestStatus.PendingApproval, RaisedAt));
    }

    [Fact]
    public void Raise_with_zero_threshold_sends_every_request_to_approval()
    {
        Assert.Equal(RequestStatus.PendingApproval, Raise(0.01m, threshold: 0m).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Raise_rejects_blank_description(string description)
    {
        Assert.Throws<ArgumentException>(() =>
            MaintenanceRequest.Raise(OrgId, SiteId, RaiserId, description, 10m, Threshold, RaisedAt));
    }

    [Fact]
    public void Raise_accepts_2000_characters_and_rejects_2001()
    {
        var ok = MaintenanceRequest.Raise(OrgId, SiteId, RaiserId, new string('a', 2000), 10m, Threshold, RaisedAt);
        Assert.Equal(2000, ok.Description.Length);

        Assert.Throws<ArgumentException>(() =>
            MaintenanceRequest.Raise(OrgId, SiteId, RaiserId, new string('a', 2001), 10m, Threshold, RaisedAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Raise_rejects_non_positive_cost(decimal cost)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Raise(cost));
    }

    [Fact]
    public void Raise_rejects_empty_ids()
    {
        Assert.Throws<ArgumentException>(() =>
            MaintenanceRequest.Raise(Guid.Empty, SiteId, RaiserId, "x", 10m, Threshold, RaisedAt));
        Assert.Throws<ArgumentException>(() =>
            MaintenanceRequest.Raise(OrgId, Guid.Empty, RaiserId, "x", 10m, Threshold, RaisedAt));
        Assert.Throws<ArgumentException>(() =>
            MaintenanceRequest.Raise(OrgId, SiteId, Guid.Empty, "x", 10m, Threshold, RaisedAt));
    }

    // ---- Approve and reject (FR-3, FR-3.4, FR-4.2) ----

    [Fact]
    public void Approve_moves_pending_to_approved_with_one_audit_entry_and_comment()
    {
        var request = Pending();

        request.Approve(ApproverId, Threshold, "Go ahead", DecidedAt);

        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Equal(3, request.AuditEntries.Count);
        AssertEntry(request.AuditEntries.Last(), request, ApproverId, AuditActions.Approved,
            RequestStatus.PendingApproval, RequestStatus.Approved, DecidedAt, "Go ahead");
    }

    [Fact]
    public void Reject_moves_pending_to_rejected_with_one_audit_entry_and_comment()
    {
        var request = Pending();

        request.Reject(ApproverId, "Too expensive", DecidedAt);

        Assert.Equal(RequestStatus.Rejected, request.Status);
        Assert.Null(request.ThresholdAtDecision);
        Assert.Equal(3, request.AuditEntries.Count);
        AssertEntry(request.AuditEntries.Last(), request, ApproverId, AuditActions.Rejected,
            RequestStatus.PendingApproval, RequestStatus.Rejected, DecidedAt, "Too expensive");
    }

    [Fact]
    public void Approve_and_reject_without_comment_store_null()
    {
        var approved = Pending();
        approved.Approve(ApproverId, Threshold, null, DecidedAt);
        Assert.Null(approved.AuditEntries.Last().Comment);

        var rejected = Pending();
        rejected.Reject(ApproverId, "  ", DecidedAt);
        Assert.Null(rejected.AuditEntries.Last().Comment);
    }

    [Fact]
    public void Auto_approval_snapshots_the_raise_time_threshold()
    {
        Assert.Equal(750m, Raise(10m, threshold: 750m).ThresholdAtDecision);
    }

    [Fact]
    public void Manual_approval_snapshots_the_approve_time_threshold()
    {
        var request = Raise(600m, threshold: 500m);

        request.Approve(ApproverId, 800m, null, DecidedAt);

        Assert.Equal(800m, request.ThresholdAtDecision);
    }

    [Fact]
    public void Self_approval_of_a_pending_request_is_forbidden()
    {
        var request = Pending();

        Assert.Throws<SelfApprovalException>(() => request.Approve(RaiserId, Threshold, null, DecidedAt));
        Assert.Equal(RequestStatus.PendingApproval, request.Status);
        Assert.Equal(2, request.AuditEntries.Count);
    }

    [Fact]
    public void Self_rejection_of_a_pending_request_is_forbidden()
    {
        var request = Pending();

        Assert.Throws<SelfApprovalException>(() => request.Reject(RaiserId, null, DecidedAt));
        Assert.Equal(RequestStatus.PendingApproval, request.Status);
        Assert.Equal(2, request.AuditEntries.Count);
    }

    [Fact]
    public void Self_approval_check_runs_before_the_transition_check()
    {
        var request = AutoApproved();

        Assert.Throws<SelfApprovalException>(() => request.Approve(RaiserId, Threshold, null, DecidedAt));
        Assert.Throws<SelfApprovalException>(() => request.Reject(RaiserId, null, DecidedAt));
    }

    // ---- Illegal transitions (FR-3.1, FR-3.2) ----
    // Raised is never a resting state (Raise always routes), so it can't be reached through the
    // public API; every state a request can rest in is covered.

    public static TheoryData<string, string> IllegalTransitions => new()
    {
        { "PendingApproval", "Complete" },
        { "AutoApproved", "Approve" },
        { "AutoApproved", "Reject" },
        { "ManuallyApproved", "Approve" },
        { "ManuallyApproved", "Reject" },
        { "Rejected", "Approve" },
        { "Rejected", "Reject" },
        { "Rejected", "Complete" },
        { "Completed", "Approve" },
        { "Completed", "Reject" },
        { "Completed", "Complete" },
    };

    [Theory]
    [MemberData(nameof(IllegalTransitions))]
    public void Illegal_transitions_are_rejected_without_side_effects(string state, string action)
    {
        var request = InState(state);
        var statusBefore = request.Status;
        var auditCountBefore = request.AuditEntries.Count;
        var thresholdBefore = request.ThresholdAtDecision;
        var actualCostBefore = request.ActualCost;

        var ex = Assert.Throws<InvalidTransitionException>(() => Apply(request, action));

        Assert.Equal(statusBefore, ex.From);
        Assert.IsAssignableFrom<DomainException>(ex);
        Assert.Equal(statusBefore, request.Status);
        Assert.Equal(auditCountBefore, request.AuditEntries.Count);
        Assert.Equal(thresholdBefore, request.ThresholdAtDecision);
        Assert.Equal(actualCostBefore, request.ActualCost);
    }

    private static MaintenanceRequest InState(string state)
    {
        switch (state)
        {
            case "PendingApproval":
                return Pending();
            case "AutoApproved":
                return AutoApproved();
            case "ManuallyApproved":
                return ManuallyApproved();
            case "Rejected":
                var rejected = Pending();
                rejected.Reject(ApproverId, null, DecidedAt);
                return rejected;
            case "Completed":
                var completed = AutoApproved();
                completed.Complete(RaiserId, 100m, CompletedAt);
                return completed;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    private static void Apply(MaintenanceRequest request, string action)
    {
        switch (action)
        {
            case "Approve":
                request.Approve(ApproverId, Threshold, null, CompletedAt);
                break;
            case "Reject":
                request.Reject(ApproverId, null, CompletedAt);
                break;
            case "Complete":
                request.Complete(ApproverId, 100m, CompletedAt);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    // ---- Complete and overrun (FR-4.4) ----

    [Fact]
    public void Complete_sets_cost_and_time_with_one_audit_entry()
    {
        var request = AutoApproved();

        request.Complete(RaiserId, 350m, CompletedAt);

        Assert.Equal(RequestStatus.Completed, request.Status);
        Assert.Equal(350m, request.ActualCost);
        Assert.Equal(CompletedAt, request.CompletedAt);
        Assert.False(request.ExceededThreshold);
        Assert.Equal(3, request.AuditEntries.Count);
        AssertEntry(request.AuditEntries.Last(), request, RaiserId, AuditActions.Completed,
            RequestStatus.Approved, RequestStatus.Completed, CompletedAt, null);
    }

    [Theory]
    [InlineData(499.99, false)]
    [InlineData(500.00, true)]
    [InlineData(500.01, true)]
    public void Auto_approved_request_overruns_at_or_above_its_threshold_snapshot(decimal actualCost, bool overrun)
    {
        var request = Raise(400m, threshold: 500m);

        request.Complete(RaiserId, actualCost, CompletedAt);

        Assert.Equal(overrun, request.ExceededThreshold);
        var comment = request.AuditEntries.Last().Comment;
        if (overrun)
        {
            Assert.NotNull(comment);
            Assert.Contains(actualCost.ToString("N2", System.Globalization.CultureInfo.InvariantCulture), comment);
            Assert.Contains("500.00", comment);
        }
        else
        {
            Assert.Null(comment);
        }
    }

    [Theory]
    [InlineData(6199.99, false)]
    [InlineData(6200.00, false)]
    [InlineData(6450.00, true)]
    public void Manually_approved_request_overruns_only_above_its_estimate(decimal actualCost, bool overrun)
    {
        var request = Raise(6200m, threshold: 5000m);
        request.Approve(ApproverId, 5000m, null, DecidedAt);

        request.Complete(ApproverId, actualCost, CompletedAt);

        Assert.Equal(RequestStatus.Completed, request.Status);
        Assert.Equal(overrun, request.ExceededThreshold);
        var entry = request.AuditEntries.Last();
        Assert.Equal(AuditActions.Completed, entry.Action);
        if (overrun)
        {
            Assert.Equal("Actual cost 6,450.00 exceeded the approved estimate 6,200.00.", entry.Comment);
        }
        else
        {
            Assert.Null(entry.Comment);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Complete_rejects_non_positive_actual_cost(decimal actualCost)
    {
        var request = AutoApproved();

        Assert.Throws<ArgumentOutOfRangeException>(() => request.Complete(RaiserId, actualCost, CompletedAt));
        Assert.Equal(RequestStatus.Approved, request.Status);
    }

    // ---- Audit trail (FR-7, T1.6) ----

    [Fact]
    public void Full_lifecycle_writes_exactly_one_entry_per_transition()
    {
        var request = Pending();
        request.Approve(ApproverId, Threshold, "ok", DecidedAt);
        request.Complete(RaiserId, 600m, CompletedAt);

        Assert.Collection(
            request.AuditEntries,
            e => AssertEntry(e, request, RaiserId, AuditActions.Raised, null, RequestStatus.Raised, RaisedAt),
            e => AssertEntry(e, request, null, AuditActions.RoutedForApproval, RequestStatus.Raised, RequestStatus.PendingApproval, RaisedAt),
            e => AssertEntry(e, request, ApproverId, AuditActions.Approved, RequestStatus.PendingApproval, RequestStatus.Approved, DecidedAt, "ok"),
            e => AssertEntry(e, request, RaiserId, AuditActions.Completed, RequestStatus.Approved, RequestStatus.Completed, CompletedAt));
    }

    private static void AssertEntry(
        AuditEntry entry,
        MaintenanceRequest request,
        Guid? actor,
        string action,
        RequestStatus? from,
        RequestStatus to,
        DateTime at,
        string? comment = null)
    {
        Assert.Equal(request.OrganisationId, entry.OrganisationId);
        Assert.Equal(request.Id, entry.RequestId);
        Assert.Equal(actor, entry.ActorUserId);
        Assert.Equal(action, entry.Action);
        Assert.Equal(from, entry.FromStatus);
        Assert.Equal(to, entry.ToStatus);
        Assert.Equal(at, entry.OccurredAt);
        Assert.Equal(comment, entry.Comment);
    }
}
