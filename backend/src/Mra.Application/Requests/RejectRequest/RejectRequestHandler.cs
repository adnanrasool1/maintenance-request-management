using MediatR;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;

namespace Mra.Application.Requests.RejectRequest;

// Same error behaviour as approve: self-rejection 403, not pending 409, and a concurrent
// decision saved first surfaces as an uncaught DbUpdateConcurrencyException (409, FR-3.5).
public sealed class RejectRequestHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<RejectRequestCommand, RequestDetail>
{
    public async Task<RequestDetail> Handle(RejectRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Filtered set: another organisation's request is not found (404).
        var maintenanceRequest = await db.MaintenanceRequests
            .SingleOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException();

        maintenanceRequest.Reject(userId, request.Comment, timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        return await db.MaintenanceRequests.FindDetailAsync(db, maintenanceRequest.Id, cancellationToken)
            ?? throw new NotFoundException();
    }
}
