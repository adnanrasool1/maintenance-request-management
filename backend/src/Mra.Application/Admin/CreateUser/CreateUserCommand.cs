using MediatR;

namespace Mra.Application.Admin.CreateUser;

/// <summary>
/// Creates a Requester or Approver in the caller's organisation (contract §3.2, FR-2.3).
/// <see cref="Role"/> is a string matched case-insensitively (contract §1).
/// </summary>
public sealed record CreateUserCommand(string Email, string Password, string Role) : IRequest<UserDto>;
