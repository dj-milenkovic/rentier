using Rentier.Domain.Exceptions;

namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Represents a monetary amount with its currency.
/// Amount is decimal per constitution Principle III — no IEEE 754 floating-point types.
/// Properties are get-only to prevent validation bypass via <c>with</c> expressions.
/// </summary>
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("Currency must not be null or empty");
        if (amount < 0)
            throw new DomainException($"Amount must not be negative, got {amount}");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }
}
