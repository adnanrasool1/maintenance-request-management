namespace Mra.Domain;

// Base for rule violations raised by the domain, so the API's exception handler can map them
// (architecture §4.4). Argument guards use the standard ArgumentException types instead.
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }
}
