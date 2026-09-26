using FluentValidation;

namespace Mra.Application.Requests.CompleteRequest;

// Contract §3.11.
public sealed class CompleteRequestValidator : AbstractValidator<CompleteRequestCommand>
{
    private const decimal MaxCost = 1_000_000.00m;

    public CompleteRequestValidator()
    {
        RuleFor(c => c.ActualCost)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxCost)
            .Must(HaveAtMostTwoDecimals).WithMessage("'{PropertyName}' must have at most 2 decimal places.");
    }

    private static bool HaveAtMostTwoDecimals(decimal value) => decimal.Round(value, 2) == value;
}
