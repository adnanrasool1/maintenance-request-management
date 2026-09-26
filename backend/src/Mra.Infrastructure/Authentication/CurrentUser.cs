using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Mra.Application.Common.Abstractions;

namespace Mra.Infrastructure.Authentication;

/// <summary>
/// Reads the caller from the validated token's claims on <see cref="HttpContext.User"/> only.
/// It never looks at the route, query string, headers or body (architecture §7, layer 1).
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ReadGuid(JwtClaimNames.Subject);

    public Guid? OrganisationId => ReadGuid(JwtClaimNames.Organisation);

    public string? Role => IsAuthenticated ? Principal!.FindFirstValue(JwtClaimNames.Role) : null;

    // Claims are only trusted on an authenticated principal; a malformed GUID reads as null.
    private Guid? ReadGuid(string claimType) =>
        IsAuthenticated && Guid.TryParse(Principal!.FindFirstValue(claimType), out var value) ? value : null;
}
