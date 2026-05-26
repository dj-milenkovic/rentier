using Rentier.Domain.Exceptions;

namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Represents a daily NBS official exchange rate from a foreign currency to RSD.
/// DateOnly and decimal per constitution Principle III.
/// Properties are get-only to prevent validation bypass via <c>with</c> expressions.
/// </summary>
public sealed record ExchangeRate
{
    public DateOnly Date { get; }
    public string Currency { get; }
    public decimal RateToRsd { get; }

    public ExchangeRate(DateOnly date, string currency, decimal rateToRsd)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("Currency must not be null or empty");
        if (rateToRsd <= 0)
            throw new DomainException($"RateToRsd must be positive, got {rateToRsd}");

        Date = date;
        Currency = currency.ToUpperInvariant();
        RateToRsd = rateToRsd;
    }
}
