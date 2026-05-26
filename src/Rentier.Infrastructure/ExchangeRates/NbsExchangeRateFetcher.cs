using System.Globalization;
using System.Xml.Linq;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.ValueObjects;

namespace Rentier.Infrastructure.ExchangeRates;

public sealed class NbsExchangeRateFetcher : IExchangeRateFetcher
{
    private readonly HttpClient _http;
    private readonly IExchangeRateCacheRepository _cache;

    public static readonly IReadOnlySet<string> SupportedCurrencies =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "EUR", "USD", "GBP", "CHF", "AUD", "CAD", "CZK", "DKK",
          "HUF", "JPY", "NOK", "PLN", "SEK", "TRY", "AED" };

    public NbsExchangeRateFetcher(HttpClient http, IExchangeRateCacheRepository cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<Result<ExchangeRate, Error>> FetchRateAsync(
        DateOnly date, string currency, CancellationToken ct = default)
    {
        // Step 1: Validate currency
        var upperCurrency = currency.ToUpperInvariant();
        if (!SupportedCurrencies.Contains(upperCurrency))
            return Result<ExchangeRate, Error>.Failure(
                new Error("UNSUPPORTED_CURRENCY",
                    $"Currency '{upperCurrency}' is not supported by NBS."));

        // Step 2: Cache lookup
        var cached = await _cache.GetAsync(date, upperCurrency, ct);
        if (cached is not null)
            return Result<ExchangeRate, Error>.Success(cached);

        // Step 3: Build NBS URL — date format MUST be MM/dd/yyyy (US month-first).
        // IMPORTANT: use CultureInfo.InvariantCulture — the '/' in a format string is
        // the culture-specific date separator, which on Serbian/European locales becomes '.'
        // (producing "01.15.2024" instead of "01/15/2024", which NBS rejects).
        var dateStr = date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
        var url = $"https://webservices.nbs.rs/CommunicationOfficeService1_3/" +
                  $"ExchangeRateXmlService.asmx/GetAllExchangeRates" +
                  $"?InputDate={dateStr}&CurrencyCodeCo=0";

        // Step 4: HTTP GET
        string xml;
        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Result<ExchangeRate, Error>.Failure(
                    new Error("NBS_HTTP_ERROR",
                        $"NBS returned {(int)response.StatusCode} {response.StatusCode}."));

            // Step 5: Read body
            xml = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Result<ExchangeRate, Error>.Failure(
                new Error("NBS_HTTP_ERROR", ex.Message));
        }

        // Step 6: Parse XML — LocalName used to avoid namespace binding fragility
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (Exception ex)
        {
            return Result<ExchangeRate, Error>.Failure(
                new Error("NBS_PARSE_ERROR",
                    $"Failed to parse NBS XML: {ex.Message}"));
        }

        // Step 7: Extract entries — empty = weekend / Serbian holiday
        var entries = doc.Descendants()
            .Where(e => e.Name.LocalName == "ExchangeRateXml")
            .ToList();

        if (entries.Count == 0)
            return Result<ExchangeRate, Error>.Failure(
                new Error("RATE_NOT_FOUND",
                    $"No NBS exchange rate published for {date:yyyy-MM-dd}."));

        // Step 8: Build batch — only SupportedCurrencies are retained
        var allRates = new List<ExchangeRate>();
        foreach (var entry in entries)
        {
            var code = entry.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "CurrencyCodeCo")?.Value;
            if (code is null || !SupportedCurrencies.Contains(code)) continue;

            var unitStr = entry.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "Unit")?.Value;
            var middleStr = entry.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "Middle_Rate")?.Value;

            if (unitStr is null || middleStr is null) continue;

            if (!decimal.TryParse(unitStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var unit) || unit == 0) continue;
            if (!decimal.TryParse(middleStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var middle)) continue;

            var rateToRsd = middle / unit;
            if (rateToRsd <= 0) continue;

            allRates.Add(new ExchangeRate(date, code.ToUpperInvariant(), rateToRsd));
        }

        if (allRates.Count == 0)
            return Result<ExchangeRate, Error>.Failure(
                new Error("RATE_NOT_FOUND",
                    $"No NBS exchange rate published for {date:yyyy-MM-dd}."));

        // Step 9: Batch cache all rates for this date — non-fatal if write fails
        try { await _cache.SaveBatchAsync(allRates, ct); }
        catch (Exception ex) when (ex is Microsoft.EntityFrameworkCore.DbUpdateException or InvalidOperationException)
        {
            // Cache write failure is non-fatal; the fetched rates are still returned below
        }

        // Step 10: Return requested rate from parsed batch (not re-queried from DB)
        var result = allRates.FirstOrDefault(r =>
            string.Equals(r.Currency, upperCurrency, StringComparison.OrdinalIgnoreCase));

        return result is not null
            ? Result<ExchangeRate, Error>.Success(result)
            : Result<ExchangeRate, Error>.Failure(
                new Error("RATE_NOT_FOUND",
                    $"Currency '{upperCurrency}' not found in NBS response for {date:yyyy-MM-dd}."));
    }
}
