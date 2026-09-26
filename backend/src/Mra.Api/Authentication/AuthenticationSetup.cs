using Microsoft.AspNetCore.Authentication.JwtBearer;
using Mra.Infrastructure.Authentication;

namespace Mra.Api.Authentication;

public static class AuthenticationSetup
{
    /// <summary>
    /// JWT bearer validation of issuer, audience, lifetime and signing key. Uses the
    /// <see cref="JwtSettings"/> registered (and checked at startup) by AddInfrastructure.
    /// </summary>
    public static IServiceCollection AddJwtBearerAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSettings>((options, settings) =>
            {
                // Keep "sub", "org" and "role" as issued instead of mapping them to the long
                // WS-* claim URIs, so CurrentUser and the role policies read the same names.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = settings.CreateValidationParameters();
            });

        return services;
    }
}
