# Research: Holiday Configuration (Feature 003)

**Branch**: `feature/003-holiday-configuration`  
**Date**: 2026-04-06

All NEEDS CLARIFICATION items from the Technical Context are resolved below.

---

## Decision 1: HTML Parser — AngleSharp vs HtmlAgilityPack

**Decision**: Use **AngleSharp 1.x** (`AngleSharp` NuGet package).

**Rationale**:
- Pure .NET 5+ HTML5 parser; no native dependencies; works on Windows and macOS without
  any additional tooling.
- Full CSS selector support via `IDocument.QuerySelectorAll(selector)` — required to filter
  `tr.noshow` and `tr.js-holiday-private` hidden rows efficiently.
- MIT licence — no LGPL concerns (HtmlAgilityPack is LGPL 2.0 with uncertainty about linking).
- AngleSharp conforms to the WHATWG HTML5 parsing specification, giving reliable results on
  real-world HTML that may not be well-formed XML.
- Version 1.x is the current stable release (2024); actively maintained.

**Alternatives considered**:

| Library | Licence | CSS selectors | .NET 8 native | Decision |
|---------|---------|--------------|--------------|---------|
| **AngleSharp 1.x** | MIT | ✅ Full | ✅ | ✅ **Selected** |
| HtmlAgilityPack 1.11 | LGPL 2.0 | ❌ XPath only | ✅ | ❌ Licence concern + no CSS selectors |
| `System.Xml.Linq` | MIT (BCL) | ❌ Not applicable | ✅ | ❌ Cannot parse HTML5 reliably |
| PuppeteerSharp | MIT | ✅ | ✅ | ❌ Heavyweight (Chromium headless); not needed |

**NuGet reference** (add to `Rentier.Infrastructure.csproj` only):
```xml
<PackageReference Include="AngleSharp" Version="1.*" />
```

---

## Decision 2: timeanddate.com DOM Structure

**Decision**: Target URL `https://www.timeanddate.com/holidays/serbia/{year}?hol=1`
(parameter `hol=1` returns public holidays only).

**Observed DOM structure** (as of 2024–2025 scrapes):

```html
<table id="holidays-table" class="table table-sm">
  <thead>…</thead>
  <tbody>
    <tr>
      <td class="date npad">Jan 1</td>
      <td class="name"><a href="…">New Year's Day</a></td>
      <td>…</td>
    </tr>
    <tr class="noshow">          <!-- hidden row (e.g., observance / private) -->
      <td class="date npad">Jan 3</td>
      <td class="name">…</td>
    </tr>
    <tr class="js-holiday-private">   <!-- another hidden variant -->
      …
    </tr>
  </tbody>
</table>
```

**Scraper strategy**:
1. Select all `<tr>` elements inside `#holidays-table tbody` (or `table.table tbody`).
2. Exclude rows where `tr.classList.Contains("noshow")` or `tr.classList.Contains("js-holiday-private")`.
3. From each remaining row:
   - Date: `tr.QuerySelector("td.date")?.TextContent.Trim()` → parse to `DateOnly`.
   - Name: `tr.QuerySelector("td.name")?.TextContent.Trim()`.

**Date parsing**: The date text uses format `"MMM d"` (e.g., `"Jan 1"`). Since the year is
known from the command parameter, construct `DateOnly.ParseExact($"{dateText} {year}", "MMM d yyyy", CultureInfo.InvariantCulture)`.

**Error handling**:
- HTTP status != 2xx → `Result.Failure("HTTP {statusCode}: {reasonPhrase}")`.
- Parse error on any row → skip the row and log warning; do not abort the entire import.
- Zero rows returned → `Result.Failure("No holidays found for year {year}")`.

**CSS selector** (AngleSharp):
```csharp
var rows = document.QuerySelectorAll("table.table tbody tr")
                   .Where(tr => !tr.ClassList.Contains("noshow")
                             && !tr.ClassList.Contains("js-holiday-private"));
```

---

## Decision 3: AddHttpClient Pattern for TimeAndDateHolidayScraper

**Decision**: Register via `services.AddHttpClient<TimeAndDateHolidayScraper>()`.

**Rationale**:
- `AddHttpClient<T>()` injects a properly managed `HttpClient` into the typed client
  constructor, handling socket pool lifecycle automatically (avoids socket exhaustion).
- Consistent with `System.Net.Http.HttpClient` approach already used in the project (NBS
  scraper pattern).
- `TimeAndDateHolidayScraper` is registered as a typed `HttpClient` client; the DI container
  simultaneously registers it as `IHolidayImporter`.

**Registration** (in `InfrastructureServiceExtensions.cs`):
```csharp
services.AddHttpClient<TimeAndDateHolidayScraper>();
services.AddTransient<IHolidayImporter, TimeAndDateHolidayScraper>();
```

> **Important**: `HttpClient` is injected into Infrastructure only. Application layer defines
> `IHolidayImporter` and knows nothing about `HttpClient`.

---

## Decision 4: EF Core 8 DateOnly Support with SQLite

**Decision**: No `HasConversion` required for `DateOnly` in EF Core 8 + SQLite provider.

**Rationale**:
- EF Core 8 introduced native `DateOnly` and `TimeOnly` support for the SQLite provider
  (see [EF Core 8 What's New](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew#dateonlyand-timeonlysupport)).
- Values are stored as TEXT in `"yyyy-MM-dd"` format; EF materialises them correctly.
- No explicit `HasConversion` needed in `PublicHolidayConfiguration`.

---

## Decision 5: Singleton HolidayYearRange Enforcement

**Decision**: Singleton enforced by Application layer + fixed `Id = 1` convention; no DB
unique constraint beyond the PK.

**Rationale**:
- A SQLite `UNIQUE` constraint on a constant column is not idiomatic. The PK `Id = 1`
  is sufficient — inserting a second row with `Id = 1` would violate the PK constraint.
- Application layer is authoritative: `SaveHolidayConfCommandHandler` always upserts `Id = 1`.
- Consistent with `TaxpayerProfile` singleton pattern from feature 002.

---

## Decision 6: AngleSharp IBrowsingContext Lifecycle

**Decision**: Create `IBrowsingContext` per-request inside `ImportAsync`; do not inject it.

**Rationale**:
- `BrowsingContext.New(Configuration.Default.WithDefaultLoader())` is lightweight and
  thread-safe to create per-request.
- Injecting a shared `IBrowsingContext` would create thread-safety issues if `ImportAsync`
  is called concurrently (unlikely in desktop, but still undesirable).
- Keeps `TimeAndDateHolidayScraper` constructor simple: only `HttpClient` is injected.

**Scraper constructor**:
```csharp
public sealed class TimeAndDateHolidayScraper(HttpClient httpClient) : IHolidayImporter
{
    public async Task<Result<IReadOnlyList<HolidayEntryDto>>> ImportAsync(
        int year, CancellationToken cancellationToken = default)
    {
        var url = $"https://www.timeanddate.com/holidays/serbia/{year}?hol=1";
        var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return Result.Failure<IReadOnlyList<HolidayEntryDto>>(
                $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(html), cancellationToken);
        // … parse and return
    }
}
```

---

## Decision 7: Replace-All vs Differential Save

**Decision**: Replace-all strategy (truncate + insert) for `SaveHolidaysAsync`.

**Rationale**:
- Holiday count is bounded (≤ ~50 rows/year; typically 8–15 per year).
- Differential diffing (detect add/update/delete) adds significant complexity for negligible
  performance gain at this scale.
- Full replace avoids orphaned rows when the user changes dates in existing entries.
- Consistent with decision in clarify.md (A-005).

**Implementation**:
```csharp
await context.PublicHolidays.ExecuteDeleteAsync(ct);
context.PublicHolidays.AddRange(newEntities);
await context.SaveChangesAsync(ct);
```

---

## Decision 8: Constitution Amendment CA-EXT-001

**Decision**: `timeanddate.com` outbound HTTP is approved as a user-initiated, on-demand
exception to the Local-First constraint.

**Documented in**: `.specify/specs/003-holiday-configuration/clarify.md` (A-009, CA-EXT-001).

**Guard**: Scraper is ONLY invoked when the user explicitly clicks "Import from Web".
No background polling, no startup calls, no scheduled tasks.
