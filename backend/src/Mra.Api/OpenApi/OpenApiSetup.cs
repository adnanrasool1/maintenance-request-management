using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Mra.Api.OpenApi;

/// <summary>OpenAPI document and Scalar UI. Mapped in Development only (architecture §4.4).</summary>
public static class OpenApiSetup
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddOpenApiDocument(this IServiceCollection services) =>
        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            // Lets Scalar send "Authorization: Bearer <token>" once the user pastes a login token.
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
            };
            document.Security =
            [
                new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [] },
            ];
            return Task.CompletedTask;
        }));

    public static WebApplication MapOpenApiInDevelopment(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            // The only anonymous endpoints besides login (stakeholder-approved exception, DECISIONS):
            // the docs themselves must load in a browser. Every API call made from Scalar still
            // needs a bearer token. Never mapped outside Development.
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        return app;
    }
}
