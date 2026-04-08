namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Represents a monetary amount with its currency.
/// Amount is decimal per constitution Principle III — no IEEE 754 floating-point types.
/// </summary>
public record Money(decimal Amount, string Currency);
