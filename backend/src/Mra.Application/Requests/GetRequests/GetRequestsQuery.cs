using MediatR;
using Mra.Domain.Requests;

namespace Mra.Application.Requests.GetRequests;

/// <summary>
/// Lists the requests visible to the caller, newest first (contract §3.7, FR-5.1, FR-5.2).
/// <paramref name="Status"/> is an optional status name, matched case-insensitively.
/// </summary>
public sealed record GetRequestsQuery(string? Status) : IRequest<IReadOnlyList<RequestSummary>>
{
    // Names only, ignoring case. Enum.TryParse would also accept numbers such as "3" or "99".
    internal static RequestStatus? ParseStatus(string? value) =>
        Enum.GetValues<RequestStatus>()
            .Select(status => (RequestStatus?)status)
            .FirstOrDefault(status => string.Equals(status.ToString(), value, StringComparison.OrdinalIgnoreCase));
}
