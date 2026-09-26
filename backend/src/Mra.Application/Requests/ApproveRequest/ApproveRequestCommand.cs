using MediatR;

namespace Mra.Application.Requests.ApproveRequest;

/// <summary>Approves a pending request (contract §3.9, FR-3, FR-3.4, FR-4.2).</summary>
public sealed record ApproveRequestCommand(Guid Id, string? Comment) : IRequest<RequestDetail>;
