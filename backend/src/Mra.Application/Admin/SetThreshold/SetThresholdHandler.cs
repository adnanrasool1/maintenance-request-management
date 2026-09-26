using MediatR;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;

namespace Mra.Application.Admin.SetThreshold;

public sealed class SetThresholdHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<SetThresholdCommand, SetThresholdResponse>
{
    public async Task<SetThresholdResponse> Handle(SetThresholdCommand command, CancellationToken cancellationToken)
    {
        // Through the filtered set, so only the caller's own organisation can be found.
        var organisation = await db.Organisations
            .SingleOrDefaultAsync(o => o.Id == currentUser.OrganisationId, cancellationToken)
            ?? throw new NotFoundException();

        // Existing requests are not touched: pending ones stay pending (FR-4.3).
        organisation.SetApprovalThreshold(command.ApprovalThreshold!.Value);

        await db.SaveChangesAsync(cancellationToken);

        return new SetThresholdResponse(organisation.ApprovalThreshold);
    }
}
