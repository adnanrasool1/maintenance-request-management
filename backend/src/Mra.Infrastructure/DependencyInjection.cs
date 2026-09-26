using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mra.Application.Common.Abstractions;
using Mra.Infrastructure.Authentication;

namespace Mra.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the infrastructure services. Reads the JWT settings immediately, so a missing or
    /// weak <c>JWT_SIGNING_KEY</c> fails startup instead of the first request.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(JwtSettings.FromConfiguration(configuration));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}
