using MediatR;

namespace Mra.Application.Requests.CompleteRequest;

/// <summary>Completes an approved request with its actual cost (contract §3.11, FR-3, FR-4.4, A-5).</summary>
public sealed record CompleteRequestCommand(Guid Id, decimal ActualCost) : IRequest<RequestDetail>;
