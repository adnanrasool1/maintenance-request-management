using Microsoft.EntityFrameworkCore;
using Mra.Domain.Organisations;
using Mra.Domain.Requests;
using Mra.Domain.Sites;
using Mra.Domain.Users;

namespace Mra.Application.Common.Abstractions;

// Handlers use EF Core directly through this interface; there is no repository layer (architecture §4.2).
// Every set is tenant-filtered by the implementation (architecture §7).
public interface IAppDbContext
{
    DbSet<Organisation> Organisations { get; }

    DbSet<Site> Sites { get; }

    DbSet<User> Users { get; }

    DbSet<MaintenanceRequest> MaintenanceRequests { get; }

    DbSet<AuditEntry> AuditEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
