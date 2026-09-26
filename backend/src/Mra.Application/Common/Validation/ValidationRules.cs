using System.Net.Mail;
using FluentValidation;

namespace Mra.Application.Common.Validation;

/// <summary>Shared FluentValidation rules for the limits in contract §1 and §6 (D-4, D-5).</summary>
public static class ValidationRules
{
    /// <summary>The cost cap, also the threshold cap (contract D-4).</summary>
    public const decimal MaxMoney = 1_000_000.00m;

    public const int MaxEmailLength = 256;
    public const int MaxNameLength = 200;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    /// <summary>Exact check: true when <paramref name="value"/> has no digits beyond the second decimal place.</summary>
    public static bool HasAtMostTwoDecimalPlaces(decimal value) => decimal.Round(value, 2) == value;

    /// <summary>Required, not blank, and 1–<paramref name="maxLength"/> characters after trimming.</summary>
    public static IRuleBuilderOptions<T, string> TrimmedName<T>(this IRuleBuilderInitial<T, string> rule, int maxLength = MaxNameLength) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => value.Trim().Length <= maxLength)
            .WithMessage($"'{{PropertyName}}' must be at most {maxLength} characters.");

    /// <summary>Required, a valid address, and at most 256 characters after trimming (contract §1).</summary>
    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => value.Trim().Length <= MaxEmailLength)
            .WithMessage($"'{{PropertyName}}' must be at most {MaxEmailLength} characters.")
            .Must(value => IsEmailAddress(value.Trim()))
            .WithMessage("'{PropertyName}' is not a valid email address.");

    /// <summary>Required, 8–128 characters (contract D-5).</summary>
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Length(MinPasswordLength, MaxPasswordLength);

    /// <summary>Required, 0 to 1,000,000.00 inclusive, at most 2 decimal places (contract D-4).</summary>
    public static IRuleBuilderOptions<T, decimal?> ValidThreshold<T>(this IRuleBuilderInitial<T, decimal?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull()
            .InclusiveBetween(0m, MaxMoney)
            .Must(value => HasAtMostTwoDecimalPlaces(value!.Value))
            .WithMessage("'{PropertyName}' must have at most 2 decimal places.");

    // MailAddress also accepts display-name forms ("Bob <bob@x>"), so require the parsed address
    // to be the whole input.
    private static bool IsEmailAddress(string value) =>
        MailAddress.TryCreate(value, out var address) && address.Address == value;
}
