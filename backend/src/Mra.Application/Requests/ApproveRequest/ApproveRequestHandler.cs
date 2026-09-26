using MediatR;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;

namespace Mra.Application.Requests.ApproveRequest;

// Self-approval (403) and a request that isn't pending (409) are domain exceptions. A concurrent
// decision saved first makes SaveChangesAsync throw DbUpdateConcurrencyException (rowversion),
// which is deliberately not caught: it becomes 409 (FR-3.5).
public sealed class ApproveRequestHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<ApproveRequestCommand, RequestDetail>
{
    public async Task<RequestDetail> Handle(ApproveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Filtered set: another organisation's request is not found (404).
        var maintenanceRequest = await db.MaintenanceRequests
            .SingleOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException();

        // The threshold in force now is snapshotted on approval (FR-4.2).
        var threshold = await db.Organisations
            .Where(o => o.Id == maintenanceRequest.OrganisationId)
            .Select(o => o.ApprovalThreshold)
            .SingleAsync(cancellationToken);

        maintenanceRequest.Approve(userId, threshold, request.Comment, timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        return await db.MaintenanceRequests.FindDetailAsync(db, maintenanceRequest.Id, cancellationToken)
            ?? throw new NotFoundException();
    }
}
