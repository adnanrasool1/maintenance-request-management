using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Mra.Application.Common.Abstractions;

namespace Mra.Infrastructure.Persistence;

// Used only by `dotnet ef` to build the model for migrations. The connection string is a
// placeholder: adding a migration or scripting it never connects to a database.
internal sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DesignTimeConnectionString = "Server=.;Database=design-time;Trusted_Connection=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(DesignTimeConnectionString)
            .Options;

        return new AppDbContext(options, new NoCurrentUser());
    }

    private sealed class NoCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;

        public Guid? OrganisationId => null;

        public string? Role => null;

        public bool IsAuthenticated => false;
    }
}
