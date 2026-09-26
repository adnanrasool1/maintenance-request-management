using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;
using Mra.Domain.Users;

namespace Mra.Application.Admin.CreateUser;

public sealed class CreateUserHandler(IAppDbContext db, ICurrentUser currentUser, IPasswordHasher passwordHasher)
    : IRequestHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        // The TenantAdmin policy guarantees an organisation claim; the organisation comes only from it.
        var organisationId = currentUser.OrganisationId ?? throw new ForbiddenException();

        if (!CreateUserValidator.TryParseRole(command.Role, out var role))
        {
            // Unreachable after ValidationBehavior; kept so the handler never creates another role.
            throw new ValidationException([new ValidationFailure(nameof(command.Role), "'Role' must be Requester or Approver.")]);
        }

        var user = User.Create(organisationId, command.Email.Trim(), passwordHasher.Hash(command.Password), role);
        db.Users.Add(user);

        // Emails are unique system-wide, but this handler can't see other organisations' users
        // (no IgnoreQueryFilters outside login and System Admin), so the unique index is the check (D-2).
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateEmailException exception)
        {
            throw new ValidationException([new ValidationFailure(nameof(command.Email), exception.Message)]);
        }

        return user.ToDto();
    }
}
