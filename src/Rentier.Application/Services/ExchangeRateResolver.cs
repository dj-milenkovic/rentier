using Microsoft.Extensions.Logging;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Domain.Enums;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;
using Rentier.Application.Logging;

namespace Rentier.Application.Services;

public sealed class ExchangeRateResolver : IExchangeRateResolver
{
    private readonly IExchangeRateFetcher _fetcher;
    private readonly ILogger<ExchangeRateResolver> _logger;

    // Fetcher error codes that mean "the rate source could not be reached or read right now":
    // NBS_HTTP_ERROR (network/HTTP failure) and NBS_SCRAPE_ERROR (the web-app fallback, used
    // after the primary API failed, returned an unreadable page).
    private static readonly HashSet<string> TRANSIENT_SOURCE_ERRORS =
        new(StringComparer.OrdinalIgnoreCase) { ErrorCodes.NBS_HTTP_ERROR, ErrorCodes.NBS_SCRAPE_ERROR };

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
        IReadOnlyList<IbkrExchangeRate>? brokerRates = null,
        CancellationToken ct = default)
    {
        var nbs = await ResolveNbsAsync(date, currency, holidays, maxLookbackDays, ct);

        // Broker statement rates are a fallback ONLY for currencies NBS does not publish. Any other
        // failure (NBS unreachable, no rate within the look-back) must surface: NBS's own rate for
        // the currency is authoritative and may exist.
        if (nbs.IsSuccess || brokerRates is null || nbs.Error.Code != ErrorCodes.UNSUPPORTED_CURRENCY)
            return nbs;

        return await ResolveCrossRateAsync(date, currency, holidays, maxLookbackDays, brokerRates, nbs.Error, ct);
    }

    /// <summary>
    /// Synthesizes a rate for a currency NBS does not publish from the broker's rate to a pivot
    /// currency (the statement's <c>ToCurrency</c>, usually the account base currency) × the NBS
    /// rate of that pivot. Falls back to <paramref name="original"/> when no broker rate exists or
    /// the pivot has no NBS rate; a pivot whose source is unavailable defers like any NBS outage.
    /// </summary>
    private async Task<Result<RateResolution, Error>> ResolveCrossRateAsync(
        DateOnly date,
        string currency,
        HolidayConf holidays,
        int maxLookbackDays,
        IReadOnlyList<IbkrExchangeRate> brokerRates,
        Error original,
        CancellationToken ct)
    {
        var candidates = brokerRates
            .Where(r => r.FromCurrency.Equals(currency, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var brokerRate = candidates.FirstOrDefault(r => r.Date == date) ?? candidates.FirstOrDefault();
        if (brokerRate is null)
            return Result<RateResolution, Error>.Failure(original);

        var pivot = await ResolveNbsAsync(date, brokerRate.ToCurrency, holidays, maxLookbackDays, ct);
        if (!pivot.IsSuccess)
        {
            return pivot.Error.Code == ErrorCodes.RATE_SOURCE_UNAVAILABLE
                ? pivot
                : Result<RateResolution, Error>.Failure(original);
        }

        var syntheticValue = brokerRate.Rate * pivot.Value.Rate.RateToRsd;
        if (syntheticValue <= 0)
        {
            return Result<RateResolution, Error>.Failure(
                new Error(ErrorCodes.INVALID_EXCHANGE_RATE,
                    $"Synthetic cross-rate for '{currency}' on {date:yyyy-MM-dd} is not positive ({syntheticValue})."));
        }

        _logger.RateResolvedByCrossRate(
            currency, date, brokerRate.Rate, brokerRate.ToCurrency, pivot.Value.Rate.RateToRsd, pivot.Value.SourceDate);

        return Result<RateResolution, Error>.Success(new RateResolution(
            new ExchangeRate(pivot.Value.SourceDate, currency, syntheticValue),
            pivot.Value.SourceDate,
            ExchangeRateSourceType.CrossRate));
    }

    private async Task<Result<RateResolution, Error>> ResolveNbsAsync(
        DateOnly date,
        string currency,
        HolidayConf holidays,
        int maxLookbackDays,
        CancellationToken ct)
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
                _logger.RateResolvedByFallback(currency, date, candidate);
                return Result<RateResolution, Error>.Success(
                    new RateResolution(fallbackResult.Value, candidate, ExchangeRateSourceType.Fallback));
            }

            if (IsNoRatePublished(fallbackResult.Error))
                continue;

            // Skipping a candidate whose rate could not be determined would silently apply an
            // even older rate, so stop here.
            _logger.RateLookupStopped(currency, candidate, fallbackResult.Error.Code);
            return Result<RateResolution, Error>.Failure(Classify(fallbackResult.Error, currency, candidate));
        }

        // Step 3: Exhausted
        var triedList = string.Join(", ", triedDates.Select(d => d.ToString("yyyy-MM-dd")));
        var message = $"No rate found for {currency} on {date:yyyy-MM-dd}. Tried: {triedList}. " +
                      "Manually import rate or check NBS availability.";
        _logger.RateResolutionExhausted(currency, date, triedList);
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
