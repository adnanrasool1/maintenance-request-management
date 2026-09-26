using MediatR;
using Mra.Api.Authorization;
using Mra.Application.Admin.CreateOrganisation;
using Mra.Application.Admin.CreateSite;
using Mra.Application.Admin.CreateUser;
using Mra.Application.Admin.SetThreshold;

namespace Mra.Api.Endpoints;

/// <summary>Tenant administration (contract §3.1–§3.4, FR-2). Thin: send the request, return the result.</summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var systemAdmin = app.MapGroup("/api/admin").RequireAuthorization(Policies.SystemAdmin);

        // 201 without a Location header: there is no route to read an organisation, user or site.
        systemAdmin.MapPost("/organisations", async (CreateOrganisationCommand command, ISender sender, CancellationToken ct) =>
            TypedResults.Created((string?)null, await sender.Send(command, ct)));

        var tenantAdmin = app.MapGroup("/api/org").RequireAuthorization(Policies.TenantAdmin);

        tenantAdmin.MapPost("/users", async (CreateUserCommand command, ISender sender, CancellationToken ct) =>
            TypedResults.Created((string?)null, await sender.Send(command, ct)));

        tenantAdmin.MapPost("/sites", async (CreateSiteCommand command, ISender sender, CancellationToken ct) =>
            TypedResults.Created((string?)null, await sender.Send(command, ct)));

        tenantAdmin.MapPut("/threshold", async (SetThresholdCommand command, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(command, ct)));

        return app;
    }
}
