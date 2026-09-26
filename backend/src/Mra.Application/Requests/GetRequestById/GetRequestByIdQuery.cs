using MediatR;

namespace Mra.Application.Requests.GetRequestById;

/// <summary>Returns one request visible to the caller (contract §3.8, FR-5.1–FR-5.3).</summary>
public sealed record GetRequestByIdQuery(Guid Id) : IRequest<RequestDetail>;
