using FluentValidation;

namespace Mra.Application.Reports.SpendBySite;

// Contract §3.12: both dates required, YYYY-MM-DD, from <= to. Error keys are "from" and "to".
public sealed class GetSpendBySiteValidator : AbstractValidator<GetSpendBySiteQuery>
{
    public GetSpendBySiteValidator()
    {
        RuleFor(q => q.From)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("From is required.")
            .Must(BeADate).WithMessage("From must be a date in the format YYYY-MM-DD.");

        RuleFor(q => q.To)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("To is required.")
            .Must(BeADate).WithMessage("To must be a date in the format YYYY-MM-DD.")
            // The range ends at the start of the day after "to", so that day must exist.
            .Must(value => ReportDate.Parse(value!) < DateOnly.MaxValue).WithMessage("To is out of range.");

        RuleFor(q => q)
            .Must(q => ReportDate.Parse(q.From!) <= ReportDate.Parse(q.To!))
            .When(q => BeADate(q.From) && BeADate(q.To))
            .OverridePropertyName(nameof(GetSpendBySiteQuery.To))
            .WithMessage("To must be on or after From.");
    }

    private static bool BeADate(string? value) => ReportDate.TryParse(value, out _);
}
