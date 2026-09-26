using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Domain.Requests;

namespace Mra.Application.Requests;

// Manual projections to the response shapes (contract §3.13). They run in SQL, so no entity
// graph is loaded. The joined Sites and Users sets are tenant-filtered like the requests.
internal static class RequestMappings
{
    public static IQueryable<RequestSummary> SelectSummary(this IQueryable<MaintenanceRequest> requests, IAppDbContext db) =>
        from r in requests
        join s in db.Sites on r.SiteId equals s.Id
        select new RequestSummary(
            r.Id,
            r.SiteId,
            s.Name,
            r.Description,
            r.EstimatedCost,
            r.Status,
            r.RaisedByUserId,
            r.CreatedAt);

    public static IQueryable<RequestDetail> SelectDetail(this IQueryable<MaintenanceRequest> requests, IAppDbContext db) =>
        from r in requests
        join s in db.Sites on r.SiteId equals s.Id
        join u in db.Users on r.RaisedByUserId equals u.Id
        select new RequestDetail(
            r.Id,
            r.SiteId,
            s.Name,
            r.RaisedByUserId,
            u.Email,
            r.Description,
            r.EstimatedCost,
            r.ActualCost,
            r.Status,
            r.ThresholdAtDecision,
            r.ExceededThreshold,
            r.CreatedAt,
            r.CompletedAt);

    // One request as a RequestDetail, read without tracking; null when the (already filtered)
    // set doesn't contain it. Commands call it after their save to build the response.
    public static Task<RequestDetail?> FindDetailAsync(
        this IQueryable<MaintenanceRequest> requests, IAppDbContext db, Guid id, CancellationToken cancellationToken) =>
        requests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .SelectDetail(db)
            .SingleOrDefaultAsync(cancellationToken);
}
