namespace Mra.Application.Common.Abstractions;

/// <summary>Issues signed access tokens (contract §2.1).</summary>
public interface ITokenService
{
    /// <summary>
    /// Creates an access token with the claims <c>sub</c>, <c>org</c> (omitted when
    /// <paramref name="organisationId"/> is null), <c>role</c> and <c>email</c>.
    /// </summary>
    /// <param name="role">
    /// SystemAdmin, TenantAdmin, Requester or Approver. A string until the Domain <c>Role</c>
    /// enum lands (T1.1).
    /// </param>
    AccessToken CreateToken(Guid userId, Guid? organisationId, string role, string email);
}

/// <summary>A signed token and its expiry. <see cref="ExpiresAt"/> is UTC (<see cref="DateTimeKind.Utc"/>).</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAt);
