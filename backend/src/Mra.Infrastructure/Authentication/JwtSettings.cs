using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Mra.Infrastructure.Authentication;

/// <summary>
/// JWT configuration from the environment (<c>JWT_SIGNING_KEY</c>, <c>JWT_ISSUER</c>,
/// <c>JWT_AUDIENCE</c>; see infra/.env.example). There are no defaults: a missing or weak key
/// stops the application at startup (architecture §9).
/// </summary>
public sealed class JwtSettings
{
    public const string SigningKeyKey = "JWT_SIGNING_KEY";
    public const string IssuerKey = "JWT_ISSUER";
    public const string AudienceKey = "JWT_AUDIENCE";

    /// <summary>HS256 needs a key of at least 256 bits.</summary>
    public const int MinimumKeyBytes = 32;

    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(60);

    /// <summary>Tolerance for clock differences when checking <c>exp</c> and <c>nbf</c>.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private JwtSettings(SymmetricSecurityKey signingKey, string issuer, string audience)
    {
        SigningKey = signingKey;
        Issuer = issuer;
        Audience = audience;
    }

    public SymmetricSecurityKey SigningKey { get; }

    public string Issuer { get; }

    public string Audience { get; }

    /// <summary>Reads and checks the settings. Throws <see cref="InvalidOperationException"/> when invalid.</summary>
    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var encodedKey = Required(configuration, SigningKeyKey);
        var issuer = Required(configuration, IssuerKey);
        var audience = Required(configuration, AudienceKey);

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                $"{SigningKeyKey} must be base64-encoded. Run infra/setup.sh or setup.ps1 to generate one.");
        }

        if (keyBytes.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"{SigningKeyKey} must decode to at least {MinimumKeyBytes} bytes; it decodes to {keyBytes.Length}.");
        }

        return new JwtSettings(new SymmetricSecurityKey(keyBytes), issuer, audience);
    }

    /// <summary>The parameters the API uses to validate incoming bearer tokens.</summary>
    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = ClockSkew,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey,
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = JwtClaimNames.Subject,
        RoleClaimType = JwtClaimNames.Role,
    };

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Configuration value {key} is missing. Set it in infra/.env (see infra/.env.example).");
        }

        return value.Trim();
    }
}
