using MediatR;
using Microsoft.EntityFrameworkCore;
using Mra.Application.Common.Abstractions;
using Mra.Application.Common.Exceptions;

namespace Mra.Application.Auth.Login;

public sealed class LoginCommandHandler(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : IRequestHandler<LoginCommand, LoginResponse>
{
    // A real hash to verify against when the email is unknown, so both failure paths do the
    // same PBKDF2 work and the response time doesn't reveal whether the email exists.
    private static string? _dummyHash;

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // The Email column's collation is case-insensitive, so trimming is all that's needed (contract §1).
        var email = request.Email.Trim();

        // Allowed use of IgnoreQueryFilters (CLAUDE.md): the caller has no token yet, so there is no tenant to filter by.
        var user = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => new { u.Id, u.OrganisationId, u.Email, u.PasswordHash, u.Role })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            _dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString());
            passwordHasher.Verify(_dummyHash, request.Password);
            throw new InvalidCredentialsException();
        }

        if (!passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new InvalidCredentialsException();
        }

        var role = user.Role.ToString();
        var token = tokenService.CreateToken(user.Id, user.OrganisationId, role, user.Email);

        return new LoginResponse(token.Token, token.ExpiresAt, new LoginUser(user.Id, user.Email, role));
    }
}
