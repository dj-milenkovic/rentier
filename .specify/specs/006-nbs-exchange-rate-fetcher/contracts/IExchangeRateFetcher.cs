using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Fetches an official NBS (Narodna Banka Srbije) exchange rate for a given date and currency.
/// Implementations MUST consult the local SQLite cache before making an HTTP call.
/// On a cache miss, ALL currencies returned for that date MUST be batch-cached.
/// </summary>
/// <remarks>
/// <para>
/// Supported currencies: EUR, USD, GBP, CHF, AUD, CAD, CZK, DKK, HUF, JPY, NOK, PLN, SEK, TRY, AED.
/// Requesting an unsupported currency returns <c>UNSUPPORTED_CURRENCY</c>.
/// </para>
/// <para>
/// NBS does not publish rates on weekends or Serbian public holidays.
/// Requesting such a date returns <c>RATE_NOT_FOUND</c>.
/// Callers are responsible for date adjustment (e.g., rolling back to the previous business day).
/// </para>
/// <para>
/// All monetary values use <c>decimal</c>; all dates use <c>DateOnly</c> — per Rentier Constitution Principle III.
/// </para>
/// </remarks>
public interface IExchangeRateFetcher
{
    /// <summary>
    /// Returns the official NBS middle exchange rate for <paramref name="currency"/> on <paramref name="date"/>.
    /// </summary>
    /// <param name="date">The calendar date for which the rate is requested.</param>
    /// <param name="currency">
    /// ISO 4217 currency code (case-insensitive).
    /// Must be one of the 15 NBS-supported currencies.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{TValue,TError}"/> containing the <see cref="ExchangeRate"/> on success,
    /// or an <c>Error</c> with one of the following codes on failure:
    /// <list type="bullet">
    ///   <item><term>UNSUPPORTED_CURRENCY</term><description>Currency is not in the NBS-supported set.</description></item>
    ///   <item><term>RATE_NOT_FOUND</term><description>NBS published no rate for this date (weekend / holiday) or the currency was absent from the response.</description></item>
    ///   <item><term>NBS_HTTP_ERROR</term><description>The NBS HTTP call returned a non-2xx status.</description></item>
    ///   <item><term>NBS_PARSE_ERROR</term><description>The NBS XML response could not be parsed.</description></item>
    /// </list>
    /// </returns>
    Task<Result<ExchangeRate, Error>> FetchRateAsync(
        DateOnly date,
        string currency,
        CancellationToken ct = default);
}
