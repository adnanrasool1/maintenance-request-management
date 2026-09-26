using MediatR;

namespace Mra.Application.Reports.SpendBySite;

/// <summary>
/// Spend per site for an inclusive UTC date range (FR-6, contract §3.12). The dates arrive as the
/// raw query-string values so the validator reports a missing or malformed date as a 400 keyed on
/// <c>from</c> / <c>to</c> (contract §4.1), rather than leaving it to minimal-API binding.
/// </summary>
public sealed record GetSpendBySiteQuery(string? From, string? To) : IRequest<SpendReportDto>;
