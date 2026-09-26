using MediatR;
using Mra.Api.Authorization;
using Mra.Application.Reports.SpendBySite;

namespace Mra.Api.Endpoints;

public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        // Approvers and Tenant Admins only; Requesters get 403 (FR-6.1, A-7).
        var group = app.MapGroup("/api/reports")
            .RequireAuthorization(Policies.ApproverOrTenantAdmin);

        // from/to bind as strings; GetSpendBySiteValidator turns a missing or malformed date into
        // a 400 ValidationProblem keyed on "from"/"to" (contract §4.1).
        group.MapGet("/spend", async (string? from, string? to, ISender sender, CancellationToken cancellationToken) =>
            TypedResults.Ok(await sender.Send(new GetSpendBySiteQuery(from, to), cancellationToken)));

        return app;
    }
}
