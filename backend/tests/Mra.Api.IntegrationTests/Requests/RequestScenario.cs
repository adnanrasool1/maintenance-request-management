using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Application.Requests;
using Mra.Application.Requests.ApproveRequest;
using Mra.Application.Requests.CompleteRequest;
using Mra.Application.Requests.CreateRequest;
using Mra.Application.Requests.GetRequestById;
using Mra.Application.Requests.GetRequests;
using Mra.Application.Requests.RejectRequest;
using Mra.Api.IntegrationTests.Persistence;
using Mra.Domain.Organisations;
using Mra.Domain.Requests;
using Mra.Domain.Sites;
using Mra.Domain.Users;
using Mra.Infrastructure.Persistence;
using Xunit;

namespace Mra.Api.IntegrationTests.Requests;

// An organisation with one site, two Requesters and two Approvers, seeded the way the System Admin
// and Tenant Admin would create them.
public sealed record Org(
    Guid Id,
    Guid SiteId,
    decimal Threshold,
    Caller Requester,
    Caller OtherRequester,
    Caller Approver,
    Caller OtherApprover);

// The caller as ICurrentUser would read it from the JWT claims.
public sealed record Caller(Guid UserId, Guid OrganisationId, Role UserRole) : ICurrentUser
{
    Guid? ICurrentUser.UserId => UserId;

    Guid? ICurrentUser.OrganisationId => OrganisationId;

    string? ICurrentUser.Role => UserRole.ToString();

    bool ICurrentUser.IsAuthenticated => true;
}

// Runs the real request handlers against the Testcontainers database, each call on a fresh
// context scoped to the caller's organisation (as a request scope would be in the API).
public sealed class RequestScenario(SqlServerFixture fixture)
{
    public static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    private static readonly TimeProvider Clock = new FixedTimeProvider(Now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async Task<Org> SeedOrgAsync(decimal threshold = 1000m)
    {
        var organisation = Organisation.Create($"Org {Guid.NewGuid():N}", threshold);
        var site = Site.Create(organisation.Id, "Head office");
        var users = new[]
        {
            User.Create(organisation.Id, SqlServerFixture.UniqueEmail(), "hash", Role.Requester),
            User.Create(organisation.Id, SqlServerFixture.UniqueEmail(), "hash", Role.Requester),
            User.Create(organisation.Id, SqlServerFixture.UniqueEmail(), "hash", Role.Approver),
            User.Create(organisation.Id, SqlServerFixture.UniqueEmail(), "hash", Role.Approver),
        };

        await using (var db = fixture.CreateContext(organisationId: null))
        {
            db.Add(organisation);
            db.Add(site);
            db.AddRange(users);
            await db.SaveChangesAsync(Ct);
        }

        Caller As(User user) => new(user.Id, organisation.Id, user.Role);
        return new Org(organisation.Id, site.Id, threshold, As(users[0]), As(users[1]), As(users[2]), As(users[3]));
    }

    public Task<RequestDetail> RaiseAsync(Caller caller, decimal estimatedCost, Guid siteId) =>
        Run(caller, (db, user) => new CreateRequestHandler(db, user, Clock)
            .Handle(new CreateRequestCommand(siteId, "Replace broken air-conditioning unit", estimatedCost), Ct));

    public Task<RequestDetail> RaiseAsync(Org org, Caller caller, decimal estimatedCost) =>
        RaiseAsync(caller, estimatedCost, org.SiteId);

    public Task<IReadOnlyList<RequestSummary>> ListAsync(Caller caller, string? status = null) =>
        Run(caller, (db, user) => new GetRequestsHandler(db, user).Handle(new GetRequestsQuery(status), Ct));

    public Task<RequestDetail> GetAsync(Caller caller, Guid id) =>
        Run(caller, (db, user) => new GetRequestByIdHandler(db, user).Handle(new GetRequestByIdQuery(id), Ct));

    public Task<RequestDetail> ApproveAsync(Caller caller, Guid id, string? comment = null) =>
        Run(caller, (db, user) => ApproveAsync(db, user, id, comment));

    public static Task<RequestDetail> ApproveAsync(IAppDbContext db, Caller caller, Guid id, string? comment = null) =>
        new ApproveRequestHandler(db, caller, Clock).Handle(new ApproveRequestCommand(id, comment), Ct);

    public Task<RequestDetail> RejectAsync(Caller caller, Guid id, string? comment = null) =>
        Run(caller, (db, user) => RejectAsync(db, user, id, comment));

    public static Task<RequestDetail> RejectAsync(IAppDbContext db, Caller caller, Guid id, string? comment = null) =>
        new RejectRequestHandler(db, caller, Clock).Handle(new RejectRequestCommand(id, comment), Ct);

    public Task<RequestDetail> CompleteAsync(Caller caller, Guid id, decimal actualCost) =>
        Run(caller, (db, user) => new CompleteRequestHandler(db, user, Clock)
            .Handle(new CompleteRequestCommand(id, actualCost), Ct));

    public AppDbContext CreateContext(Guid organisationId) => fixture.CreateContext(organisationId);

    // The stored row and its audit trail, read directly (bypassing the handlers) as its own
    // organisation sees them.
    public async Task<Snapshot> SnapshotAsync(Guid organisationId, Guid requestId)
    {
        await using var db = fixture.CreateContext(organisationId);
        var request = await db.MaintenanceRequests.AsNoTracking().SingleAsync(r => r.Id == requestId, Ct);
        var audit = await db.AuditEntries.AsNoTracking()
            .Where(a => a.RequestId == requestId)
            .OrderBy(a => a.Id)
            .Select(a => new AuditRow(a.Action, a.ActorUserId, a.FromStatus, a.ToStatus, a.Comment))
            .ToListAsync(Ct);

        return new Snapshot(request.Status, request.ActualCost, request.ThresholdAtDecision, request.ExceededThreshold, request.RowVersion, audit);
    }

    private async Task<T> Run<T>(Caller caller, Func<AppDbContext, Caller, Task<T>> action)
    {
        await using var db = fixture.CreateContext(caller.OrganisationId);
        return await action(db, caller);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}

public sealed record AuditRow(string Action, Guid? ActorUserId, RequestStatus? FromStatus, RequestStatus ToStatus, string? Comment);

public sealed record Snapshot(
    RequestStatus Status,
    decimal? ActualCost,
    decimal? ThresholdAtDecision,
    bool ExceededThreshold,
    byte[] RowVersion,
    IReadOnlyList<AuditRow> Audit);
