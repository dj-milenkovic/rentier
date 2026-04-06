namespace Rentier.Domain.Exceptions;

/// <summary>
/// Thrown by domain entities and value objects to signal invariant violations.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}
