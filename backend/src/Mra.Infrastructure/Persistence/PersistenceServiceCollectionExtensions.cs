using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mra.Application.Common.Abstractions;

namespace Mra.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    // Registers the context only. Migrations are applied by Mra.DbMigrator, never by the API.
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        return services;
    }
}
