using MediatR;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;

namespace Mra.Application.Sites.GetSites;

public sealed class GetSitesHandler(IAppDbContext db) : IRequestHandler<GetSitesQuery, IReadOnlyList<SiteDto>>
{
    // The tenant query filter limits the set to the caller's organisation (architecture §7).
    public async Task<IReadOnlyList<SiteDto>> Handle(GetSitesQuery query, CancellationToken cancellationToken) =>
        await db.Sites
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SiteDto(s.Id, s.Name))
            .ToListAsync(cancellationToken);
}
