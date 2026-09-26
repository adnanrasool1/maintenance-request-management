using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Mra.Application.Common.Abstractions;

namespace Mra.Infrastructure.Authentication;

/// <summary>Issues HS256 access tokens with a 60-minute lifetime (architecture §9).</summary>
public sealed class JwtTokenService(JwtSettings settings, TimeProvider timeProvider) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials = new(settings.SigningKey, SecurityAlgorithms.HmacSha256);

    public AccessToken CreateToken(Guid userId, Guid? organisationId, string role, string email)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.Add(JwtSettings.TokenLifetime);

        var claims = new Dictionary<string, object>
        {
            [JwtClaimNames.Subject] = userId.ToString(),
            [JwtClaimNames.Role] = role,
            [JwtClaimNames.Email] = email,
        };

        // The System Admin has no organisation, so the claim is left out rather than empty.
        if (organisationId is { } org)
        {
            claims[JwtClaimNames.Organisation] = org.ToString();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = _credentials,
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
