using FluentValidation;
using Mra.Application.Common.Validation;

namespace Mra.Application.Admin.SetThreshold;

public sealed class SetThresholdValidator : AbstractValidator<SetThresholdCommand>
{
    public SetThresholdValidator()
    {
        RuleFor(c => c.ApprovalThreshold).ValidThreshold();
    }
}
