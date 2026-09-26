using MediatR;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;

namespace Mra.Application.Requests.CompleteRequest;

// Who may complete (A-5, contract §3.11): an Approver any request in the organisation, a
// Requester only their own; anyone else's is not found (404), as for reads. A request that
// isn't approved is a domain InvalidTransitionException (409); the overrun flag is set by the
// domain (FR-4.4).
public sealed class CompleteRequestHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<CompleteRequestCommand, RequestDetail>
{
    public async Task<RequestDetail> Handle(CompleteRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Complete reads the approval audit entry to tell auto from manual approval, so the
        // entries must be loaded.
        var maintenanceRequest = await db.MaintenanceRequests
            .VisibleTo(currentUser)
            .Include(r => r.AuditEntries)
            .SingleOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException();

        maintenanceRequest.Complete(userId, request.ActualCost, timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        return await db.MaintenanceRequests.FindDetailAsync(db, maintenanceRequest.Id, cancellationToken)
            ?? throw new NotFoundException();
    }
}
