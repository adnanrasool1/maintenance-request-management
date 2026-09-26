using Mra.Api.Authentication;
using Mra.Api.Authorization;
using Mra.Api.Endpoints;
using Mra.Api.Errors;
using Mra.Api.OpenApi;
using Mra.Application;
using Mra.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
// Throws at startup when JWT_SIGNING_KEY, JWT_ISSUER or JWT_AUDIENCE is missing or the key is weak.
builder.Services.AddInfrastructure(builder.Configuration);

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
app.MapRequestsEndpoints();

app.Run();
