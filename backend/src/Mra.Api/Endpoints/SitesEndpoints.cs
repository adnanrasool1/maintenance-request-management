using MediatR;
using Mra.Api.Authorization;
using Mra.Application.Sites.GetSites;

namespace Mra.Api.Endpoints;

/// <summary>Site list for the create-request picker (contract §3.5, FR-2.6).</summary>
public static class SitesEndpoints
{
    public static IEndpointRouteBuilder MapSitesEndpoints(this IEndpointRouteBuilder app)
    {
        var sites = app.MapGroup("/api/sites").RequireAuthorization(Policies.OrgMember);

        sites.MapGet("", async (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetSitesQuery(), ct)));

        return app;
    }
}
