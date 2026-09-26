namespace Mra.Infrastructure.Persistence;

// A write that breaks tenant isolation or audit immutability. It always means a bug in the
// application, never bad input, so it maps to 500.
public sealed class TenantGuardException(string message) : InvalidOperationException(message);
