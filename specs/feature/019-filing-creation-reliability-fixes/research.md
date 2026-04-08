# Research: Filing Creation Reliability Fixes

**Feature**: 019-filing-creation-reliability-fixes  
**Date**: 2025-07-16  
**Status**: Complete

## Research Topics

### R-001: NBS Web App HTML Structure for Exchange Rate Scraping

**Question**: What is the HTML structure of the NBS ExchangeRateWebApp and how should it be parsed?

**Findings**:
- **URL Pattern**: `https://webappcenter.nbs.rs/ExchangeRateWebApp/ExchangeRate/IndexByDate?isSearchExecuted=true&Date={DD.MM.YYYY.}&ExchangeRateListTypeID=1`
  - Note: Date format is `DD.MM.YYYY.` with trailing dot (Serbian date format)
  - `ExchangeRateListTypeID=1` selects the official exchange rate list
- **Framework**: Bootstrap 5.3.3 HTML page
- **Table structure**: Standard HTML `<table>` with `<tr>/<td>` cells, no `<thead>`, data in `<tbody>`
- **Columns** (6 per currency row):
  1. Currency code (e.g., `EUR`, `USD`, `GBP`)
  2. ISO 4217 numeric code (e.g., `978`, `840`)
  3. Country/region name (e.g., `EMU`, `USA`)
  4. Unit (typically `1`)
  5. Buying rate (kupovni kurs) — comma decimal, e.g., `117,0539`
  6. Selling rate (prodajni kurs) — comma decimal, e.g., `117,7583`
- **Middle rate derivation**: `(buyingRate + sellingRate) / 2` — equivalent to NBS official middle rate
- **Decimal format**: Serbian locale uses comma `,` as decimal separator — must parse with `CultureInfo("sr-Latn-RS")` or explicit comma-to-dot replacement
- **Empty pages**: When no rates are published (weekends/holidays), the table has no data rows — this is not a parse error

**Decision**: Parse NBS HTML using AngleSharp (already a dependency in Rentier.Infrastructure). Use CSS selector targeting table rows containing currency data. Compute middle rate as `(buying + selling) / 2m`. Use `NumberStyles.Any` with Serbian `CultureInfo` for decimal parsing.

**Rationale**: AngleSharp is already referenced (v1.*) in Rentier.Infrastructure.csproj. Adding HtmlAgilityPack would introduce a redundant dependency. AngleSharp provides a modern DOM API with CSS selector support, making table parsing straightforward.

**Alternatives considered**:
- HtmlAgilityPack: Widely used but adds another dependency when AngleSharp is already present
- Regex-based parsing: Fragile, error-prone for HTML tables
- XPath (via HtmlAgilityPack): Not needed since AngleSharp CSS selectors are sufficient

---

### R-002: AngleSharp vs HtmlAgilityPack for HTML Parsing

**Question**: Which HTML parsing library should be used for the NBS web scraper?

**Findings**:
- **AngleSharp** is already a NuGet dependency in `Rentier.Infrastructure.csproj` (version 1.*)
- It is currently used by `TimeAndDateHolidayScraper` for holiday scraping
- AngleSharp provides `BrowsingContext`, CSS selectors via `QuerySelectorAll`, and a full DOM API
- HtmlAgilityPack is **not** a current dependency

**Decision**: Use AngleSharp exclusively. No new dependency needed.

**Rationale**: Consistency with existing holiday scraper; avoids adding a redundant NuGet package; AngleSharp's CSS selector API is well-suited for table parsing.

**Alternatives considered**:
- HtmlAgilityPack: Would require adding a new NuGet dependency for functionality AngleSharp already provides
- Both side-by-side: Unnecessary complexity

---

### R-003: Business Day Calculation (Weekend + Serbian Holiday Skipping)

**Question**: How should the system walk backward from an income date to find the previous business day with a published NBS exchange rate?

**Findings**:
- NBS publishes exchange rates Monday–Friday, excluding Serbian public holidays
- The existing `FilingDeadlineCalculator` already implements forward business day skipping using `HolidayConf`
- The backward walk is the symmetric operation: given a date, find the most recent preceding date that is (1) not Saturday, (2) not Sunday, (3) not in `HolidayConf.Holidays`
- `HolidayConf` is already loaded from the database in `ProcessReportsCommandHandler` (line 43-44)
- Maximum lookback of 10 calendar days covers the longest realistic holiday+weekend block (e.g., Easter Friday→Monday + adjacent weekends = 4–6 days)

**Decision**: Create a new Domain service `BusinessDayResolver` with a static method `FindPreviousBusinessDay(DateOnly date, HolidayConf holidays, int maxLookbackDays = 10)`. This mirrors the existing `FilingDeadlineCalculator` pattern (static, pure domain, no I/O).

**Rationale**: 
- Domain logic: the concept of "previous business day" is a domain rule, not infrastructure
- Reusable: same logic applies to direct rate lookups and cross-rate USD lookups
- Testable: pure function, easily unit-tested with various holiday configurations
- Consistent: follows the static service pattern established by `FilingDeadlineCalculator` and `TaxCalculationService`

**Alternatives considered**:
- Embed in `FilingDeadlineCalculator`: Would mix forward (deadline) and backward (rate) day logic — separate concerns
- Embed in `NbsExchangeRateFetcher`: Would violate Clean Architecture — business day rules are domain logic, not infrastructure
- Inline in handler: Would duplicate logic for direct rate and cross-rate lookups

---

### R-004: Rate Provenance Storage Strategy

**Question**: Where and how should rate provenance (exact vs. fallback, source date) be stored on Filing?

**Findings**:
- Current `Filing` entity stores RSD-converted amounts but does **not** record the exchange rate used or its source date
- The spec requires: `ExchangeRateSourceDate` (DateOnly) and `ExchangeRateSourceType` (Exact/Fallback)
- Options evaluated:
  1. **New columns on Filing table**: Simple, queryable, directly auditable
  2. **Separate metadata table**: Over-engineered for two fields
  3. **JSON blob column**: Not queryable, harder to validate
- Filing is the aggregate root — provenance is Filing-level metadata (one rate per filing)

**Decision**: Add two new columns to the `Filing` entity:
- `ExchangeRateSourceDate` (`DateOnly?`) — the actual date whose rate was used (nullable for pre-existing filings)
- `ExchangeRateSourceType` (`ExchangeRateSourceType?`) — enum: `Exact = 0`, `Fallback = 1` (nullable for pre-existing filings)

Both are nullable to maintain backward compatibility with existing filings that predate this feature.

**Rationale**: Simplest approach that maintains queryability and auditability. Two columns on an existing table is minimal schema change. Nullable fields avoid migration data-fix complexity.

**Alternatives considered**:
- Separate `FilingRateProvenance` table: Over-engineered for 2 fields
- JSON metadata column: Not easily queryable for audit purposes
- Store on ExchangeRate value object: ExchangeRate is a shared cache record; provenance is per-filing context

---

### R-005: Partial Success Processing Strategy

**Question**: How should `ProcessReportsCommandHandler` accumulate partial results without aborting the entire report?

**Findings**:
- Current code already catches per-event exceptions (lines 149-152, 199-202) and accumulates errors in a `List<string>`
- However, the report is always set to `Processed` if the outer try succeeds (line 60), even if some events failed
- The current `ProcessReportsResult` only has `IReadOnlyList<string> Errors` — no structured error data
- The spec requires: PartialError status, per-event error details with entity/date/currency/amount/code/message

**Decision**: 
1. Extend `ReportStatus` enum with `PartialError = 3`
2. Replace `IReadOnlyList<string> Errors` in `ProcessReportsResult` with `IReadOnlyList<FilingCreationError>` where `FilingCreationError` is a new record containing structured error data
3. After processing all events in a report, determine status:
   - All events succeeded → `Processed`
   - Some succeeded, some failed → `PartialError`
   - All failed (and at least one event exists) → `Error`
   - No events to process → `Processed` (empty report)
4. Track per-report event counts inside `ProcessReportAsync` to make the determination

**Rationale**: Minimal change to existing per-event try/catch structure. The error accumulation pattern is already in place — just needs structured records instead of strings, and post-iteration status determination.

**Alternatives considered**:
- Abort on first failure: Contradicts partial success requirement
- Wrap each event in a Result<T,E>: Over-engineering — try/catch pattern is already established and works well
- Separate "event processing results" table: Not needed — errors are ephemeral (per-processing-run), not persisted

---

### R-006: Exchange Rate Resolver Architecture (Fallback Chain + Business Day)

**Question**: How should the rate resolution pipeline be architected to combine business day fallback with ASMX→scraper source fallback?

**Findings**:
- Current flow: `BuildRateProvider` → `IExchangeRateFetcher.FetchRateAsync(exactDate)` → cross-rate fallback
- Need to add: business day fallback (try exact date, then walk backward) AND source fallback (ASMX then scraper)
- Two orthogonal concerns: **date resolution** (which date to use) and **source resolution** (which fetcher to use)
- The date resolution is Domain logic; source resolution is Infrastructure logic

**Decision**: Two-layer architecture:
1. **Infrastructure layer** — `CompositeExchangeRateFetcher` (new): Implements `IExchangeRateFetcher`, wraps `NbsExchangeRateFetcher` (ASMX) and `NbsWebScraper` (HTML). Tries ASMX first; on failure, tries scraper. Same interface, transparent to callers.
2. **Application layer** — `ExchangeRateResolver` (new): Wraps `IExchangeRateFetcher` and adds business day fallback. Takes `HolidayConf`, tries exact date, then walks backward using `BusinessDayResolver`. Records provenance (exact vs. fallback, source date). Returns a `RateResolution` result that includes both the rate and provenance.
3. `BuildRateProvider` in handler is refactored to use `ExchangeRateResolver` instead of calling `IExchangeRateFetcher` directly.

**Rationale**:
- Clean Architecture preserved: Domain has `BusinessDayResolver`, Application has `ExchangeRateResolver`, Infrastructure has `CompositeExchangeRateFetcher` and `NbsWebScraper`
- Each layer handles its concern: Infrastructure handles HTTP/parsing, Application handles business workflow, Domain handles date rules
- `IExchangeRateFetcher` interface unchanged — `CompositeExchangeRateFetcher` is a transparent decorator
- Testable at each layer independently

**Alternatives considered**:
- Single monolithic resolver: Mixes domain rules with HTTP calls, violates Clean Architecture
- Business day logic in NbsExchangeRateFetcher: Wrong layer — date skipping is domain logic
- New `IExchangeRateResolver` interface: Considered but adds interface proliferation. Better to keep `IExchangeRateFetcher` for source access and `ExchangeRateResolver` as an Application-layer service
