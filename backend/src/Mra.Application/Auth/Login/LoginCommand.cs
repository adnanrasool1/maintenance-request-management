using MediatR;

namespace Mra.Application.Auth.Login;

/// <summary>Contract §2.2 request body.</summary>
public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;

/// <summary>Contract §2.2 response body. <see cref="ExpiresAt"/> is UTC.</summary>
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, LoginUser User);

/// <summary><see cref="Role"/> is the enum name: SystemAdmin, TenantAdmin, Requester or Approver.</summary>
public sealed record LoginUser(Guid Id, string Email, string Role);
