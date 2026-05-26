namespace Rentier.Domain.Enums;

/// <summary>Indicates whether the exchange rate used was from the exact income date or a fallback business day.</summary>
public enum ExchangeRateSourceType
{
    /// <summary>Rate was fetched for the exact income date.</summary>
    Exact = 0,
    /// <summary>Rate was fetched from a prior business day (income date had no published rate).</summary>
    Fallback = 1
}
