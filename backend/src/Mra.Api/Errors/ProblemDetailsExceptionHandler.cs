using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Exceptions;
using Mra.Domain.Requests;
using Mra.Infrastructure.Persistence;

namespace Mra.Api.Errors;

/// <summary>
/// Turns exceptions into ProblemDetails (RFC 9457) as in contract §4 / architecture §4.4.
/// Handlers only throw; this is the single place that builds error responses.
/// </summary>
public sealed class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public const string NotFoundDetail = "The requested resource was not found.";
    public const string ConflictDetail = "The resource was changed by someone else. Reload it and try again.";
    public const string BadRequestDetail = "The request could not be read.";
    public const string InvalidCredentialsDetail = "Invalid email or password.";
    public const string SelfApprovalDetail = "You cannot approve or reject a request you raised.";
    public const string InvalidTransitionDetail = "The request is not in a state that allows this action.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => CreateValidationProblem(validation),
            // Same body for an unknown email and a wrong password (contract §4.3).
            InvalidCredentialsException => new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Detail = InvalidCredentialsDetail },
            NotFoundException => new ProblemDetails { Status = StatusCodes.Status404NotFound, Detail = NotFoundDetail },
            ForbiddenException forbidden => new ProblemDetails { Status = StatusCodes.Status403Forbidden, Detail = forbidden.Message },
            SelfApprovalException => new ProblemDetails { Status = StatusCodes.Status403Forbidden, Detail = SelfApprovalDetail },
            // Fixed detail: the exception message names the statuses, which the contract doesn't expose.
            InvalidTransitionException => new ProblemDetails { Status = StatusCodes.Status409Conflict, Detail = InvalidTransitionDetail },
            DbUpdateConcurrencyException => new ProblemDetails { Status = StatusCodes.Status409Conflict, Detail = ConflictDetail },
            // Malformed JSON or an unbindable parameter (thrown in Development; ThrowOnBadRequest).
            BadHttpRequestException badRequest => new ProblemDetails { Status = badRequest.StatusCode, Detail = BadRequestDetail },
            // A tenant-isolation or audit guard fired: always an application bug, never bad input.
            TenantGuardException => null,
            // Anything else: a generic 500. The exception is logged, never returned to the client.
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
            problem = new ProblemDetails { Status = StatusCodes.Status500InternalServerError };
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            // Not passing the exception keeps it out of any customisation that could echo it.
        });

        // The default writer declines when the Accept header excludes JSON. Returning false would
        // let the middleware reset the response to a 500, so write the problem directly instead.
        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(
                problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken);
        }

        return true;
    }

    private static HttpValidationProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(failure => ToCamelCasePath(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

        return new HttpValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest };
    }

    // "EstimatedCost" -> "estimatedCost"; "Items[0].Name" -> "items[0].name".
    private static string ToCamelCasePath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
