using Mra.Api.Authentication;
using Mra.Api.Authorization;
using Mra.Api.Endpoints;
using Mra.Api.Errors;
using Mra.Api.OpenApi;
using Mra.Application;
using Mra.Infrastructure;
using Mra.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
// Throws at startup when JWT_SIGNING_KEY, JWT_ISSUER or JWT_AUDIENCE is missing or the key is weak.
builder.Services.AddInfrastructure(builder.Configuration);
// ConnectionStrings__App: the API's SQL Server connection as the restricted mra_app login (architecture §8),
// composed in docker-compose. No default: startup fails when it is missing. Migrations are the DbMigrator's job.
builder.Services.AddPersistence(builder.Configuration.GetConnectionString("App")
    ?? throw new InvalidOperationException("Configuration value ConnectionStrings__App is missing. docker-compose composes it from infra/.env."));

builder.Services.AddJwtBearerAuthentication();
builder.Services.AddAuthorizationPolicies();

builder.Services.AddOpenApiDocument();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApiInDevelopment();
app.MapAuthEndpoints();
app.MapReportsEndpoints();

app.Run();
