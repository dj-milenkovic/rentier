using Rentier.Domain.Exceptions;

namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Represents a daily NBS official exchange rate from a foreign currency to RSD.
/// DateOnly and decimal per constitution Principle III.
/// </summary>
public record ExchangeRate
{
    public DateOnly Date { get; init; }
    public string Currency { get; init; }
    public decimal RateToRsd { get; init; }

    public ExchangeRate(DateOnly date, string currency, decimal rateToRsd)
    {
        if (rateToRsd <= 0)
            throw new DomainException($"RateToRsd must be positive, got {rateToRsd}");

        Date = date;
        Currency = currency;
        RateToRsd = rateToRsd;
    }
}
