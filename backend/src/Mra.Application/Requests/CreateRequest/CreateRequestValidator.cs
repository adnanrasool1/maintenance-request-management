using FluentValidation;
using Mra.Domain.Requests;

namespace Mra.Application.Requests.CreateRequest;

// Contract §3.6.
public sealed class CreateRequestValidator : AbstractValidator<CreateRequestCommand>
{
    private const decimal MaxCost = 1_000_000.00m;

    public CreateRequestValidator()
    {
        RuleFor(c => c.SiteId).NotEmpty();

        // NotEmpty also rejects whitespace-only strings.
        RuleFor(c => c.Description)
            .NotEmpty()
            .MaximumLength(MaintenanceRequest.MaxDescriptionLength);

        RuleFor(c => c.EstimatedCost)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxCost)
            .Must(HaveAtMostTwoDecimals).WithMessage("'{PropertyName}' must have at most 2 decimal places.");
    }

    private static bool HaveAtMostTwoDecimals(decimal value) => decimal.Round(value, 2) == value;
}
