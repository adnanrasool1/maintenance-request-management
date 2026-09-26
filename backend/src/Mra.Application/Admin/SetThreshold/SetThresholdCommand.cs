using MediatR;

namespace Mra.Application.Admin.SetThreshold;

/// <summary>Sets the caller's organisation's approval threshold (contract §3.4, FR-2.5).</summary>
public sealed record SetThresholdCommand(decimal? ApprovalThreshold) : IRequest<SetThresholdResponse>;

public sealed record SetThresholdResponse(decimal ApprovalThreshold);
