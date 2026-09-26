using MediatR;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;
using Mra.Domain.Requests;

namespace Mra.Application.Requests.CreateRequest;

public sealed class CreateRequestHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<CreateRequestCommand, RequestDetail>
{
    public async Task<RequestDetail> Handle(CreateRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Filtered set: a site in another organisation looks the same as a missing one (404).
        var site = await db.Sites
            .AsNoTracking()
            .Where(s => s.Id == request.SiteId)
            .Select(s => new { s.Id, s.OrganisationId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException();

        var threshold = await db.Organisations
            .Where(o => o.Id == site.OrganisationId)
            .Select(o => o.ApprovalThreshold)
            .SingleAsync(cancellationToken);

        // Creation and routing happen together in the aggregate and are saved together (FR-3.3).
        var maintenanceRequest = MaintenanceRequest.Raise(
            site.OrganisationId,
            site.Id,
            userId,
            request.Description,
            request.EstimatedCost,
            threshold,
            timeProvider.GetUtcNow().UtcDateTime);

        db.MaintenanceRequests.Add(maintenanceRequest);
        await db.SaveChangesAsync(cancellationToken);

        return await db.MaintenanceRequests.FindDetailAsync(db, maintenanceRequest.Id, cancellationToken)
            ?? throw new NotFoundException();
    }
}
