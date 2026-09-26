using MediatR;
using Mra.Application.Auth.Login;

namespace Mra.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // The only anonymous API endpoint (contract §2.2): the caller has no token yet.
        group.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(command, cancellationToken)))
            .AllowAnonymous();

        return app;
    }
}
