using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Parsing;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Resolves the exchange rate to apply for a given date and currency: the NBS middle rate for the
/// date, walking back through business days when the date has no published rate.
/// </summary>
/// <remarks>
/// Failure codes callers act on: <see cref="ErrorCodes.RATE_SOURCE_UNAVAILABLE"/> (temporary —
/// retry later), <see cref="ErrorCodes.RATE_NOT_FOUND"/> (no rate within the look-back),
/// <see cref="ErrorCodes.UNSUPPORTED_CURRENCY"/> (NBS does not publish the currency and no broker
/// cross-rate applied) and <see cref="ErrorCodes.INVALID_EXCHANGE_RATE"/>.
/// </remarks>
public interface IExchangeRateResolver
{
    /// <param name="brokerRates">
    /// The statement's own FX rates. Used only when NBS does not publish <paramref name="currency"/>:
    /// the rate is then synthesized as broker rate to a pivot currency × that pivot's NBS rate, and
    /// tagged <see cref="Rentier.Domain.Enums.ExchangeRateSourceType.CrossRate"/>.
    /// </param>
    Task<Result<RateResolution, Error>> ResolveAsync(
        DateOnly date,
        string currency,
        HolidayConf holidays,
        int maxLookbackDays = 10,
        IReadOnlyList<IbkrExchangeRate>? brokerRates = null,
        CancellationToken ct = default);
}
