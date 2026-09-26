using MediatR;

namespace Mra.Application.Requests.RejectRequest;

/// <summary>Rejects a pending request (contract §3.10, FR-3, FR-3.2, FR-3.4).</summary>
public sealed record RejectRequestCommand(Guid Id, string? Comment) : IRequest<RequestDetail>;
