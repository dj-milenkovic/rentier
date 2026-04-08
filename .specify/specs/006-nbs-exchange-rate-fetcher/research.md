# Research: NBS Exchange Rate Fetcher (Feature 006)

**Date**: 2026-04-07  
**Status**: Complete — all NEEDS CLARIFICATION resolved

---

## R1 — NBS XML Web Service

**Decision**: Use `GetAllExchangeRates` with `CurrencyCodeCo=0`  
**Rationale**: Returns all currencies in one HTTP call; avoids N-per-currency calls; stable ASMX endpoint used by the NBS public portal.  
**Alternatives considered**:
- Per-currency endpoint (`CurrencyCodeCo={code}`) — rejected; would require 15 HTTP calls per date.
- Scraping the HTML portal — rejected; fragile, maintenance burden, no structured output.

**URL template**:
```
https://webservices.nbs.rs/CommunicationOfficeService1_3/ExchangeRateXmlService.asmx/GetAllExchangeRates
    ?InputDate={date:MM/dd/yyyy}
    &CurrencyCodeCo=0
```

**Date format**: `MM/dd/yyyy` (US month-first). Confirmed via clarify.md Q1.

**Response XML structure**:
```xml
<ArrayOfExchangeRateXml xmlns="http://webservices.nbs.rs/">
  <ExchangeRateXml>
    <CurrencyCodeNumeric>978</CurrencyCodeNumeric>
    <CurrencyCodeCo>EUR</CurrencyCodeCo>
    <Unit>1</Unit>
    <BuyingRate>117.2150</BuyingRate>
    <Middle_Rate>117.5952</Middle_Rate>
    <SellingRate>117.9754</SellingRate>
  </ExchangeRateXml>
  <!-- ... up to 15 entries ... -->
</ArrayOfExchangeRateXml>
```

**Weekend / holiday behaviour**: NBS returns an empty `<ArrayOfExchangeRateXml/>` (no child elements) for dates with no published rates. The implementation MUST treat 0 entries as `RATE_NOT_FOUND`, not as an error.

---

## R2 — XDocument vs XmlDocument for XML Parsing

**Decision**: Use `System.Xml.Linq.XDocument` (LINQ to XML)  
**Rationale**:
- Already in the .NET BCL; zero additional NuGet packages.
- LINQ query syntax is concise and readable.
- `Descendants().Where(e => e.Name.LocalName == "ExchangeRateXml")` handles the NBS namespace without binding it explicitly.
- `XDocument` is immutable after parse — thread-safe for concurrent reads.

**Alternatives considered**:
- `XmlDocument` (DOM) — available but more verbose; no LINQ integration; rejected.
- `XmlReader` (streaming) — unnecessary for small NBS payloads (~2 KB); rejected.
- `System.Text.Json` / JSON endpoint — NBS does not expose a JSON endpoint; rejected.

**Namespace handling**:
```csharp
// Robust: LocalName comparison avoids namespace-prefix binding
doc.Descendants().Where(e => e.Name.LocalName == "ExchangeRateXml")
```
Preferred over `doc.Descendants(ns + "ExchangeRateXml")` because the NBS namespace URI is undocumented and could change.

---

## R3 — HttpClient Typed Client Pattern

**Decision**: Register as `services.AddHttpClient<IExchangeRateFetcher, NbsExchangeRateFetcher>()`  
**Rationale**:
- `IHttpClientFactory` manages socket pooling and `HttpMessageHandler` lifetimes; avoids socket exhaustion from `new HttpClient()`.
- Typed client receives a pre-configured `HttpClient` via constructor injection — no service-locator pattern.
- Registration is consistent with the existing `AddHttpClient<IHolidayImporter, TimeAndDateHolidayScraper>()` pattern in `InfrastructureServiceExtensions`.
- `AddHttpClient<TInterface, TImpl>()` registers the typed client as transient by default — correct for a root `ServiceProvider`.

**Alternatives considered**:
- Named client (`services.AddHttpClient("nbs")`) — requires `IHttpClientFactory` injection and `CreateClient("nbs")` calls; more boilerplate; rejected.
- Static `HttpClient` field — works but bypasses `IHttpClientFactory` lifecycle management; rejected.

---

## R4 — SQLite Upsert: EF8 Change Tracking vs INSERT OR REPLACE

**Decision**: Use EF8 change-tracking upsert (`Find` → `SetValues` or `Add`)  
**Rationale**:
- Keeps logic in C# and benefits from EF change tracking, unit-testability with in-memory providers.
- EF8 `CurrentValues.SetValues(record)` correctly sets `init` properties on records during update because EF bypasses the constructor for tracked entities.
- Simple iteration pattern; 15 rows per call — no performance concern.

**Alternatives considered**:
- `_db.Database.ExecuteSqlRaw("INSERT OR REPLACE INTO ExchangeRateCache ...")` — works and is simpler for SQLite, but bypasses EF change tracking; cannot be tested with `UseInMemoryDatabase`; rejected for unit-test compatibility.
- EF8 `BulkMerge` (EFCore.BulkExtensions) — would introduce an extra NuGet dependency; rejected (constitution: minimal dependencies).

**Implementation pattern**:
```csharp
foreach (var rate in rates)
{
    var existing = await _db.Set<ExchangeRate>()
        .FindAsync(new object[] { rate.Date, rate.Currency }, ct);
    if (existing is not null)
        _db.Entry(existing).CurrentValues.SetValues(rate);
    else
        _db.Set<ExchangeRate>().Add(rate);
}
await _db.SaveChangesAsync(ct);
```

---

## R5 — EF8 Record Hydration with `init` Setters

**Decision**: No changes to `ExchangeRate.cs` required  
**Rationale**:
- EF Core 8 can hydrate records using the primary constructor when parameter names match property names (`date`, `currency`, `rateToRsd` → `Date`, `Currency`, `RateToRsd` with conventional casing match).
- The constructor validation (`rateToRsd > 0`) will fire during hydration, which is correct behaviour — invalid data in the DB should not be silently loaded.
- `init` setters are supported for `CurrentValues.SetValues()` in EF8 because the setter is `init` only in C# source but the underlying CLR property is writable via reflection (which EF uses).

**Alternatives considered**:
- Adding a private parameterless constructor for EF — not needed in EF8 with record primary constructors.
- Switching to `set` properties — rejected; would weaken the value-object immutability guarantee.

---

## R6 — Result<T, Error> Pattern

**Decision**: Return `Result<ExchangeRate, Error>` from `FetchRateAsync`; do not throw for expected failures  
**Rationale**:
- Consistent with `ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>` patterns in the Application layer.
- Callers (future tax-calculation handlers) can pattern-match on `IsSuccess`/`IsFailure` without try-catch.
- `Error` carries a `Code` (string) and `Message` — all error codes documented in plan.md.

**Error codes**:

| Code | Trigger |
|------|---------|
| `UNSUPPORTED_CURRENCY` | `currency` not in `SupportedCurrencies` |
| `RATE_NOT_FOUND` | NBS returned 0 entries, or parsed entries don't contain requested currency |
| `NBS_HTTP_ERROR` | HTTP response is non-2xx |
| `NBS_PARSE_ERROR` | XML parse exception or missing required elements |

---

## R7 — HTTP Mocking Strategy for Unit Tests

**Decision**: Use a hand-rolled `FakeHttpMessageHandler : HttpMessageHandler`  
**Rationale**:
- No Moq or extra NuGet required; consistent with constitution's minimal-dependency rule.
- `HttpMessageHandler` subclass is the correct low-level seam for `HttpClient` testing.
- Simple; provides canned response string and status code.

```csharp
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string _response;
    private readonly HttpStatusCode _statusCode;

    internal FakeHttpMessageHandler(string response,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    { _response = response; _statusCode = statusCode; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(_statusCode)
            { Content = new StringContent(_response) });
}
```

---

## R8 — Integration Test Design

**Decision**: One integration test class in `Rentier.Infrastructure.Tests`, tagged `[Trait("Category","Integration")]`; uses a known historical Monday  
**Rationale**:
- Known date `2024-01-15` (Monday) has stable historical NBS data — will not change.
- Test uses a real `HttpClient` and an `InMemoryCacheStub` (always returns null) to exercise the full NBS HTTP path.
- CI excludes with `--filter "Category!=Integration"`; developers run manually or in a dedicated CI job.

**Alternatives considered**:
- `[Skip("Integration")]` — skips by default; developers might forget to run; rejected.
- WireMock / HTTP test server — overkill for one endpoint; rejected.
