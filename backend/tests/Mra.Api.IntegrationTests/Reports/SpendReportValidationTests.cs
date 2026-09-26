using Mra.Application.Reports.SpendBySite;
using Xunit;

namespace Mra.Api.IntegrationTests.Reports;

// Contract §3.12: both dates required, strictly YYYY-MM-DD, from <= to; errors keyed on from/to.
public sealed class SpendReportValidationTests
{
    private readonly GetSpendBySiteValidator _validator = new();

    [Theory]
    [InlineData("2026-09-01", "2026-09-30")]
    [InlineData("2026-09-15", "2026-09-15")]
    public void Valid_range_passes(string from, string to)
    {
        Assert.True(_validator.Validate(new GetSpendBySiteQuery(from, to)).IsValid);
    }

    [Fact]
    public void From_after_to_fails_on_to()
    {
        var result = _validator.Validate(new GetSpendBySiteQuery("2026-09-02", "2026-09-01"));

        Assert.False(result.IsValid);
        Assert.Equal(["To"], result.Errors.Select(e => e.PropertyName));
    }

    [Theory]
    [InlineData(null, "2026-09-30", "From")]
    [InlineData("", "2026-09-30", "From")]
    [InlineData("2026-09-01", null, "To")]
    [InlineData("2026-9-1", "2026-09-30", "From")]
    [InlineData("09/01/2026", "2026-09-30", "From")]
    [InlineData("2026-09-01T00:00:00Z", "2026-09-30", "From")]
    [InlineData("2026-09-01", "2026-02-30", "To")]
    [InlineData("2026-09-01", "not-a-date", "To")]
    [InlineData("2026-09-01", "9999-12-31", "To")]
    public void Missing_or_malformed_date_fails_on_that_parameter_only(string? from, string? to, string property)
    {
        var result = _validator.Validate(new GetSpendBySiteQuery(from, to));

        Assert.False(result.IsValid);
        Assert.Equal([property], result.Errors.Select(e => e.PropertyName));
    }
}
