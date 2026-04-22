using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Domain.ValueObjects;

namespace Rentier.Infrastructure.ExchangeRates;

public sealed class CompositeExchangeRateFetcher : IExchangeRateFetcher
{
    private readonly IExchangeRateFetcher _primary;
    private readonly IExchangeRateFetcher _secondary;

    // Error codes that trigger fallback to secondary
    private static readonly HashSet<string> FallbackTriggers =
        new(StringComparer.OrdinalIgnoreCase) { "NBS_HTTP_ERROR", "NBS_PARSE_ERROR" };

    public CompositeExchangeRateFetcher(IExchangeRateFetcher primary, IExchangeRateFetcher secondary)
    {
        _primary = primary;
        _secondary = secondary;
    }

    public async Task<Result<ExchangeRate, Error>> FetchRateAsync(
        DateOnly date, string currency, CancellationToken ct = default)
    {
        var primaryResult = await _primary.FetchRateAsync(date, currency, ct);
        if (primaryResult.IsSuccess)
            return primaryResult;

        // Only fall back on HTTP/parse errors, not on RATE_NOT_FOUND or UNSUPPORTED_CURRENCY
        if (!FallbackTriggers.Contains(primaryResult.Error.Code))
            return primaryResult;

        return await _secondary.FetchRateAsync(date, currency, ct);
    }
}