using Microsoft.EntityFrameworkCore;
using Mra.Api.IntegrationTests.Persistence;
using Mra.Application.Requests.ApproveRequest;
using Mra.Application.Requests.CompleteRequest;
using Mra.Application.Requests.CreateRequest;
using Mra.Application.Requests.GetRequests;
using Mra.Domain.Requests;
using Xunit;

namespace Mra.Api.IntegrationTests.Requests;

// The workflow through the real handlers and database: routing, list visibility, decisions,
// concurrency, completion and audit (FR-3, FR-4, FR-5; architecture §13).
public sealed class RequestWorkflowTests(SqlServerFixture fixture)
{
    private readonly RequestScenario _scenario = new(fixture);

    [Fact]
    public async Task A_request_below_the_threshold_is_auto_approved_with_the_threshold_snapshot_and_two_audit_rows()
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);

        var created = await _scenario.RaiseAsync(org, org.Requester, 999.99m);

        Assert.Equal(RequestStatus.Approved, created.Status);
        Assert.Equal(1000m, created.ThresholdAtDecision);
        Assert.Equal(org.SiteId, created.SiteId);
        Assert.Equal("Head office", created.SiteName);
        Assert.Equal(org.Requester.UserId, created.RaisedByUserId);
        Assert.False(string.IsNullOrEmpty(created.RaisedByEmail));
        Assert.Equal(RequestScenario.Now, created.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);

        var stored = await _scenario.SnapshotAsync(org.Id, created.Id);
        Assert.Equal(
            [
                new AuditRow(AuditActions.Raised, org.Requester.UserId, null, RequestStatus.Raised, null),
                new AuditRow(AuditActions.AutoApproved, null, RequestStatus.Raised, RequestStatus.Approved, null),
            ],
            stored.Audit);
    }

    [Fact]
    public async Task A_request_exactly_at_the_threshold_needs_approval()
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);

        var created = await _scenario.RaiseAsync(org, org.Requester, 1000m);

        Assert.Equal(RequestStatus.PendingApproval, created.Status);
        Assert.Null(created.ThresholdAtDecision);
        var stored = await _scenario.SnapshotAsync(org.Id, created.Id);
        Assert.Equal([AuditActions.Raised, AuditActions.RoutedForApproval], stored.Audit.Select(a => a.Action));
    }

    [Fact]
    public async Task A_requester_lists_only_their_own_requests_newest_first()
    {
        var org = await _scenario.SeedOrgAsync();
        var first = await _scenario.RaiseAsync(org, org.Requester, 100m);
        await _scenario.RaiseAsync(org, org.OtherRequester, 100m);
        await _scenario.RaiseAsync(org, org.Approver, 100m);
        var second = await _scenario.RaiseAsync(org, org.Requester, 5000m);

        var list = await _scenario.ListAsync(org.Requester);

        // All requests share the fixed clock, so compare as a set; ordering is checked below.
        Assert.Equal(new[] { first.Id, second.Id }.Order(), list.Select(r => r.Id).Order());
        Assert.All(list, r => Assert.Equal(org.Requester.UserId, r.RaisedByUserId));
        Assert.All(list, r => Assert.Equal("Head office", r.SiteName));
    }

    [Fact]
    public async Task An_approver_lists_every_request_in_the_organisation_and_can_filter_by_status()
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);
        var approved = await _scenario.RaiseAsync(org, org.Requester, 100m);
        var pending = await _scenario.RaiseAsync(org, org.OtherRequester, 5000m);
        var own = await _scenario.RaiseAsync(org, org.Approver, 2000m);

        var all = await _scenario.ListAsync(org.Approver);
        var pendingOnly = await _scenario.ListAsync(org.Approver, "pendingapproval");

        Assert.Equal(new[] { approved.Id, pending.Id, own.Id }.Order(), all.Select(r => r.Id).Order());
        Assert.Equal(new[] { pending.Id, own.Id }.Order(), pendingOnly.Select(r => r.Id).Order());
        Assert.All(pendingOnly, r => Assert.Equal(RequestStatus.PendingApproval, r.Status));
    }

    [Fact]
    public async Task The_list_is_ordered_by_creation_time_descending()
    {
        var org = await _scenario.SeedOrgAsync();
        var older = await _scenario.RaiseAsync(org, org.Requester, 100m);
        var newer = await _scenario.RaiseAsync(org, org.Requester, 200m);

        // The scenario clock is fixed, so move the older request back in time directly.
        await using (var db = _scenario.CreateContext(org.Id))
        {
            await db.MaintenanceRequests
                .Where(r => r.Id == older.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAt, RequestScenario.Now.AddDays(-1)), TestContext.Current.CancellationToken);
        }

        var list = await _scenario.ListAsync(org.Requester);

        Assert.Equal([newer.Id, older.Id], list.Select(r => r.Id));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("Approved", true)]
    [InlineData("pendingAPPROVAL", true)]
    [InlineData("Unknown", false)]
    [InlineData("3", false)]
    [InlineData("", false)]
    public void The_status_filter_accepts_only_status_names_ignoring_case(string? status, bool valid)
    {
        var result = new GetRequestsValidator().Validate(new GetRequestsQuery(status));

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(["Status"], result.Errors.Select(e => e.PropertyName));
        }
    }

    [Fact]
    public async Task Approving_a_pending_request_snapshots_the_current_threshold_and_audits_the_decision()
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);
        var created = await _scenario.RaiseAsync(org, org.Requester, 5000m);

        var approved = await _scenario.ApproveAsync(org.Approver, created.Id, "Go ahead");

        Assert.Equal(RequestStatus.Approved, approved.Status);
        Assert.Equal(1000m, approved.ThresholdAtDecision);
        var stored = await _scenario.SnapshotAsync(org.Id, created.Id);
        Assert.Equal(
            new AuditRow(AuditActions.Approved, org.Approver.UserId, RequestStatus.PendingApproval, RequestStatus.Approved, "Go ahead"),
            stored.Audit[^1]);
        Assert.Equal(3, stored.Audit.Count);
    }

    [Fact]
    public async Task Rejecting_a_pending_request_audits_the_decision_and_takes_no_threshold_snapshot()
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);
        var created = await _scenario.RaiseAsync(org, org.Requester, 5000m);

        var rejected = await _scenario.RejectAsync(org.Approver, created.Id, "Out of budget");

        Assert.Equal(RequestStatus.Rejected, rejected.Status);
        Assert.Null(rejected.ThresholdAtDecision);
        var stored = await _scenario.SnapshotAsync(org.Id, created.Id);
        Assert.Equal(
            new AuditRow(AuditActions.Rejected, org.Approver.UserId, RequestStatus.PendingApproval, RequestStatus.Rejected, "Out of budget"),
            stored.Audit[^1]);
    }

    [Fact]
    public async Task An_approver_cannot_approve_or_reject_their_own_request()
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);
        var own = await _scenario.RaiseAsync(org, org.Approver, 5000m);
        var before = await _scenario.SnapshotAsync(org.Id, own.Id);

        await Assert.ThrowsAsync<SelfApprovalException>(() => _scenario.ApproveAsync(org.Approver, own.Id));
        await Assert.ThrowsAsync<SelfApprovalException>(() => _scenario.RejectAsync(org.Approver, own.Id));

        var after = await _scenario.SnapshotAsync(org.Id, own.Id);
        Assert.Equal(before.RowVersion, after.RowVersion);
        Assert.Equal(RequestStatus.PendingApproval, after.Status);

        // A-3: another Approver can.
        Assert.Equal(RequestStatus.Approved, (await _scenario.ApproveAsync(org.OtherApprover, own.Id)).Status);
    }

    [Fact]
    public async Task Deciding_a_request_that_is_not_pending_is_an_invalid_transition()
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);
        var autoApproved = await _scenario.RaiseAsync(org, org.Requester, 100m);
        var rejected = await _scenario.RaiseAsync(org, org.Requester, 5000m);
        await _scenario.RejectAsync(org.Approver, rejected.Id);

        await Assert.ThrowsAsync<InvalidTransitionException>(() => _scenario.ApproveAsync(org.Approver, autoApproved.Id));
        await Assert.ThrowsAsync<InvalidTransitionException>(() => _scenario.RejectAsync(org.Approver, autoApproved.Id));
        await Assert.ThrowsAsync<InvalidTransitionException>(() => _scenario.ApproveAsync(org.Approver, rejected.Id));
    }

    [Fact]
    public async Task Concurrent_decisions_on_the_same_request_do_not_both_succeed()
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);
        var created = await _scenario.RaiseAsync(org, org.Requester, 5000m);

        // The second Approver's context loads the pending request before the first decision is saved.
        await using var slow = _scenario.CreateContext(org.Id);
        await slow.MaintenanceRequests.SingleAsync(r => r.Id == created.Id, TestContext.Current.CancellationToken);

        await _scenario.ApproveAsync(org.Approver, created.Id);

        // The handler reuses the stale tracked row, so its save hits the rowversion check (FR-3.5).
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => RequestScenario.RejectAsync(slow, org.OtherApprover, created.Id));

        var stored = await _scenario.SnapshotAsync(org.Id, created.Id);
        Assert.Equal(RequestStatus.Approved, stored.Status);
        Assert.Single(stored.Audit, a => a.ToStatus is RequestStatus.Approved or RequestStatus.Rejected);
    }

    [Fact]
    public async Task Concurrent_approvals_from_two_contexts_fail_on_the_second_save()
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);
        var created = await _scenario.RaiseAsync(org, org.Requester, 5000m);
        var ct = TestContext.Current.CancellationToken;

        await using var first = _scenario.CreateContext(org.Id);
        await using var second = _scenario.CreateContext(org.Id);
        var a = await first.MaintenanceRequests.SingleAsync(r => r.Id == created.Id, ct);
        var b = await second.MaintenanceRequests.SingleAsync(r => r.Id == created.Id, ct);

        a.Approve(org.Approver.UserId, 1000m, null, RequestScenario.Now);
        b.Approve(org.OtherApprover.UserId, 1000m, null, RequestScenario.Now);
        await first.SaveChangesAsync(ct);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(ct));
        var stored = await _scenario.SnapshotAsync(org.Id, created.Id);
        Assert.Single(stored.Audit, row => row.Action == AuditActions.Approved);
    }

    [Theory]
    [InlineData(999.99, false)]
    [InlineData(1000, true)]
    [InlineData(1500, true)]
    public async Task Completing_an_auto_approved_request_flags_an_overrun_at_or_above_the_threshold_snapshot(decimal actualCost, bool overrun)
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);
        var created = await _scenario.RaiseAsync(org, org.Requester, 100m);

        var completed = await _scenario.CompleteAsync(org.Requester, created.Id, actualCost);

        Assert.Equal(RequestStatus.Completed, completed.Status);
        Assert.Equal(actualCost, completed.ActualCost);
        Assert.Equal(RequestScenario.Now, completed.CompletedAt);
        Assert.Equal(overrun, completed.ExceededThreshold);

        var last = (await _scenario.SnapshotAsync(org.Id, created.Id)).Audit[^1];
        Assert.Equal(AuditActions.Completed, last.Action);
        Assert.Equal(org.Requester.UserId, last.ActorUserId);
        Assert.Equal(overrun, last.Comment is not null);
    }

    [Theory]
    [InlineData(5000, false)]
    [InlineData(5000.01, true)]
    public async Task Completing_a_manually_approved_request_flags_an_overrun_above_the_estimate(decimal actualCost, bool overrun)
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);
        var created = await _scenario.RaiseAsync(org, org.Requester, 5000m);
        await _scenario.ApproveAsync(org.Approver, created.Id);

        // An Approver may complete any request in the organisation (A-5).
        var completed = await _scenario.CompleteAsync(org.OtherApprover, created.Id, actualCost);

        Assert.Equal(RequestStatus.Completed, completed.Status);
        Assert.Equal(overrun, completed.ExceededThreshold);
        var stored = await _scenario.SnapshotAsync(org.Id, created.Id);
        Assert.Equal(
            [AuditActions.Raised, AuditActions.RoutedForApproval, AuditActions.Approved, AuditActions.Completed],
            stored.Audit.Select(a => a.Action));
    }

    [Fact]
    public async Task Completing_a_request_that_is_not_approved_is_an_invalid_transition()
    {
        var org = await _scenario.SeedOrgAsync(threshold: 1000m);
        var pending = await _scenario.RaiseAsync(org, org.Requester, 5000m);
        var completed = await _scenario.RaiseAsync(org, org.Requester, 100m);
        await _scenario.CompleteAsync(org.Requester, completed.Id, 90m);

        await Assert.ThrowsAsync<InvalidTransitionException>(() => _scenario.CompleteAsync(org.Requester, pending.Id, 90m));
        await Assert.ThrowsAsync<InvalidTransitionException>(() => _scenario.CompleteAsync(org.Requester, completed.Id, 90m));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(0.01, true)]
    [InlineData(10.555, false)]
    [InlineData(1000000.00, true)]
    [InlineData(1000000.01, false)]
    public void The_cost_rules_match_the_contract(decimal cost, bool valid)
    {
        var create = new CreateRequestValidator()
            .Validate(new CreateRequestCommand(Guid.NewGuid(), "Fix tap", cost));
        var complete = new CompleteRequestValidator()
            .Validate(new CompleteRequestCommand(Guid.NewGuid(), cost));

        Assert.Equal(valid, create.IsValid);
        Assert.Equal(valid, complete.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_description_is_rejected(string description)
    {
        var result = new CreateRequestValidator()
            .Validate(new CreateRequestCommand(Guid.NewGuid(), description, 10m));

        Assert.Equal(["Description"], result.Errors.Select(e => e.PropertyName).Distinct());
    }

    [Fact]
    public void A_comment_longer_than_2000_characters_is_rejected()
    {
        var result = new ApproveRequestValidator()
            .Validate(new ApproveRequestCommand(Guid.NewGuid(), new string('x', 2001)));

        Assert.False(result.IsValid);
        Assert.True(new ApproveRequestValidator()
            .Validate(new ApproveRequestCommand(Guid.NewGuid(), null)).IsValid);
    }
}
