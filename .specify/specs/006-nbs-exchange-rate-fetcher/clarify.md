# Feature 006 — NBS Exchange Rate Fetcher: Clarifications

**Status**: Resolved  
**Date**: 2026-04-07  
**Feature**: NBS exchange rate fetching with SQLite cache  
**Method**: Autonomous resolution

---

## Q1 — NBS API endpoint and response format

**Decision**: Use the NBS XML web service at:
```
https://webservices.nbs.rs/CommunicationOfficeService1_3/ExchangeRateXmlService.asmx/GetAllExchangeRates?InputDate={MM/DD/YYYY}&CurrencyCodeCo=0
```
`CurrencyCodeCo=0` returns all currencies. Date format: `MM/DD/YYYY`.

The response is XML with the following structure:
```xml
<ArrayOfExchangeRateXml xmlns="...">
  <ExchangeRateXml>
    <CurrencyCodeNumeric>978</CurrencyCodeNumeric>
    <CurrencyCodeCo>EUR</CurrencyCodeCo>
    <Unit>1</Unit>
    <BuyingRate>117.2150</BuyingRate>
    <Middle_Rate>117.5952</Middle_Rate>
    <SellingRate>117.9754</SellingRate>
  </ExchangeRateXml>
  ...
</ArrayOfExchangeRateXml>
```

Effective RSD rate = `Middle_Rate / Unit` (decimal division). Parsed with `System.Xml.Linq` (XDocument.Parse). No extra NuGet packages needed.

---

## Q2 — ExchangeRate value object as EF entity

**Decision**: The existing `ExchangeRate` record (in Domain/ValueObjects) is used directly as the EF entity with a composite primary key `(Date, Currency)`. EF8 can hydrate records using their primary constructors. The EF configuration lives in `ExchangeRateCacheConfiguration.cs` in Infrastructure. No new entity class is needed. The `DbSet<ExchangeRate>` on `AppDbContext` satisfies the user's requirement. Table name: `ExchangeRateCache`.

Composite key: `HasKey(e => new { e.Date, e.Currency })`.
EF8 maps `DateOnly` natively for SQLite. `decimal` is stored as TEXT (SQLite) or REAL — use `decimal(18,6)` precision via `HasPrecision(18, 6)`.

---

## Q3 — ExchangeRate record mutability for EF

**Decision**: `ExchangeRate` uses `init` setters on all properties. EF8 can set `init` properties during materialization. However, the existing constructor validates `rateToRsd > 0` — EF8 will use the constructor if parameter names match (they do: `date`, `currency`, `rateToRsd`). No changes to `ExchangeRate.cs` needed.

---

## Q4 — IExchangeRateFetcher interface placement

**Decision**: New file `src/Rentier.Application/Interfaces/IExchangeRateFetcher.cs`.
```csharp
public interface IExchangeRateFetcher
{
    Task<Result<ExchangeRate, Error>> FetchRateAsync(DateOnly date, string currency, CancellationToken ct = default);
}
```
Returns `Result<ExchangeRate, Error>` — not throws — consistent with project patterns.

---

## Q5 — Cache-first lookup strategy

**Decision**: The `NbsExchangeRateFetcher` implementation:
1. Calls `IExchangeRateCacheRepository.GetAsync(date, currency)` 
2. If non-null: return `Result.Success(cached)` immediately (no HTTP call)
3. If null: fetch from NBS XML API for the given date (fetches ALL currencies in one call)
4. Parse XML, find requested currency by `CurrencyCodeCo`
5. If not found in response: return `Result.Failure(new Error("RATE_NOT_FOUND", ...))`
6. Compute `rateToRsd = Middle_Rate / Unit`; create `new ExchangeRate(date, currency, rateToRsd)`
7. Persist via `IExchangeRateCacheRepository.SaveAsync(rate)`
8. Return `Result.Success(rate)`

**Batch caching optimisation**: When fetching NBS for one currency, we receive ALL currencies in the response. Cache all of them via `SaveBatchAsync` (not just the requested one) to avoid repeat HTTP calls for the same date/different currencies.

---

## Q6 — Currency code validation

**Decision**: Validate that the requested `currency` is in the supported list before making the HTTP call. Supported currencies (ISO 4217): EUR, USD, GBP, CHF, AUD, CAD, CZK, DKK, HUF, JPY, NOK, PLN, SEK, TRY, AED. If unsupported: return `Result.Failure(new Error("UNSUPPORTED_CURRENCY", ...))`.

---

## Q7 — Weekends and holidays (NBS does not publish rates)

**Decision**: NBS does not publish rates on weekends or Serbian public holidays. If the API returns an empty list or the date has no data, return `Result.Failure(new Error("RATE_NOT_FOUND", $"No NBS exchange rate found for {date:yyyy-MM-dd}"))`. The caller is responsible for date adjustment (future feature). This feature does NOT do date roll-back automatically.

---

## Q8 — Unit tests with mocked HTTP

**Decision**: Use `HttpMessageHandler` mocking pattern:
```csharp
var handler = new FakeHttpMessageHandler(xmlResponse);
var client = new HttpClient(handler);
```
Where `FakeHttpMessageHandler` is a test-local `DelegatingHandler` or subclass of `HttpMessageHandler` that returns a pre-canned `HttpResponseMessage`. No Moq or extra NuGet needed — just a simple `HttpMessageHandler` subclass.

---

## Q9 — Integration tests

**Decision**: One or more tests tagged `[Trait("Category","Integration")]` that make a real HTTP call to the NBS web service. These tests:
- Are not skipped by default (no `[Skip]`)
- Fetch a known historical date (e.g., 2024-01-15, a Monday) for EUR
- Assert `IsSuccess == true` and `rate.RateToRsd > 0`
- Are in `Rentier.Infrastructure.Tests`
- The CI pipeline should exclude them with `--filter Category!=Integration` (note this in quickstart.md)

---

## Q10 — No CQRS wrapper needed

**Decision**: `IExchangeRateFetcher` is a service interface, not a CQRS command/query. It will be called directly by future Application handlers (e.g., tax calculation). No `FetchRateCommand`/`FetchRateQuery` records are created in this feature. This keeps the interface simple and composable.

---

## Q11 — IExchangeRateCacheRepository registration

**Decision**: Register `IExchangeRateCacheRepository → ExchangeRateCacheRepository` and `IExchangeRateFetcher → NbsExchangeRateFetcher` in `InfrastructureServiceExtensions`. Use `AddHttpClient<NbsExchangeRateFetcher>()` for proper socket management. `IExchangeRateFetcher` is registered as the typed client interface: `services.AddHttpClient<IExchangeRateFetcher, NbsExchangeRateFetcher>()`.

---

## Q12 — SaveBatchAsync duplicate handling

**Decision**: `SaveBatchAsync` should upsert (insert or update) — if a rate for `(Date, Currency)` already exists, update it. In EF SQLite, use `AddRange` + `SaveChanges` with `ExecuteUpdate` or rely on EF's `Upsert` via change tracking. Simplest: use `_db.Database.ExecuteSqlRaw("INSERT OR REPLACE INTO ...")` or individually check+insert per rate. Best: iterate, use `AddOrUpdate` pattern: find existing, if found update `RateToRsd`, if not add. Actually since EF8 has composite key tracking: use `_db.Set<ExchangeRate>().Find(date, currency)` → if exists update, else add.

---

## Architecture Decisions

### AD-1: File locations
- `src/Rentier.Application/Interfaces/IExchangeRateFetcher.cs` — NEW
- `src/Rentier.Infrastructure/ExchangeRates/NbsExchangeRateFetcher.cs` — NEW
- `src/Rentier.Infrastructure/Repositories/ExchangeRateCacheRepository.cs` — NEW
- `src/Rentier.Infrastructure/Persistence/Configurations/ExchangeRateCacheConfiguration.cs` — NEW
- `tests/Rentier.Infrastructure.Tests/ExchangeRateCacheRepositoryTests.cs` — NEW (unit/integration)
- `tests/Rentier.Infrastructure.Tests/NbsExchangeRateFetcherTests.cs` — NEW (unit + integration)

### AD-2: Supported currencies constant
```csharp
public static readonly IReadOnlySet<string> SupportedCurrencies =
    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "EUR","USD","GBP","CHF","AUD","CAD","CZK","DKK","HUF","JPY","NOK","PLN","SEK","TRY","AED" };
```
Define as `public static` on `NbsExchangeRateFetcher` so tests can access it.

### AD-3: XML parsing
Use `System.Xml.Linq.XDocument.Parse(xml)`. Namespace-aware query:
```csharp
var ns = XNamespace.Get("...");
var elements = doc.Descendants(ns + "ExchangeRateXml");
```
Or use `LocalName` comparison if namespace varies.

### AD-4: EF Migration
`0006_ExchangeRateCache` — adds `ExchangeRateCache` table with composite PK.

---

## Assumptions
1. `ExchangeRate` record needs no code changes — EF8 handles `init` setters.
2. No Desktop UI in this feature — it's a pure service consumed by future features.
3. No `ICommandHandler`/`IQueryHandler` wrappers — `IExchangeRateFetcher` is used directly.
4. `decimal` precision: `HasPrecision(18, 6)` for `RateToRsd` in EF config.
5. `Currency` stored as `VARCHAR(3)` (uppercase ISO 4217 code), max 10 chars.
6. NBS API returns rates for the requested date; if date falls on weekend/holiday, response may be empty or for a different date — we return not-found error.
7. `HttpClient` timeout: default 30s (no custom timeout needed).
8. The integration test uses a known Monday date (2024-01-15) — stable historical data.
9. No retry logic in this feature — transient errors propagate as `Result.Failure`.
10. `SaveBatchAsync` upserts all rates from a single NBS API call for a given date.
