namespace Mra.Application.Common.Abstractions;

/// <summary>Hashes and verifies passwords (PRD FR-1.2). Plain-text passwords are never stored.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>True when <paramref name="password"/> matches <paramref name="hash"/>.</summary>
    bool Verify(string hash, string password);
}
