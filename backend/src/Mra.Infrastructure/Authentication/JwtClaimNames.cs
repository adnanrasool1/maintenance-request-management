namespace Mra.Infrastructure.Authentication;

/// <summary>The claim types in our tokens (contract §2.1). Inbound claim mapping is off, so they arrive unchanged.</summary>
public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string Organisation = "org";
    public const string Role = "role";
    public const string Email = "email";
}
