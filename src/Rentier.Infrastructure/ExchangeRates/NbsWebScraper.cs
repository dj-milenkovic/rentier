using System.Globalization;
using AngleSharp;
using AngleSharp.Dom;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.ValueObjects;

namespace Rentier.Infrastructure.ExchangeRates;

public sealed class NbsWebScraper : IExchangeRateFetcher
{
    private readonly HttpClient _http;
    private readonly IExchangeRateCacheRepository _cache;

    // NBS web app uses Serbian locale with comma decimal separator
    private static readonly CultureInfo SerbianCulture = new CultureInfo("sr-Latn-RS");

    public NbsWebScraper(HttpClient http, IExchangeRateCacheRepository cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<Result<ExchangeRate, Error>> FetchRateAsync(
        DateOnly date, string currency, CancellationToken ct = default)
    {
        var upperCurrency = currency.ToUpperInvariant();

        // Cache check
        var cached = await _cache.GetAsync(date, upperCurrency, ct);
        if (cached is not null)
            return Result<ExchangeRate, Error>.Success(cached);

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
                return Result<ExchangeRate, Error>.Failure(
                    new Error("NBS_HTTP_ERROR",
                        $"NBS web app returned {(int)response.StatusCode} for {date:yyyy-MM-dd}."));
            html = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Result<ExchangeRate, Error>.Failure(
                new Error("NBS_HTTP_ERROR", ex.Message));
        }

        // Parse with AngleSharp
        try
        {
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html), ct);

            // Find data rows: rows with at least 6 td cells
            var dataRows = document
                .QuerySelectorAll("table tbody tr, table tr")
                .Where(r => r.QuerySelectorAll("td").Length >= 6)
                .ToList();

            if (dataRows.Count == 0)
                return Result<ExchangeRate, Error>.Failure(
                    new Error("RATE_NOT_FOUND",
                        $"No exchange rates published for {date:yyyy-MM-dd} (NBS web app)."));

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

            if (allRates.Count == 0)
                return Result<ExchangeRate, Error>.Failure(
                    new Error("RATE_NOT_FOUND",
                        $"No exchange rates published for {date:yyyy-MM-dd} (NBS web app)."));

            // Cache all rates - swallow cache write failures (non-fatal), but re-throw cancellation
            try { await _cache.SaveBatchAsync(allRates, ct); } catch (Exception ex) when (ex is not OperationCanceledException) { /* non-fatal */ }

            var result = allRates.FirstOrDefault(r =>
                string.Equals(r.Currency, upperCurrency, StringComparison.OrdinalIgnoreCase));

            return result is not null
                ? Result<ExchangeRate, Error>.Success(result)
                : Result<ExchangeRate, Error>.Failure(
                    new Error("RATE_NOT_FOUND",
                        $"Currency '{upperCurrency}' not found in NBS web app for {date:yyyy-MM-dd}."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<ExchangeRate, Error>.Failure(
                new Error("NBS_SCRAPE_ERROR", $"Failed to parse NBS web app HTML: {ex.Message}"));
        }
    }
}
