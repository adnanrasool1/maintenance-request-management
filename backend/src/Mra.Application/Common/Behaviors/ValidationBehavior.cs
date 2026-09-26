using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Mra.Application.Common.Behaviors;

/// <summary>
/// Runs every FluentValidation validator registered for the request before the handler.
/// Throws <see cref="ValidationException"/> (mapped to 400 by the API) when any rule fails.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            // A fresh context per validator: a ValidationContext accumulates failures, so sharing
            // one would repeat the earlier validators' errors in every later result.
            var result = await validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
