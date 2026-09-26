using FluentValidation;
using Mra.Application.Common.Validation;

namespace Mra.Application.Admin.CreateSite;

public sealed class CreateSiteValidator : AbstractValidator<CreateSiteCommand>
{
    public CreateSiteValidator()
    {
        RuleFor(c => c.Name).TrimmedName();
    }
}
