using FluentValidation;

namespace Mra.Application.Auth.Login;

// Presence only (contract §2.2); format and strength rules apply when accounts are created.
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().WithMessage("Email is required.");
        RuleFor(c => c.Password).NotEmpty().WithMessage("Password is required.");
    }
}
