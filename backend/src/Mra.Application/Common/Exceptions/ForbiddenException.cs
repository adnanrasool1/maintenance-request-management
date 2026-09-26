namespace Mra.Application.Common.Exceptions;

/// <summary>
/// An application ownership rule rejects the caller. Mapped to 403, with the message as the
/// ProblemDetails <c>detail</c>, so the message must not reveal data.
/// Never use this for another tenant's data: that is a <see cref="NotFoundException"/>.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException()
        : base("You are not allowed to perform this action.")
    {
    }

    public ForbiddenException(string message)
        : base(message)
    {
    }
}
