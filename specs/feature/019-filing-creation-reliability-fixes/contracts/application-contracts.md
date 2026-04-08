# Application Contracts: Filing Creation Reliability Fixes

**Feature**: 019-filing-creation-reliability-fixes  
**Date**: 2025-07-16

## Interface Changes

### 1. `IExchangeRateFetcher` — Unchanged

**Location**: `src/Rentier.Application/Interfaces/IExchangeRateFetcher.cs`

```csharp
public interface IExchangeRateFetcher
{
    Task<Result<ExchangeRate, Error>> FetchRateAsync(
        DateOnly date, string currency, CancellationToken ct = default);
}
```

**No changes to this interface.** The existing contract is sufficient. The ASMX→scraper fallback chain is handled transparently by `CompositeExchangeRateFetcher` which implements this same interface.

**Implementations after this feature**:
1. `NbsExchangeRateFetcher` — existing ASMX XML fetcher (unchanged)
2. `NbsWebScraper` — **new** HTML scraper fetcher
3. `CompositeExchangeRateFetcher` — **new** decorator that chains (1) then (2)

**DI registration change**: `AddHttpClient<IExchangeRateFetcher, NbsExchangeRateFetcher>()` is replaced by registering `CompositeExchangeRateFetcher` as the `IExchangeRateFetcher` implementation, which internally receives named `NbsExchangeRateFetcher` and `NbsWebScraper` instances.

---

### 2. `ExchangeRateResolver` — New Application Service

**Location**: `src/Rentier.Application/Services/ExchangeRateResolver.cs`

```csharp
namespace Rentier.Application.Services;

/// <summary>
/// Resolves exchange rates with business day fallback logic.
/// Orchestrates: try exact date → walk backward through business days → error.
/// Returns rate + provenance metadata for audit trail.
/// </summary>
public sealed class ExchangeRateResolver
{
    private readonly IExchangeRateFetcher _fetcher;

    public ExchangeRateResolver(IExchangeRateFetcher fetcher)
    {
        _fetcher = fetcher;
    }

    /// <summary>
    /// Resolves an exchange rate for the given date and currency.
    /// First tries exact date, then falls back to previous business days.
    /// </summary>
    /// <param name="date">Income event date.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <param name="holidays">Serbian public holiday configuration.</param>
    /// <param name="maxLookbackDays">Maximum calendar days to walk backward (default: 10).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>RateResolution with rate, source date, and source type (Exact/Fallback).</returns>
    public async Task<Result<RateResolution, Error>> ResolveAsync(
        DateOnly date,
        string currency,
        HolidayConf holidays,
        int maxLookbackDays = 10,
        CancellationToken ct = default);
}
```

**Return type**: `Result<RateResolution, Error>` where:
- Success: `RateResolution(ExchangeRate rate, DateOnly sourceDate, ExchangeRateSourceType sourceType)`
- Failure: `Error("RATE_NOT_FOUND", "Rate for {currency} not found; searched {date} back to {earliestDate}. Dates tried: {list}.")`

**Algorithm**:
```
1. Try FetchRateAsync(date, currency)
   → Success: return RateResolution(rate, date, Exact)
   → Failure with UNSUPPORTED_CURRENCY or NBS_PARSE_ERROR: return error immediately (not retryable)

2. For each previousBusinessDay from BusinessDayResolver.WalkBackward(date, holidays, maxLookbackDays):
   → Try FetchRateAsync(candidateDate, currency)
   → Success: return RateResolution(rate, candidateDate, Fallback)
   → Failure with RATE_NOT_FOUND: continue to next candidate
   → Failure with other error: continue to next candidate (log warning)

3. All candidates exhausted: return Error("RATE_NOT_FOUND", diagnostic message with all dates tried)
```

**Logging contract**: Each resolution step is logged at appropriate level:
- `Information`: Successful exact-date resolution
- `Warning`: Fallback used (with original date and fallback date)
- `Error`: All candidates exhausted (with full search history)

---

### 3. `BusinessDayResolver` — New Domain Service

**Location**: `src/Rentier.Domain/Services/BusinessDayResolver.cs`

```csharp
namespace Rentier.Domain.Services;

/// <summary>
/// Resolves business days by walking backward from a given date,
/// skipping weekends and Serbian public holidays.
/// Pure domain logic — no I/O, no async, DateOnly only.
/// Mirrors FilingDeadlineCalculator pattern.
/// </summary>
public static class BusinessDayResolver
{
    /// <summary>
    /// Returns whether the given date is a business day (not weekend, not holiday).
    /// </summary>
    public static bool IsBusinessDay(DateOnly date, HolidayConf holidays);

    /// <summary>
    /// Returns the previous business day on or before the given date.
    /// If the date itself is a business day, it returns the date itself.
    /// </summary>
    public static DateOnly FindPreviousBusinessDay(DateOnly date, HolidayConf holidays);

    /// <summary>
    /// Yields candidate business days walking backward from the day before the
    /// given date, up to maxLookbackDays calendar days back.
    /// Used by ExchangeRateResolver for fallback date iteration.
    /// </summary>
    public static IEnumerable<DateOnly> WalkBackward(
        DateOnly fromDate, HolidayConf holidays, int maxLookbackDays = 10);
}
```

**Behavior**:
- `WalkBackward` starts from `fromDate.AddDays(-1)` and steps backward day by day
- Only yields dates where `IsBusinessDay(date, holidays)` returns true
- Stops after `maxLookbackDays` calendar days from `fromDate`
- Throws `DomainException` if `holidays` is null or `maxLookbackDays < 1`

---

### 4. `NbsWebScraper` — New Infrastructure Service

**Location**: `src/Rentier.Infrastructure/ExchangeRates/NbsWebScraper.cs`

```csharp
namespace Rentier.Infrastructure.ExchangeRates;

/// <summary>
/// Fetches exchange rates from the NBS web application HTML page.
/// Implements IExchangeRateFetcher as a fallback source when the ASMX service is unavailable.
/// Uses AngleSharp for HTML parsing.
/// </summary>
public sealed class NbsWebScraper : IExchangeRateFetcher
{
    public NbsWebScraper(HttpClient http, IExchangeRateCacheRepository cache);

    public async Task<Result<ExchangeRate, Error>> FetchRateAsync(
        DateOnly date, string currency, CancellationToken ct = default);
}
```

**URL format**: `https://webappcenter.nbs.rs/ExchangeRateWebApp/ExchangeRate/IndexByDate?isSearchExecuted=true&Date={DD.MM.YYYY.}&ExchangeRateListTypeID=1`

**Parsing contract**:
- Extract all `<tr>` rows from the exchange rate table
- Each data row has 6 `<td>` cells: code, isoNum, country, unit, buyingRate, sellingRate
- Middle rate = `(buying + selling) / 2m`
- Parse decimal values using Serbian locale (comma separator): `decimal.Parse(value, NumberStyles.Any, new CultureInfo("sr-Latn-RS"))`
- Rate to RSD = `middleRate / unit`

**Error codes**:
- `NBS_SCRAPE_ERROR`: HTML parsing failed (unexpected structure, missing table, parse exception)
- `RATE_NOT_FOUND`: Page loaded successfully but no rate data (weekend/holiday)
- `NBS_HTTP_ERROR`: HTTP request failed (timeout, 5xx, etc.)
- `UNSUPPORTED_CURRENCY`: Currency not in supported set (validated before HTTP call)

**Caching**: Same as `NbsExchangeRateFetcher` — batch-caches all parsed rates for the date via `IExchangeRateCacheRepository.SaveBatchAsync()`.

---

### 5. `CompositeExchangeRateFetcher` — New Infrastructure Decorator

**Location**: `src/Rentier.Infrastructure/ExchangeRates/CompositeExchangeRateFetcher.cs`

```csharp
namespace Rentier.Infrastructure.ExchangeRates;

/// <summary>
/// Composite IExchangeRateFetcher that tries the primary fetcher (ASMX) first,
/// then falls back to the secondary fetcher (HTML scraper) on failure.
/// Transparent to callers — same IExchangeRateFetcher interface.
/// </summary>
public sealed class CompositeExchangeRateFetcher : IExchangeRateFetcher
{
    public CompositeExchangeRateFetcher(
        NbsExchangeRateFetcher primary,
        NbsWebScraper secondary);

    public async Task<Result<ExchangeRate, Error>> FetchRateAsync(
        DateOnly date, string currency, CancellationToken ct = default);
}
```

**Fallback rules**:
- Try primary (`NbsExchangeRateFetcher`) first
- If primary returns `NBS_HTTP_ERROR` → try secondary (`NbsWebScraper`)
- If primary returns `RATE_NOT_FOUND` → return immediately (both sources would agree — no rate exists for that date)
- If primary returns `NBS_PARSE_ERROR` → try secondary (primary data may be corrupt)
- If primary returns `UNSUPPORTED_CURRENCY` → return immediately (not source-dependent)
- If secondary also fails → return composite error with both failure details

**DI registration**:
```csharp
// Named HttpClient registrations for each fetcher
services.AddHttpClient<NbsExchangeRateFetcher>();
services.AddHttpClient<NbsWebScraper>();

// Register composite as the IExchangeRateFetcher
services.AddTransient<IExchangeRateFetcher, CompositeExchangeRateFetcher>();

// Register ExchangeRateResolver in Application layer
services.AddTransient<ExchangeRateResolver>();
```

---

### 6. `ProcessReportsResult` — Updated DTO

**Location**: `src/Rentier.Application/DTOs/ProcessReportsResult.cs`

```csharp
namespace Rentier.Application.DTOs;

public sealed record ProcessReportsResult(
    int FilingsCreated,
    int ReportsProcessed,
    int ReportsErrored,
    int ReportsPartialError,
    IReadOnlyList<FilingCreationError> EventErrors);
```

---

### 7. `ProcessReportsCommandHandler` — Updated Behavior

**Key changes to handler logic**:

1. **Constructor**: Inject `ExchangeRateResolver` instead of using `IExchangeRateFetcher` directly
2. **`BuildRateProvider`**: Refactored to use `ExchangeRateResolver.ResolveAsync()` with holiday and provenance tracking
3. **Per-event error handling**: Catch failures as structured `FilingCreationError` records instead of strings
4. **Post-iteration status**: After processing all events in a report, determine status:
   - `totalEvents > 0 && failedEvents == 0` → `Processed`
   - `totalEvents > 0 && failedEvents > 0 && succeededEvents > 0` → `PartialError`
   - `totalEvents > 0 && succeededEvents == 0` → `Error`
   - `totalEvents == 0` → `Processed` (empty report)
5. **Provenance propagation**: Pass `RateResolution.SourceDate` and `RateResolution.SourceType` to `Filing.CreateFromIncome()`

**Rate provider signature change**:
```csharp
// Before: Func<DateOnly, string, Task<ExchangeRate>>
// After:  Func<DateOnly, string, Task<RateResolution>>
```

The rate provider returns `RateResolution` so the handler can extract both the rate (for tax calculation) and provenance (for filing metadata).

---

## Component Dependency Graph

```text
ProcessReportsCommandHandler (Application)
    │
    ├── ExchangeRateResolver (Application)
    │       │
    │       ├── IExchangeRateFetcher (Application interface)
    │       │       │
    │       │       └── CompositeExchangeRateFetcher (Infrastructure)
    │       │               ├── NbsExchangeRateFetcher (Infrastructure, ASMX)
    │       │               └── NbsWebScraper (Infrastructure, HTML)
    │       │
    │       └── BusinessDayResolver (Domain, static)
    │
    ├── TaxCalculationService (Domain, static)
    │
    └── FilingDeadlineCalculator (Domain, static)
```
