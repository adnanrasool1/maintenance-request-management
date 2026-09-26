using MediatR;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;

namespace Mra.Application.Requests.GetRequestById;

public sealed class GetRequestByIdHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetRequestByIdQuery, RequestDetail>
{
    // Missing, another organisation's, and (for a Requester) another user's request all give
    // the same NotFoundException, so the response doesn't reveal which (FR-5.3).
    public async Task<RequestDetail> Handle(GetRequestByIdQuery request, CancellationToken cancellationToken) =>
        await db.MaintenanceRequests.VisibleTo(currentUser).FindDetailAsync(db, request.Id, cancellationToken)
            ?? throw new NotFoundException();
}
