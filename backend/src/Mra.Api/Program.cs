using Mra.Api.Errors;
using Mra.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

app.Run();
