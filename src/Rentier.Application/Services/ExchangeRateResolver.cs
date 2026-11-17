using Microsoft.Extensions.Logging;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Domain.Enums;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Services;

public sealed class ExchangeRateResolver : IExchangeRateResolver
{
    private readonly IExchangeRateFetcher _fetcher;
    private readonly ILogger<ExchangeRateResolver> _logger;

    // Fetcher error codes that mean "the rate source could not be reached or read right now":
    // NBS_HTTP_ERROR (network/HTTP failure) and NBS_SCRAPE_ERROR (the web-app fallback, used
    // after the primary API failed, returned an unreadable page).
    private static readonly HashSet<string> TRANSIENT_SOURCE_ERRORS =
        new(StringComparer.OrdinalIgnoreCase) { "NBS_HTTP_ERROR", "NBS_SCRAPE_ERROR" };

    public ExchangeRateResolver(IExchangeRateFetcher fetcher, ILogger<ExchangeRateResolver> logger)
    {
        _fetcher = fetcher;
        _logger = logger;
    }

    public async Task<Result<RateResolution, Error>> ResolveAsync(
        DateOnly date,
        string currency,
        HolidayConf holidays,
        int maxLookbackDays = 10,
        CancellationToken ct = default)
    {
        // Step 1: Try exact date
        var exactResult = await _fetcher.FetchRateAsync(date, currency, ct);
        if (exactResult.IsSuccess)
        {
            return Result<RateResolution, Error>.Success(
                new RateResolution(exactResult.Value, date, ExchangeRateSourceType.Exact));
        }

        // Only a confirmed "no rate published for this date" (weekend/holiday) may fall back to
        // an earlier business day. Any other failure — notably NBS being unreachable — means the
        // exact-date rate may well exist, so falling back would apply the wrong rate.
        if (!IsNoRatePublished(exactResult.Error))
            return Result<RateResolution, Error>.Failure(Classify(exactResult.Error, currency, date));

        // Step 2: Walk backward through business days
        var triedDates = new List<DateOnly> { date };
        foreach (var candidate in BusinessDayResolver.WalkBackward(date, holidays, maxLookbackDays))
        {
            ct.ThrowIfCancellationRequested();
            var fallbackResult = await _fetcher.FetchRateAsync(candidate, currency, ct);
            triedDates.Add(candidate);

            if (fallbackResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Rate resolved (fallback): {Currency} on {OriginalDate} used {FallbackDate}",
                    currency, date, candidate);
                return Result<RateResolution, Error>.Success(
                    new RateResolution(fallbackResult.Value, candidate, ExchangeRateSourceType.Fallback));
            }

            if (IsNoRatePublished(fallbackResult.Error))
                continue;

            // Skipping a candidate whose rate could not be determined would silently apply an
            // even older rate, so stop here.
            _logger.LogWarning(
                "Rate lookup for {Currency} on {Date} failed with {Code}; not walking back further",
                currency, candidate, fallbackResult.Error.Code);
            return Result<RateResolution, Error>.Failure(Classify(fallbackResult.Error, currency, candidate));
        }

        // Step 3: Exhausted
        var triedList = string.Join(", ", triedDates.Select(d => d.ToString("yyyy-MM-dd")));
        var message = $"No rate found for {currency} on {date:yyyy-MM-dd}. Tried: {triedList}. " +
                      "Manually import rate or check NBS availability.";
        _logger.LogError(
            "Rate resolution exhausted for {Currency} on {Date}. Tried: {TriedDates}",
            currency, date, triedList);
        return Result<RateResolution, Error>.Failure(new Error(ErrorCodes.RATE_NOT_FOUND, message));
    }

    private static bool IsNoRatePublished(Error error)
        => string.Equals(error.Code, ErrorCodes.RATE_NOT_FOUND, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps a temporary rate-source failure to <see cref="ErrorCodes.RATE_SOURCE_UNAVAILABLE"/> so
    /// callers can retry later instead of treating the rate as permanently missing; other errors
    /// (unsupported currency, unparseable response) are returned unchanged.
    /// </summary>
    private static Error Classify(Error error, string currency, DateOnly date)
        => TRANSIENT_SOURCE_ERRORS.Contains(error.Code)
            ? new Error(ErrorCodes.RATE_SOURCE_UNAVAILABLE,
                $"The NBS exchange-rate service could not be reached for {currency} on {date:yyyy-MM-dd} " +
                $"({error.Code}: {error.Message}). Try again later.")
            : error;
}
