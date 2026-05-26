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

    // Non-retryable error codes - resolver returns immediately without walking back
    private static readonly HashSet<string> NonRetryable =
        new(StringComparer.OrdinalIgnoreCase) { "UNSUPPORTED_CURRENCY", "NBS_PARSE_ERROR" };

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
            _logger.LogInformation(
                "Rate resolved (exact): {Currency} on {Date} = {Rate}",
                currency, date, exactResult.Value.RateToRsd);
            return Result<RateResolution, Error>.Success(
                new RateResolution(exactResult.Value, date, ExchangeRateSourceType.Exact));
        }

        // Non-retryable errors - return immediately
        if (NonRetryable.Contains(exactResult.Error.Code))
            return Result<RateResolution, Error>.Failure(exactResult.Error);

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

            if (fallbackResult.Error.Code == "RATE_NOT_FOUND")
                continue;

            _logger.LogWarning(
                "Unexpected error fetching rate for {Currency} on {Date}: {Code}",
                currency, candidate, fallbackResult.Error.Code);
        }

        // Step 3: Exhausted
        var triedList = string.Join(", ", triedDates.Select(d => d.ToString("yyyy-MM-dd")));
        var message = $"No rate found for {currency} on {date:yyyy-MM-dd}. Tried: {triedList}. " +
                      "Manually import rate or check NBS availability.";
        _logger.LogError(
            "Rate resolution exhausted for {Currency} on {Date}. Tried: {TriedDates}",
            currency, date, triedList);
        return Result<RateResolution, Error>.Failure(new Error("RATE_NOT_FOUND", message));
    }
}
