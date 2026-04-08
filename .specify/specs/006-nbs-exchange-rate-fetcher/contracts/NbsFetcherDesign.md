# NbsExchangeRateFetcher — Full Design

**Feature**: 006 — NBS Exchange Rate Fetcher  
**Date**: 2026-04-07

---

## Overview

`NbsExchangeRateFetcher` is a sealed Infrastructure service that implements `IExchangeRateFetcher`. It follows a **cache-first** strategy: the SQLite cache is checked before any HTTP call. On a cache miss, a single HTTP request fetches **all** currencies for the requested date; all 15 are batch-upserted and the requested one is returned.

---

## Class Signature

```csharp
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
        _http  = http;
        _cache = cache;
    }

    public async Task<Result<ExchangeRate, Error>> FetchRateAsync(
        DateOnly date, string currency, CancellationToken ct = default) { ... }
}
```

---

## NBS URL

```
GET https://webservices.nbs.rs/CommunicationOfficeService1_3/ExchangeRateXmlService.asmx/GetAllExchangeRates
    ?InputDate={date:MM/dd/yyyy}
    &CurrencyCodeCo=0
```

**Date format**: `MM/dd/yyyy` — US month-first. Example: January 15 2024 → `01/15/2024`.

**`CurrencyCodeCo=0`**: Returns all available currencies in one response.

---

## Full Algorithm

```
FetchRateAsync(date, currency)
│
├─ Step 1: Validate currency
│    └─ if currency ∉ SupportedCurrencies
│         └─ return Failure("UNSUPPORTED_CURRENCY", $"Currency {currency} is not supported by NBS")
│
├─ Step 2: Cache lookup
│    └─ cached = await _cache.GetAsync(date, currency, ct)
│         └─ if cached != null
│              └─ return Success(cached)
│
├─ Step 3: Build NBS URL
│    └─ url = "https://webservices.nbs.rs/.../GetAllExchangeRates" +
│             "?InputDate=" + date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) +
│             "&CurrencyCodeCo=0"
│    NOTE: MUST use CultureInfo.InvariantCulture — '/' in format strings is the
│          culture-specific date separator (on Serbian locale it becomes '.', breaking NBS)
│
├─ Step 4: HTTP GET
│    └─ response = await _http.GetAsync(url, ct)
│         └─ if !response.IsSuccessStatusCode
│              └─ return Failure("NBS_HTTP_ERROR", $"NBS returned {response.StatusCode}")
│
├─ Step 5: Read response body
│    └─ xml = await response.Content.ReadAsStringAsync(ct)
│
├─ Step 6: Parse XML
│    └─ try { doc = XDocument.Parse(xml) }
│         catch { return Failure("NBS_PARSE_ERROR", "Failed to parse NBS XML") }
│
├─ Step 7: Extract entries
│    └─ entries = doc.Descendants()
│                    .Where(e => e.Name.LocalName == "ExchangeRateXml")
│         └─ if entries is empty
│              └─ return Failure("RATE_NOT_FOUND", $"No NBS exchange rate found for {date:yyyy-MM-dd}")
│
├─ Step 8: Build ExchangeRate batch
│    └─ rates = new List<ExchangeRate>()
│         for each entry in entries:
│           code       = entry child LocalName="CurrencyCodeCo" .Value
│           unit       = decimal.Parse(entry child LocalName="Unit" .Value, InvariantCulture)
│           middle     = decimal.Parse(entry child LocalName="Middle_Rate" .Value, InvariantCulture)
│           rateToRsd  = middle / unit
│           if code ∈ SupportedCurrencies:
│               rates.Add(new ExchangeRate(date, code.ToUpperInvariant(), rateToRsd))
│
├─ Step 9: Batch save (fire & forget errors — don't fail the caller on cache write failure)
│    └─ await _cache.SaveBatchAsync(rates, ct)
│
└─ Step 10: Return requested rate
     └─ requested = rates.FirstOrDefault(r => r.Currency.Equals(currency, OrdinalIgnoreCase))
          └─ if requested == null
               └─ return Failure("RATE_NOT_FOUND", $"Currency {currency} not found in NBS response for {date:yyyy-MM-dd}")
          └─ return Success(requested)
```

---

## XML Element Mapping

| XML Element | CLR Mapping | Notes |
|-------------|------------|-------|
| `<CurrencyCodeCo>` | `string currency` | ISO 4217 alphabetic code, e.g. `EUR` |
| `<Unit>` | `decimal unit` | Number of foreign currency units the rate applies to (usually 1; JPY and HUF use 100) |
| `<Middle_Rate>` | `decimal middleRate` | NBS official middle rate for `Unit` units |
| (computed) | `decimal rateToRsd = middleRate / unit` | Rate for **1** unit of the foreign currency |
| `<BuyingRate>` | unused | Buy side — not used |
| `<SellingRate>` | unused | Sell side — not used |
| `<CurrencyCodeNumeric>` | unused | ISO numeric code — not used |

### Example (EUR, Unit=1)

```xml
<ExchangeRateXml>
  <CurrencyCodeNumeric>978</CurrencyCodeNumeric>
  <CurrencyCodeCo>EUR</CurrencyCodeCo>
  <Unit>1</Unit>
  <BuyingRate>117.2150</BuyingRate>
  <Middle_Rate>117.5952</Middle_Rate>
  <SellingRate>117.9754</SellingRate>
</ExchangeRateXml>
```
→ `RateToRsd = 117.5952 / 1 = 117.5952`

### Example (JPY, Unit=100)

```xml
<ExchangeRateXml>
  <CurrencyCodeCo>JPY</CurrencyCodeCo>
  <Unit>100</Unit>
  <Middle_Rate>77.3500</Middle_Rate>
</ExchangeRateXml>
```
→ `RateToRsd = 77.35 / 100 = 0.7735`

---

## Batch Cache Strategy

When `FetchRateAsync` calls the NBS API, the response contains up to 15 currencies. **All** are extracted and persisted via `SaveBatchAsync` — not just the requested one. This means:

- A second call for a different currency on the same date is served from cache (no HTTP).
- A complete date is cached after one HTTP round-trip.
- `SaveBatchAsync` performs an upsert — safe to call multiple times for the same date.

---

## Error Handling Summary

| Step | Error Code | Condition |
|------|-----------|-----------|
| 1 | `UNSUPPORTED_CURRENCY` | `currency ∉ SupportedCurrencies` |
| 4 | `NBS_HTTP_ERROR` | Non-2xx HTTP status |
| 6 | `NBS_PARSE_ERROR` | `XDocument.Parse` throws |
| 7 | `RATE_NOT_FOUND` | 0 `<ExchangeRateXml>` entries (weekend/holiday) |
| 10 | `RATE_NOT_FOUND` | Requested currency absent from parsed batch |

---

## ExchangeRateCacheRepository Contract

```csharp
public interface IExchangeRateCacheRepository
{
    // Point lookup — returns null on miss
    Task<ExchangeRate?> GetAsync(DateOnly date, string currency, CancellationToken ct = default);

    // Range lookup for tax calculation
    Task<IReadOnlyList<ExchangeRate>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, string currency, CancellationToken ct = default);

    // Single upsert
    Task SaveAsync(ExchangeRate rate, CancellationToken ct = default);

    // Batch upsert — used after NBS HTTP call
    Task SaveBatchAsync(IReadOnlyList<ExchangeRate> rates, CancellationToken ct = default);
}
```

### SaveBatchAsync Upsert Logic

```csharp
foreach (var rate in rates)
{
    var upper = rate.Currency.ToUpperInvariant();   // normalize before DB lookup
    var existing = await _db.ExchangeRateCache
        .FindAsync(new object[] { rate.Date, upper }, ct);

    if (existing is not null)
        _db.Entry(existing).CurrentValues.SetValues(
            new ExchangeRate(rate.Date, upper, rate.RateToRsd));
    else
        _db.ExchangeRateCache.Add(new ExchangeRate(rate.Date, upper, rate.RateToRsd));
}
await _db.SaveChangesAsync(ct);
```

---

## DI Registration

```csharp
// In InfrastructureServiceExtensions.AddInfrastructureServices:
services.AddTransient<IExchangeRateCacheRepository, ExchangeRateCacheRepository>();
services.AddHttpClient<IExchangeRateFetcher, NbsExchangeRateFetcher>();
```

`AddHttpClient<IExchangeRateFetcher, NbsExchangeRateFetcher>()` registers `NbsExchangeRateFetcher` as a transient typed client. The `HttpClient` instance injected into its constructor has its socket lifecycle managed by `IHttpClientFactory`.

---

## FakeHttpMessageHandler (for unit tests)

```csharp
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string _response;
    private readonly HttpStatusCode _statusCode;
    public int CallCount { get; private set; }

    internal FakeHttpMessageHandler(string response,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    { _response = response; _statusCode = statusCode; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(new HttpResponseMessage(_statusCode)
            { Content = new StringContent(_response) });
    }
}
```
