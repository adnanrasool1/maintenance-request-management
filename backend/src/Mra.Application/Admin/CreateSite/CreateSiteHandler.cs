using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;
using Mra.Application.Sites;
using Mra.Domain.Sites;

namespace Mra.Application.Admin.CreateSite;

public sealed class CreateSiteHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateSiteCommand, SiteDto>
{
    public const string DuplicateNameMessage = "A site with this name already exists.";

    public async Task<SiteDto> Handle(CreateSiteCommand command, CancellationToken cancellationToken)
    {
        // The TenantAdmin policy guarantees an organisation claim; the organisation comes only from it.
        var organisationId = currentUser.OrganisationId ?? throw new ForbiddenException();
        var name = command.Name.Trim();

        // Filtered set: only the caller's organisation's sites. The comparison is case-insensitive
        // through the database's case-insensitive collation, the same one the unique index uses.
        if (await db.Sites.AnyAsync(s => s.Name == name, cancellationToken))
        {
            throw DuplicateName();
        }

        var site = Site.Create(organisationId, name);
        db.Sites.Add(site);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateSiteNameException)
        {
            // Another request created the same name between the check and the save.
            throw DuplicateName();
        }

        return site.ToDto();
    }

    private static ValidationException DuplicateName() =>
        new([new ValidationFailure(nameof(CreateSiteCommand.Name), DuplicateNameMessage)]);
}
