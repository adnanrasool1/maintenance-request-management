namespace Mra.Application.Common.Exceptions;

/// <summary>
/// A save broke the system-wide unique index on user emails (contract D-2). Thrown by
/// Infrastructure; the handler that saved turns it into a <c>ValidationException</c> on its
/// email field, so the client gets 400. Handlers can't pre-check across organisations, because
/// only login and System Admin handlers may bypass the tenant filter.
/// </summary>
public sealed class DuplicateEmailException(Exception innerException)
    : Exception("Email is already in use.", innerException);
