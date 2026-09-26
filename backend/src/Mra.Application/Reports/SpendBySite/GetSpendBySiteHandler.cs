using MediatR;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Domain.Requests;

namespace Mra.Application.Reports.SpendBySite;

/// <summary>
/// One row per site in the caller's organisation, zero-spend sites included, ordered by site name
/// (FR-6.2, FR-6.3). Tenant scoping comes only from the global query filters on both sets (FR-6.4).
/// The per-site sums are a grouped subquery left-joined to Sites, so grouping and summing run in
/// SQL, and the subquery can be answered from the (OrganisationId, Status, CompletedAt) index.
/// </summary>
public sealed class GetSpendBySiteHandler(IAppDbContext db)
    : IRequestHandler<GetSpendBySiteQuery, SpendReportDto>
{
    public async Task<SpendReportDto> Handle(GetSpendBySiteQuery request, CancellationToken cancellationToken)
    {
        // Already checked by GetSpendBySiteValidator.
        var from = ReportDate.Parse(request.From!);
        var to = ReportDate.Parse(request.To!);

        // Inclusive UTC days: from 00:00:00Z <= CompletedAt < (to + 1 day) 00:00:00Z.
        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var totals = db.MaintenanceRequests
            .AsNoTracking()
            .Where(r => r.Status == RequestStatus.Completed && r.CompletedAt >= start && r.CompletedAt < end)
            .GroupBy(r => r.SiteId)
            .Select(g => new { SiteId = g.Key, Total = g.Sum(r => r.ActualCost) });

        var rows = await (
                from site in db.Sites.AsNoTracking()
                join total in totals on site.Id equals total.SiteId into siteTotals
                from total in siteTotals.DefaultIfEmpty()
                orderby site.Name, site.Id
                select new SpendRowDto(site.Id, site.Name, total.Total ?? 0m))
            .ToListAsync(cancellationToken);

        return new SpendReportDto(from, to, rows, rows.Sum(row => row.TotalSpend));
    }
}
