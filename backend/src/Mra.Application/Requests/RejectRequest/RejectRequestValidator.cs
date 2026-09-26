using FluentValidation;
using Mra.Domain.Requests;

namespace Mra.Application.Requests.RejectRequest;

// Contract §3.10, D-5: the comment is optional and at most 2,000 characters.
public sealed class RejectRequestValidator : AbstractValidator<RejectRequestCommand>
{
    public RejectRequestValidator()
    {
        RuleFor(c => c.Comment).MaximumLength(MaintenanceRequest.MaxCommentLength);
    }
}
