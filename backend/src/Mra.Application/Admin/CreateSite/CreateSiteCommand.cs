using MediatR;
using Mra.Application.Sites;

namespace Mra.Application.Admin.CreateSite;

/// <summary>Creates a site in the caller's organisation (contract §3.3, FR-2.4).</summary>
public sealed record CreateSiteCommand(string Name) : IRequest<SiteDto>;
