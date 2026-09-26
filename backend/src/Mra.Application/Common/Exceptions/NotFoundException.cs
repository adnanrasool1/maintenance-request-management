namespace Mra.Application.Common.Exceptions;

/// <summary>
/// The requested resource doesn't exist, belongs to another organisation, or isn't visible to
/// the caller. Mapped to 404 with a fixed detail, so the three cases are indistinguishable.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException()
        : base("The requested resource was not found.")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }
}
