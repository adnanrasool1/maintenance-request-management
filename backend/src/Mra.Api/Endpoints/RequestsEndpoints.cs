using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mra.Api.Authorization;
using Mra.Application.Requests.ApproveRequest;
using Mra.Application.Requests.CompleteRequest;
using Mra.Application.Requests.CreateRequest;
using Mra.Application.Requests.GetRequestById;
using Mra.Application.Requests.GetRequests;
using Mra.Application.Requests.RejectRequest;

namespace Mra.Api.Endpoints;

/// <summary>Request workflow routes (contract §3.6–§3.11). Thin: each one sends a MediatR request.</summary>
public static class RequestsEndpoints
{
    /// <summary>Body of approve and reject (contract §3.9, §3.10).</summary>
    public sealed record DecisionBody(string? Comment);

    /// <summary>Body of complete (contract §3.11).</summary>
    public sealed record CompleteBody(decimal ActualCost);

    public static IEndpointRouteBuilder MapRequestsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/requests").WithTags("Requests");

        group.MapPost("/", async (CreateRequestCommand command, ISender sender, CancellationToken ct) =>
            {
                var request = await sender.Send(command, ct);
                return TypedResults.Created($"/api/requests/{request.Id}", request);
            })
            .RequireAuthorization(Policies.Requester);

        group.MapGet("/", async ([FromQuery] string? status, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetRequestsQuery(status), ct)))
            .RequireAuthorization(Policies.Requester);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetRequestByIdQuery(id), ct)))
            .RequireAuthorization(Policies.Requester);

        group.MapPost("/{id:guid}/approve", async (Guid id, DecisionBody body, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new ApproveRequestCommand(id, body.Comment), ct)))
            .RequireAuthorization(Policies.Approver);

        group.MapPost("/{id:guid}/reject", async (Guid id, DecisionBody body, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new RejectRequestCommand(id, body.Comment), ct)))
            .RequireAuthorization(Policies.Approver);

        group.MapPost("/{id:guid}/complete", async (Guid id, CompleteBody body, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new CompleteRequestCommand(id, body.ActualCost), ct)))
            .RequireAuthorization(Policies.Requester);

        return app;
    }
}
