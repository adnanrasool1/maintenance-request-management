using Microsoft.AspNetCore.Identity;
using Mra.Application.Common.Abstractions;

namespace Mra.Infrastructure.Authentication;

/// <summary>PBKDF2 hashing through ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>.</summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    // PasswordHasher<TUser> needs a user type but its default (V3) format never reads the user.
    // A private marker keeps the Domain User entity out of this adapter.
    private sealed class HashSubject;

    private static readonly HashSubject Subject = new();

    private readonly PasswordHasher<HashSubject> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(Subject, hash, password) is not PasswordVerificationResult.Failed;
}
