using Mra.Api.Authentication;
using Mra.Api.Errors;
using Mra.Application;
using Mra.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
// Throws at startup when JWT_SIGNING_KEY, JWT_ISSUER or JWT_AUDIENCE is missing or the key is weak.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddJwtBearerAuthentication();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();

app.Run();
