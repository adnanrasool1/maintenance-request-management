using MediatR;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;

namespace Mra.Application.Requests.GetRequests;

public sealed class GetRequestsHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetRequestsQuery, IReadOnlyList<RequestSummary>>
{
    public async Task<IReadOnlyList<RequestSummary>> Handle(GetRequestsQuery request, CancellationToken cancellationToken)
    {
        var requests = db.MaintenanceRequests.AsNoTracking().VisibleTo(currentUser);

        // The validator has already rejected unknown values, so null here means "no filter".
        if (GetRequestsQuery.ParseStatus(request.Status) is { } status)
        {
            requests = requests.Where(r => r.Status == status);
        }

        return await requests
            .OrderByDescending(r => r.CreatedAt)
            .SelectSummary(db)
            .ToListAsync(cancellationToken);
    }
}
