using System.Text.Json.Serialization;
using Mra.Domain.Requests;

namespace Mra.Application.Requests;

/// <summary>
/// One request (contract §3.13): the result of get-by-id, create, approve, reject and complete.
/// The status is serialised as its name, as the contract requires.
/// </summary>
public sealed record RequestDetail(
    Guid Id,
    Guid SiteId,
    string SiteName,
    Guid RaisedByUserId,
    string RaisedByEmail,
    string Description,
    decimal EstimatedCost,
    decimal? ActualCost,
    [property: JsonConverter(typeof(JsonStringEnumConverter<RequestStatus>))] RequestStatus Status,
    decimal? ThresholdAtDecision,
    bool ExceededThreshold,
    DateTime CreatedAt,
    DateTime? CompletedAt);
