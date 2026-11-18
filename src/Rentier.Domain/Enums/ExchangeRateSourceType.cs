namespace Rentier.Domain.Enums;

/// <summary>Where the exchange rate applied to a filing came from.</summary>
public enum ExchangeRateSourceType
{
    /// <summary>NBS rate for the exact income date.</summary>
    Exact = 0,

    /// <summary>NBS rate from a prior business day (the income date had no published rate).</summary>
    Fallback = 1,

    /// <summary>
    /// Synthesized for a currency NBS does not publish: the broker's statement rate to a pivot
    /// currency × the NBS rate of that pivot currency. The source date is the pivot rate's date.
    /// </summary>
    CrossRate = 2,
}
