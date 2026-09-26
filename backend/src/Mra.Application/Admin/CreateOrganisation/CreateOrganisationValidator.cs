using FluentValidation;
using Mra.Application.Common.Validation;

namespace Mra.Application.Admin.CreateOrganisation;

public sealed class CreateOrganisationValidator : AbstractValidator<CreateOrganisationCommand>
{
    public CreateOrganisationValidator()
    {
        RuleFor(c => c.Name).TrimmedName();
        RuleFor(c => c.ApprovalThreshold).ValidThreshold();
        RuleFor(c => c.AdminEmail).ValidEmail();
        RuleFor(c => c.AdminPassword).ValidPassword();
    }
}
