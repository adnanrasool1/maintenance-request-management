using FluentValidation;
using Mra.Application.Common.Validation;
using Mra.Domain.Users;

namespace Mra.Application.Admin.CreateUser;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.Password).ValidPassword();
        RuleFor(c => c.Role)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(role => TryParseRole(role, out _))
            .WithMessage("'Role' must be Requester or Approver.");
    }

    // Only the two roles a Tenant Admin may create. Compared by name, so numeric strings such as
    // "3" (which Enum.TryParse would accept) are rejected.
    public static bool TryParseRole(string value, out Role role)
    {
        foreach (var allowed in (Role[])[Role.Requester, Role.Approver])
        {
            if (string.Equals(value, allowed.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                role = allowed;
                return true;
            }
        }

        role = default;
        return false;
    }
}
