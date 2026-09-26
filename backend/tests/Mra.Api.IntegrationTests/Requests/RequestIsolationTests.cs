using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Exceptions;
using Mra.Api.IntegrationTests.Persistence;
using Mra.Domain.Requests;
using Xunit;

namespace Mra.Api.IntegrationTests.Requests;

// Tenant isolation and Requester visibility through the real handlers (architecture §7, §13;
// FR-5.1, FR-5.3; contract §3.6–§3.11). Another tenant's ID, and another Requester's request,
// must look exactly like a missing ID: NotFoundException, and nothing changes.
public sealed class RequestIsolationTests(SqlServerFixture fixture)
{
    private readonly RequestScenario _scenario = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creating_a_request_on_another_organisations_site_is_not_found_and_creates_nothing()
    {
        var orgA = await _scenario.SeedOrgAsync();
        var orgB = await _scenario.SeedOrgAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _scenario.RaiseAsync(orgA.Requester, 100m, orgB.SiteId));

        await using var dbB = _scenario.CreateContext(orgB.Id);
        Assert.False(await dbB.MaintenanceRequests.AnyAsync(Ct));
        await using var dbA = _scenario.CreateContext(orgA.Id);
        Assert.False(await dbA.MaintenanceRequests.AnyAsync(Ct));
    }

    [Fact]
    public async Task Creating_a_request_on_a_missing_site_is_not_found()
    {
        var org = await _scenario.SeedOrgAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _scenario.RaiseAsync(org.Requester, 100m, Guid.NewGuid()));
    }

    [Fact]
    public async Task Getting_another_organisations_request_is_not_found()
    {
        var orgA = await _scenario.SeedOrgAsync();
        var orgB = await _scenario.SeedOrgAsync();
        var requestB = await _scenario.RaiseAsync(orgB, orgB.Requester, 100m);

        await Assert.ThrowsAsync<NotFoundException>(() => _scenario.GetAsync(orgA.Approver, requestB.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _scenario.GetAsync(orgA.Requester, requestB.Id));
    }

    [Fact]
    public async Task Approving_or_rejecting_another_organisations_request_is_not_found_and_leaves_it_unchanged()
    {
        var orgA = await _scenario.SeedOrgAsync();
        var orgB = await _scenario.SeedOrgAsync();
        var requestB = await _scenario.RaiseAsync(orgB, orgB.Requester, 5000m);
        var before = await _scenario.SnapshotAsync(orgB.Id, requestB.Id);
        Assert.Equal(RequestStatus.PendingApproval, before.Status);

        await Assert.ThrowsAsync<NotFoundException>(() => _scenario.ApproveAsync(orgA.Approver, requestB.Id, "cross-tenant"));
        await Assert.ThrowsAsync<NotFoundException>(() => _scenario.RejectAsync(orgA.Approver, requestB.Id, "cross-tenant"));

        AssertUnchanged(before, await _scenario.SnapshotAsync(orgB.Id, requestB.Id));
    }

    [Fact]
    public async Task Completing_another_organisations_request_is_not_found_and_leaves_it_unchanged()
    {
        var orgA = await _scenario.SeedOrgAsync();
        var orgB = await _scenario.SeedOrgAsync();
        var requestB = await _scenario.RaiseAsync(orgB, orgB.Requester, 100m);
        var before = await _scenario.SnapshotAsync(orgB.Id, requestB.Id);
        Assert.Equal(RequestStatus.Approved, before.Status);

        await Assert.ThrowsAsync<NotFoundException>(() => _scenario.CompleteAsync(orgA.Approver, requestB.Id, 90m));
        await Assert.ThrowsAsync<NotFoundException>(() => _scenario.CompleteAsync(orgA.Requester, requestB.Id, 90m));

        AssertUnchanged(before, await _scenario.SnapshotAsync(orgB.Id, requestB.Id));
    }

    [Fact]
    public async Task Another_organisations_requests_never_appear_in_the_list()
    {
        var orgA = await _scenario.SeedOrgAsync();
        var orgB = await _scenario.SeedOrgAsync();
        var requestA = await _scenario.RaiseAsync(orgA, orgA.Requester, 100m);
        await _scenario.RaiseAsync(orgB, orgB.Requester, 100m);

        var list = await _scenario.ListAsync(orgA.Approver);

        Assert.Equal([requestA.Id], list.Select(r => r.Id));
    }

    [Fact]
    public async Task A_requester_cannot_read_another_requesters_request_but_an_approver_can()
    {
        var org = await _scenario.SeedOrgAsync();
        var request = await _scenario.RaiseAsync(org, org.OtherRequester, 100m);

        await Assert.ThrowsAsync<NotFoundException>(() => _scenario.GetAsync(org.Requester, request.Id));

        Assert.Equal(request.Id, (await _scenario.GetAsync(org.OtherRequester, request.Id)).Id);
        Assert.Equal(request.Id, (await _scenario.GetAsync(org.Approver, request.Id)).Id);
    }

    [Fact]
    public async Task A_requester_cannot_complete_another_requesters_request_and_it_is_unchanged()
    {
        var org = await _scenario.SeedOrgAsync();
        var request = await _scenario.RaiseAsync(org, org.OtherRequester, 100m);
        var before = await _scenario.SnapshotAsync(org.Id, request.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => _scenario.CompleteAsync(org.Requester, request.Id, 90m));

        AssertUnchanged(before, await _scenario.SnapshotAsync(org.Id, request.Id));
    }

    private static void AssertUnchanged(Snapshot before, Snapshot after)
    {
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.ActualCost, after.ActualCost);
        Assert.Equal(before.ThresholdAtDecision, after.ThresholdAtDecision);
        Assert.Equal(before.RowVersion, after.RowVersion);
        Assert.Equal(before.Audit, after.Audit);
    }
}
