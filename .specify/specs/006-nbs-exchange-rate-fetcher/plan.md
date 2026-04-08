# Implementation Plan: NBS Exchange Rate Fetcher

**Branch**: `feature/006-nbs-exchange-rate-fetcher` | **Date**: 2026-04-07 | **Spec**: `specs/006-nbs-exchange-rate-fetcher/spec.md`  
**Input**: Feature specification from `.specify/specs/006-nbs-exchange-rate-fetcher/spec.md`

---

## Summary

Feature 006 delivers a **cache-first NBS exchange rate fetcher** for converting foreign-currency amounts to RSD during PP-OPO tax calculation. When a rate is requested for a `(date, currency)` pair the service first consults the local SQLite cache (`ExchangeRateCache` table). On a cache miss it calls the NBS XML web service, parses all 15 returned currency rates in one HTTP round-trip, batch-upserts all of them, and returns the requested rate. No UI surface is introduced; this is a pure Infrastructure/Application service consumed by future tax-calculation handlers.

---

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: EF Core 8 (SQLite), `System.Xml.Linq` (BCL), `System.Net.Http.HttpClient` (BCL)  
**Storage**: SQLite — new table `ExchangeRateCache` with composite PK `(Date, Currency)`  
**Testing**: xUnit + FluentAssertions + NSubstitute; integration tests tagged `[Trait("Category","Integration")]`  
**Target Platform**: Cross-platform desktop (Windows / macOS) — service layer only  
**Project Type**: Desktop application — service feature (no UI layer)  
**Performance Goals**: Single HTTP call per date (batch caches all 15 currencies); subsequent lookups are local SQLite reads  
**Constraints**: `decimal` for all rates; `DateOnly` for dates; no retry logic in v1; NBS does not publish rates for weekends/holidays — return `RATE_NOT_FOUND`  
**Scale/Scope**: Up to ~250 unique trading dates per year × 15 currencies = ~3 750 rows/year — negligible SQLite load

---

## Constitution Check

- [x] **I. Clean Architecture** — `IExchangeRateFetcher` defined in `Rentier.Application`; `NbsExchangeRateFetcher` and `ExchangeRateCacheRepository` implemented in `Rentier.Infrastructure`; no Domain I/O; no Desktop layer involvement.
- [x] **II. Local-First Security** — exchange rates stored in local SQLite; only outbound call is to the approved NBS endpoint (`webservices.nbs.rs`); no cloud sync or telemetry.
- [x] **III. Financial & Temporal Correctness** — `RateToRsd` is `decimal`; `Date` is `DateOnly`; `Middle_Rate / Unit` computed in `decimal` arithmetic.
- [x] **IV. Async & UI Responsiveness** — `FetchRateAsync`, `GetAsync`, `SaveBatchAsync` are all `async Task<T>`; no `.Result`/`.Wait()` allowed.
- [x] **V. Specification-Driven Quality Gates** — mapped to approved spec; unit tests for fetcher logic and repo upsert; integration test for real NBS call; no new compiler warnings.

No constitution violations. No Complexity Tracking entries required.

---

## Project Structure

### Documentation (this feature)

```text
.specify/specs/006-nbs-exchange-rate-fetcher/
├── plan.md              ← this file
├── research.md          ← Phase 0
├── data-model.md        ← Phase 1
├── quickstart.md        ← Phase 1
├── contracts/
│   ├── IExchangeRateFetcher.cs     ← interface contract
│   └── NbsFetcherDesign.md         ← algorithm & element mapping
└── tasks.md             ← Phase 2 (/speckit.tasks — NOT created here)
```

### Source Code Changes

```text
src/
├── Rentier.Application/
│   └── Interfaces/
│       └── IExchangeRateFetcher.cs              ← NEW
├── Rentier.Infrastructure/
│   ├── ExchangeRates/
│   │   └── NbsExchangeRateFetcher.cs            ← NEW
│   ├── Repositories/
│   │   └── ExchangeRateCacheRepository.cs       ← NEW
│   ├── Persistence/
│   │   ├── AppDbContext.cs                      ← MODIFIED (add DbSet<ExchangeRate>)
│   │   ├── Configurations/
│   │   │   └── ExchangeRateCacheConfiguration.cs ← NEW
│   │   └── Migrations/
│   │       └── 0006_ExchangeRateCache.*         ← NEW (EF migration)
│   └── InfrastructureServiceExtensions.cs       ← MODIFIED (2 new registrations)

tests/
└── Rentier.Infrastructure.Tests/
    ├── ExchangeRateCacheRepositoryTests.cs      ← NEW
    └── NbsExchangeRateFetcherTests.cs           ← NEW
```

**Structure Decision**: Single-project service feature layered over the existing Clean Architecture. No new projects added. The `ExchangeRates/` sub-folder under Infrastructure mirrors the existing `Scraping/` folder pattern for focused HTTP-calling services.

---

## NBS API

**Endpoint**:
```
GET https://webservices.nbs.rs/CommunicationOfficeService1_3/ExchangeRateXmlService.asmx/GetAllExchangeRates
    ?InputDate={MM/dd/yyyy}
    &CurrencyCodeCo=0
```

- `CurrencyCodeCo=0` returns **all** currencies for the date.
- Date format: **MM/dd/yyyy** (US format).
- Returns XML `<ArrayOfExchangeRateXml>` with 0–15 `<ExchangeRateXml>` children.
- Empty response (0 children) = weekend / Serbian holiday → `RATE_NOT_FOUND`.

**Rate formula**: `RateToRsd = Middle_Rate / Unit`

**Supported currencies** (15):  
`EUR, USD, GBP, CHF, AUD, CAD, CZK, DKK, HUF, JPY, NOK, PLN, SEK, TRY, AED`

---

## XML Parsing Strategy

Use `System.Xml.Linq.XDocument.Parse(xml)`. The root element carries a namespace; use `LocalName` comparison to avoid namespace binding fragility:

```csharp
var doc = XDocument.Parse(xmlContent);
var entries = doc.Descendants()
    .Where(e => e.Name.LocalName == "ExchangeRateXml");

foreach (var entry in entries)
{
    var code        = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "CurrencyCodeCo")?.Value;
    var unit        = decimal.Parse(entry.Elements().First(e => e.Name.LocalName == "Unit").Value,
                          CultureInfo.InvariantCulture);
    var middleRate  = decimal.Parse(entry.Elements().First(e => e.Name.LocalName == "Middle_Rate").Value,
                          CultureInfo.InvariantCulture);
    var rateToRsd   = middleRate / unit;
    // filter to SupportedCurrencies, construct ExchangeRate, add to batch
}
```

---

## Cache Strategy

1. `GetAsync(date, currency)` — point lookup; returns `null` on miss.
2. On miss: `FetchRateAsync` calls NBS → receives **all** currencies for that date.
3. All rates are passed to `SaveBatchAsync` (upsert) immediately.
4. The originally requested rate is returned from the parsed batch (not re-queried from DB).

**Upsert implementation** (`SaveBatchAsync`):

```csharp
foreach (var rate in rates)
{
    var existing = await _db.Set<ExchangeRate>().FindAsync([rate.Date, rate.Currency], ct);
    if (existing != null)
        _db.Entry(existing).CurrentValues.SetValues(rate);
    else
        _db.Set<ExchangeRate>().Add(rate);
}
await _db.SaveChangesAsync(ct);
```

> Note: EF8 `SetValues` on a record with `init` setters is supported. An alternative `INSERT OR REPLACE` SQLite approach is documented in `research.md`.

---

## DI Registration

In `InfrastructureServiceExtensions.AddInfrastructureServices`:

```csharp
services.AddTransient<IExchangeRateCacheRepository, ExchangeRateCacheRepository>();
services.AddHttpClient<IExchangeRateFetcher, NbsExchangeRateFetcher>();
```

- `AddHttpClient<IExchangeRateFetcher, NbsExchangeRateFetcher>()` registers the typed client and injects `HttpClient` via constructor.
- `AddTransient` is consistent with existing repository registrations in the root `ServiceProvider`.

---

## Error Codes

| Code | Condition |
|------|-----------|
| `UNSUPPORTED_CURRENCY` | Requested currency not in `SupportedCurrencies` set |
| `RATE_NOT_FOUND` | NBS returned empty response (weekend/holiday) or currency absent from response |
| `NBS_HTTP_ERROR` | HTTP call returned non-2xx status |
| `NBS_PARSE_ERROR` | XML could not be parsed or required elements missing |

---

## Test Strategy

### Unit Tests (`NbsExchangeRateFetcherTests.cs`)

| Test | Setup | Assert |
|------|-------|--------|
| `FetchRateAsync_CacheHit_ReturnsRateWithoutHttpCall` | `GetAsync` returns non-null; `FakeHandler` never called | `IsSuccess`, correct rate, HTTP call count = 0 |
| `FetchRateAsync_CacheMiss_ParsesXmlAndCachesAll` | `GetAsync` null; handler returns valid XML with 3 currencies | `IsSuccess`; `SaveBatchAsync` called with 3 rates |
| `FetchRateAsync_UnsupportedCurrency_ReturnsFailure` | — | `IsFailure`, code = `UNSUPPORTED_CURRENCY` |
| `FetchRateAsync_EmptyXmlResponse_ReturnsRateNotFound` | Handler returns XML with 0 entries | `IsFailure`, code = `RATE_NOT_FOUND` |
| `FetchRateAsync_HttpError_ReturnsNbsHttpError` | Handler returns 500 | `IsFailure`, code = `NBS_HTTP_ERROR` |
| `FetchRateAsync_MalformedXml_ReturnsParseError` | Handler returns `"not xml"` | `IsFailure`, code = `NBS_PARSE_ERROR` |
| `FetchRateAsync_RateNotInResponse_ReturnsRateNotFound` | Valid XML but missing requested currency | `IsFailure`, code = `RATE_NOT_FOUND` |

### Unit Tests (`ExchangeRateCacheRepositoryTests.cs`)

| Test | Setup | Assert |
|------|-------|--------|
| `GetAsync_ExistingRate_ReturnsRate` | Seed rate for `(2024-01-15, EUR)` | Not null, correct `RateToRsd` |
| `GetAsync_MissingRate_ReturnsNull` | Empty DB | Returns null |
| `SaveAsync_NewRate_PersistsSingleRate` | Empty DB | DB contains 1 row |
| `SaveBatchAsync_NewRates_PeristsAll` | Empty DB; batch of 3 | DB contains 3 rows |
| `SaveBatchAsync_DuplicateKey_Upserts` | Seed EUR@100; batch with EUR@117 | DB has EUR with updated rate 117 |
| `GetByDateRangeAsync_FiltersCorrectly` | Seed 5 rates across 3 dates | Returns only rates within range |

### Integration Tests (`NbsExchangeRateFetcherTests.cs`)

```csharp
[Trait("Category", "Integration")]
public class NbsIntegrationTests
{
    [Fact]
    public async Task FetchRateAsync_RealNbs_ReturnsPositiveEurRate()
    {
        // Known Monday: 2024-01-15
        var date = new DateOnly(2024, 1, 15);
        // InMemoryCacheStub always returns null
        var fetcher = new NbsExchangeRateFetcher(new HttpClient(), new InMemoryCacheStub());
        var result = await fetcher.FetchRateAsync(date, "EUR");
        result.IsSuccess.Should().BeTrue();
        result.Value.RateToRsd.Should().BeGreaterThan(0);
    }
}
```

CI filter to exclude integration tests: `--filter "Category!=Integration"`

---

## EF Migration

- Name: `0006_ExchangeRateCache`
- Creates table `ExchangeRateCache`
- Composite PK: `(Date TEXT NOT NULL, Currency TEXT NOT NULL)`
- Column `RateToRsd REAL NOT NULL` (EF maps `decimal` → SQLite REAL; precision metadata stored in model snapshot)
- No foreign keys; standalone lookup table

---

## Phase Summary

| Phase | Output | Status |
|-------|--------|--------|
| 0 — Research | `research.md` | ✅ Complete |
| 1 — Design | `data-model.md`, `contracts/`, `quickstart.md` | ✅ Complete |
| 2 — Tasks | `tasks.md` | ⏳ Pending (`/speckit.tasks`) |
