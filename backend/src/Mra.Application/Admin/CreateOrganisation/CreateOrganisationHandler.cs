using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;
using Mra.Domain.Organisations;
using Mra.Domain.Users;

namespace Mra.Application.Admin.CreateOrganisation;

public sealed class CreateOrganisationHandler(IAppDbContext db, IPasswordHasher passwordHasher)
    : IRequestHandler<CreateOrganisationCommand, CreateOrganisationResponse>
{
    public async Task<CreateOrganisationResponse> Handle(CreateOrganisationCommand command, CancellationToken cancellationToken)
    {
        var organisation = Organisation.Create(command.Name.Trim(), command.ApprovalThreshold!.Value);
        var tenantAdmin = User.Create(
            organisation.Id,
            command.AdminEmail.Trim(),
            passwordHasher.Hash(command.AdminPassword),
            Role.TenantAdmin);

        db.Organisations.Add(organisation);
        db.Users.Add(tenantAdmin);

        // One save for both rows. The System Admin has no organisation, so the tenant guard allows
        // the insert. Email uniqueness is enforced by the unique index (contract D-2).
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateEmailException exception)
        {
            throw new ValidationException([new ValidationFailure(nameof(command.AdminEmail), exception.Message)]);
        }

        return new CreateOrganisationResponse(organisation.Name, organisation.ApprovalThreshold, tenantAdmin.ToDto());
    }
}
