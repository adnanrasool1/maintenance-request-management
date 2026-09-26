using Mra.Domain.Users;

namespace Mra.Application.Admin;

/// <summary>A created user (contract §3.1, §3.2). <see cref="Role"/> is the enum name, e.g. "Requester".</summary>
public sealed record UserDto(Guid Id, string Email, string Role);

public static class UserMappings
{
    public static UserDto ToDto(this User user) => new(user.Id, user.Email, user.Role.ToString());
}
