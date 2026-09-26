using FluentValidation;
using Mra.Domain.Requests;

namespace Mra.Application.Requests.ApproveRequest;

// Contract §3.9, D-5: the comment is optional and at most 2,000 characters.
public sealed class ApproveRequestValidator : AbstractValidator<ApproveRequestCommand>
{
    public ApproveRequestValidator()
    {
        RuleFor(c => c.Comment).MaximumLength(MaintenanceRequest.MaxCommentLength);
    }
}
