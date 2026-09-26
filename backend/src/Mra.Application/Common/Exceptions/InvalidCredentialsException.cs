namespace Mra.Application.Common.Exceptions;

/// <summary>
/// Login failed. Thrown for an unknown email and for a wrong password alike, so the two cases
/// can't be told apart (PRD FR-1, architecture §9). Mapped to 401 with a fixed detail (contract §4.3).
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid email or password.")
    {
    }
}
