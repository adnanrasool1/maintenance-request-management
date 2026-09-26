using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Exceptions;

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

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // TODO(T1.7): map the domain exceptions when they land in Mra.Domain:
        //   InvalidTransitionException -> 409 Conflict
        //   SelfApprovalException      -> 403 Forbidden
        var problem = exception switch
        {
            ValidationException validation => CreateValidationProblem(validation),
            NotFoundException => new ProblemDetails { Status = StatusCodes.Status404NotFound, Detail = NotFoundDetail },
            ForbiddenException forbidden => new ProblemDetails { Status = StatusCodes.Status403Forbidden, Detail = forbidden.Message },
            DbUpdateConcurrencyException => new ProblemDetails { Status = StatusCodes.Status409Conflict, Detail = ConflictDetail },
            // Malformed JSON or an unbindable parameter (thrown in Development; ThrowOnBadRequest).
            BadHttpRequestException badRequest => new ProblemDetails { Status = badRequest.StatusCode, Detail = BadRequestDetail },
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

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            // Not passing the exception keeps it out of any customisation that could echo it.
        });
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
