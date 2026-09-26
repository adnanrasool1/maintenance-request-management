using System.Text.Json.Serialization;
using Mra.Domain.Requests;

namespace Mra.Application.Requests;

/// <summary>A list item for <c>GET /api/requests</c> (contract §3.13).</summary>
public sealed record RequestSummary(
    Guid Id,
    Guid SiteId,
    string SiteName,
    string Description,
    decimal EstimatedCost,
    [property: JsonConverter(typeof(JsonStringEnumConverter<RequestStatus>))] RequestStatus Status,
    Guid RaisedByUserId,
    DateTime CreatedAt);
