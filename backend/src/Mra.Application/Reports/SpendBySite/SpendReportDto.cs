namespace Mra.Application.Reports.SpendBySite;

// Contract §3.12. DateOnly serialises as "YYYY-MM-DD".
public sealed record SpendReportDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<SpendRowDto> Rows,
    decimal GrandTotal);

public sealed record SpendRowDto(Guid SiteId, string SiteName, decimal TotalSpend);
