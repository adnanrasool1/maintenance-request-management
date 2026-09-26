using System.Globalization;

namespace Mra.Application.Reports.SpendBySite;

// Report dates are calendar dates written exactly as YYYY-MM-DD (contract §1).
internal static class ReportDate
{
    private const string Format = "yyyy-MM-dd";

    public static bool TryParse(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static DateOnly Parse(string value) =>
        DateOnly.ParseExact(value, Format, CultureInfo.InvariantCulture);
}
