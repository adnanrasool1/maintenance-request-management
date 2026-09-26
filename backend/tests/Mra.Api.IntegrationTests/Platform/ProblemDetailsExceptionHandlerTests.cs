using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mra.Api.Errors;
using Mra.Application.Common.Exceptions;
using Mra.Domain.Requests;
using Mra.Infrastructure.Persistence;
using Xunit;

namespace Mra.Api.IntegrationTests.Platform;

// Exercises the handler directly with a real ProblemDetails service; no test server needed.
public sealed class ProblemDetailsExceptionHandlerTests
{
    [Fact]
    public async Task Validation_exception_returns_400_with_camelCase_error_keys()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("EstimatedCost", "Estimated cost must be greater than 0."),
            new ValidationFailure("Description", "Description is required."),
            new ValidationFailure("Description", "Description is too long."),
        ]);

        var (status, contentType, body) = await HandleAsync(exception);

        Assert.Equal(400, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.Equal("One or more validation errors occurred.", body.GetProperty("title").GetString());
        var errors = body.GetProperty("errors");
        Assert.Equal(2, errors.EnumerateObject().Count());
        Assert.Equal(1, errors.GetProperty("estimatedCost").GetArrayLength());
        Assert.Equal(2, errors.GetProperty("description").GetArrayLength());
    }

    [Fact]
    public async Task Not_found_returns_404_with_fixed_detail_whatever_the_message()
    {
        var (status, _, body) = await HandleAsync(new NotFoundException("Site 123 belongs to organisation 456"));

        Assert.Equal(404, status);
        Assert.Equal("The requested resource was not found.", body.GetProperty("detail").GetString());
        Assert.DoesNotContain("456", body.GetRawText());
    }

    [Fact]
    public async Task Forbidden_returns_403()
    {
        var (status, _, body) = await HandleAsync(new ForbiddenException("Not your request."));

        Assert.Equal(403, status);
        Assert.Equal("Not your request.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Concurrency_conflict_returns_409()
    {
        var (status, _, _) = await HandleAsync(new DbUpdateConcurrencyException("row version mismatch"));

        Assert.Equal(409, status);
    }

    [Fact]
    public async Task Invalid_transition_returns_409_with_fixed_detail()
    {
        var (status, _, body) = await HandleAsync(
            new InvalidTransitionException(RequestStatus.Completed, RequestStatus.Approved));

        Assert.Equal(409, status);
        Assert.Equal("The request is not in a state that allows this action.", body.GetProperty("detail").GetString());
        Assert.Equal("Conflict", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Self_approval_returns_403_with_contract_detail()
    {
        var (status, _, body) = await HandleAsync(new SelfApprovalException());

        Assert.Equal(403, status);
        Assert.Equal("You cannot approve or reject a request you raised.", body.GetProperty("detail").GetString());
        Assert.Equal("Forbidden", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Invalid_credentials_returns_401_with_contract_body()
    {
        var (status, contentType, body) = await HandleAsync(new InvalidCredentialsException());

        Assert.Equal(401, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.Equal("Invalid email or password.", body.GetProperty("detail").GetString());
        Assert.Equal("Unauthorized", body.GetProperty("title").GetString());
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.2", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Tenant_guard_violation_returns_500_without_detail()
    {
        var (status, _, body) = await HandleAsync(new TenantGuardException("Cross-tenant write to Site 123"));

        Assert.Equal(500, status);
        Assert.False(body.TryGetProperty("detail", out _));
        Assert.DoesNotContain("123", body.GetRawText());
    }

    [Fact]
    public async Task Unexpected_exception_returns_500_without_leaking_details()
    {
        var (status, _, body) = await HandleAsync(
            new InvalidOperationException("Connection string Server=db;Password=secret failed"));

        Assert.Equal(500, status);
        var raw = body.GetRawText();
        Assert.DoesNotContain("secret", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        Assert.False(body.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task Status_is_kept_when_the_client_does_not_accept_json()
    {
        var (status, contentType, body) = await HandleAsync(new NotFoundException(), accept: "text/html");

        Assert.Equal(404, status);
        Assert.StartsWith("application/problem+json", contentType);
        Assert.Equal("The requested resource was not found.", body.GetProperty("detail").GetString());
    }

    private static async Task<(int Status, string? ContentType, JsonElement Body)> HandleAsync(
        Exception exception, string? accept = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Response.Body = new MemoryStream();
        if (accept is not null)
        {
            httpContext.Request.Headers.Accept = accept;
        }

        var handler = new ProblemDetailsExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            NullLogger<ProblemDetailsExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);
        Assert.True(handled);

        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        return (httpContext.Response.StatusCode, httpContext.Response.ContentType, document.RootElement.Clone());
    }
}
