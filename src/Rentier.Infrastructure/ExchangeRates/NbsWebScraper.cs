using System.Globalization;
using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Logging;

namespace Rentier.Infrastructure.ExchangeRates;

public sealed class NbsWebScraper : IExchangeRateFetcher
{
    private readonly HttpClient _http;
    private readonly IExchangeRateCacheRepository _cache;
    private readonly ILogger<NbsWebScraper> _logger;

    // NBS web app uses Serbian locale with comma decimal separator
    private static readonly CultureInfo SerbianCulture = new CultureInfo("sr-Latn-RS");

    public NbsWebScraper(
        HttpClient http, IExchangeRateCacheRepository cache, ILogger<NbsWebScraper> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<ExchangeRate, Error>> FetchRateAsync(
        DateOnly date, string currency, CancellationToken ct = default)
    {
        var upperCurrency = currency.ToUpperInvariant();

        // Cache check
        var cached = await _cache.GetAsync(date, upperCurrency, ct);
        if (cached is not null)
        {
            _logger.NbsCacheHit(upperCurrency, date);
            return Result<ExchangeRate, Error>.Success(cached);
        }

        _logger.NbsCacheMiss(upperCurrency, date);

        // Build URL: dd.MM.yyyy. format used by NBS web app
        var dateStr = date.ToString("dd.MM.yyyy.", CultureInfo.InvariantCulture);
        var url = $"https://webappcenter.nbs.rs/ExchangeRateWebApp/ExchangeRate/IndexByDate" +
                  $"?isSearchExecuted=true&Date={dateStr}&ExchangeRateListTypeID=1";

        // HTTP GET
        string html;
        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.NbsHttpError((int)response.StatusCode, date);
                return Result<ExchangeRate, Error>.Failure(
                    new Error(ErrorCodes.NBS_HTTP_ERROR,
                        $"NBS web app returned {(int)response.StatusCode} for {date:yyyy-MM-dd}."));
            }

            html = await response.Content.ReadAsStringAsync(ct);
        }
        // TaskCanceledException is an HttpClient timeout unless the caller cancelled —
        // caller cancellation must propagate, not become NBS_HTTP_ERROR.
        catch (Exception ex) when (
            ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            _logger.NbsRequestFailed(ex, date);
            return Result<ExchangeRate, Error>.Failure(
                new Error(ErrorCodes.NBS_HTTP_ERROR, ex.Message));
        }

        // Parse with AngleSharp
        try
        {
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html), ct);

            // Find data rows (at least 6 td cells) and parse supported rates from them.
            // "No rows" and "no rows that parsed into a rate" both surface as the same
            // RATE_NOT_FOUND error below.
            var allRates = ParseRatesFromHtml(document, date);

            _logger.NbsRatesParsed(allRates.Count, date);

            if (allRates.Count == 0)
                return Result<ExchangeRate, Error>.Failure(
                    new Error(ErrorCodes.RATE_NOT_FOUND,
                        $"No exchange rates published for {date:yyyy-MM-dd} (NBS web app)."));

            // Cache all rates - swallow cache write failures (non-fatal), but re-throw cancellation
            try
            {
                await _cache.SaveBatchAsync(allRates, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Non-fatal: the rate was fetched, it just will not be cached for next time.
                _logger.NbsCacheWriteFailed(ex, allRates.Count, date);
            }

            var result = allRates.FirstOrDefault(r =>
                string.Equals(r.Currency, upperCurrency, StringComparison.OrdinalIgnoreCase));

            return result is not null
                ? Result<ExchangeRate, Error>.Success(result)
                : Result<ExchangeRate, Error>.Failure(
                    new Error(ErrorCodes.RATE_NOT_FOUND,
                        $"Currency '{upperCurrency}' not found in NBS web app for {date:yyyy-MM-dd}."));
        }
        // Boundary translation: scraping third-party HTML can fail in ways that cannot be
        // enumerated (markup changes, number formats), so everything except cancellation
        // becomes NBS_SCRAPE_ERROR, which ExchangeRateResolver treats as "source unavailable".
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.NbsParseFailed(ex, date);
            return Result<ExchangeRate, Error>.Failure(
                new Error(ErrorCodes.NBS_SCRAPE_ERROR, $"Failed to parse NBS web app HTML: {ex.Message}"));
        }
    }

    // Extracted from FetchRateAsync (S3776 cognitive complexity) — parses the NBS web
    // app's exchange-rate table rows into ExchangeRate values.
    private static List<ExchangeRate> ParseRatesFromHtml(IDocument document, DateOnly date)
    {
        // Find data rows: rows with at least 6 td cells
        var dataRows = document
            .QuerySelectorAll("table tbody tr, table tr")
            .Where(r => r.QuerySelectorAll("td").Length >= 6)
            .ToList();

        var allRates = new List<ExchangeRate>();
        foreach (var row in dataRows)
        {
            var cells = row.QuerySelectorAll("td").ToList();
            if (cells.Count < 6) continue;

            var code = cells[0].TextContent.Trim();
            var unitStr = cells[3].TextContent.Trim();
            var buyingStr = cells[4].TextContent.Trim();
            var sellingStr = cells[5].TextContent.Trim();

            if (string.IsNullOrEmpty(code)) continue;
            if (!int.TryParse(unitStr, out var unit) || unit <= 0) continue;
            if (!decimal.TryParse(buyingStr, NumberStyles.Any, SerbianCulture, out var buying)) continue;
            if (!decimal.TryParse(sellingStr, NumberStyles.Any, SerbianCulture, out var selling)) continue;

            var middle = (buying + selling) / 2m;
            var rateToRsd = middle / unit;
            if (rateToRsd <= 0) continue;

            allRates.Add(new ExchangeRate(date, code.ToUpperInvariant(), rateToRsd));
        }

        return allRates;
    }
}
