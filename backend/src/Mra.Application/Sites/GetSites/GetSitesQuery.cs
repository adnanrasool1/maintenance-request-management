using MediatR;

namespace Mra.Application.Sites.GetSites;

/// <summary>Lists the caller's organisation's sites, ordered by name (contract §3.5, FR-2.6).</summary>
public sealed record GetSitesQuery : IRequest<IReadOnlyList<SiteDto>>;
