namespace Mra.Application.Common.Exceptions;

/// <summary>
/// A save broke the unique index on (organisation, site name). Thrown by Infrastructure when two
/// sites with the same name are created at the same time; the handler turns it into a
/// <c>ValidationException</c> on <c>name</c> (400).
/// </summary>
public sealed class DuplicateSiteNameException(Exception innerException)
    : Exception("A site with this name already exists.", innerException);
