using MediatR;

namespace Mra.Application.Requests.CreateRequest;

/// <summary>Raises a maintenance request (contract §3.6, FR-3, FR-4.1).</summary>
public sealed record CreateRequestCommand(Guid SiteId, string Description, decimal EstimatedCost) : IRequest<RequestDetail>;
