# Feature Specification: Filing Creation Reliability Fixes

**Feature Branch**: `feature/019-filing-creation-reliability-fixes`  
**Created**: 2025-07-16  
**Status**: Draft  
**Input**: User description: "Fix filing generation failures caused by missing exchange rates."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Resilient Exchange Rate Resolution (Priority: P1)

A taxpayer processes reports containing income events on dates where the NBS did not publish exchange rates (weekends, Serbian public holidays). Today the system fails silently: the report ends in "Processed" status but creates zero filings. The user sees "created 0 filing(s)" with no explanation.

After this fix, the system automatically falls back to the most recent previous business day's exchange rate when no rate exists for the exact income date. The created filing records which date's rate was actually used (provenance), so the taxpayer can verify correctness.

**Why this priority**: This is the root cause of the observed bug. Without a fallback rate resolution strategy, every income event landing on a non-business day produces zero filings, directly blocking the user's tax filing workflow.

**Independent Test**: Can be fully tested by processing a report containing a dividend paid on a Saturday (no NBS rate published) and verifying a filing is created using Friday's rate, with provenance metadata indicating the fallback date.

**Acceptance Scenarios**:

1. **Given** an income event on a date where NBS published a rate, **When** the system resolves the exchange rate, **Then** it returns the exact-date rate and records provenance as "exact".
2. **Given** an income event on a Saturday, **When** the system resolves the exchange rate, **Then** it falls back to the preceding Friday's rate and records provenance as "fallback" with the actual source date.
3. **Given** an income event on a Sunday, **When** the system resolves the exchange rate, **Then** it falls back to the preceding Friday's rate.
4. **Given** an income event on a Serbian public holiday (e.g., Sretenje, Feb 15), **When** the system resolves the exchange rate, **Then** it falls back to the last preceding business day that is neither a weekend nor a Serbian public holiday.
5. **Given** a consecutive run of holidays and weekends (e.g., Friday is a holiday), **When** the system resolves the exchange rate, **Then** it walks backward past all non-business days until a valid rate date is found, up to a configurable maximum lookback (default: 10 days).
6. **Given** no valid rate is found within the maximum lookback window, **When** the system resolves the exchange rate, **Then** it returns an error with code `RATE_NOT_FOUND` and a message including the original date, currency, and the range of dates searched.

---

### User Story 2 - Partial Success Report Processing (Priority: P1)

When a report contains multiple income events (dividends, interest), a single rate lookup failure for one event currently causes the entire report to be marked "Processed" with potentially zero filings created. The user has no way to know which events succeeded and which failed.

After this fix, the system processes each income event independently. Filings are created for every event where rates are available. Events that fail are recorded with detailed error information. The report is marked with a new "PartialError" status when some events succeed and others fail, giving the user clear visibility.

**Why this priority**: Equally critical to P1 — even with rate fallback, some edge cases will still fail (e.g., currency not supported, rate genuinely missing for extended period). Partial success ensures users get maximum value from each processing run.

**Independent Test**: Can be fully tested by processing a report with three income events — two with valid rates and one with an unsupported currency — verifying two filings are created and the report status is "PartialError" with a specific error for the failed event.

**Acceptance Scenarios**:

1. **Given** a report with 5 income events where all rates resolve successfully, **When** the system processes the report, **Then** 5 filings are created and the report status is "Processed".
2. **Given** a report with 5 income events where 3 rates resolve and 2 fail, **When** the system processes the report, **Then** 3 filings are created, the report status is "PartialError", and 2 detailed error records are stored identifying the failing events by entity name, date, and currency.
3. **Given** a report with 5 income events where all rate lookups fail, **When** the system processes the report, **Then** 0 filings are created, the report status is "Error", and 5 detailed error records are stored.
4. **Given** a report marked as "PartialError", **When** the user views the report details, **Then** they see which income events produced filings and which failed, with actionable error messages.

---

### User Story 3 - NBS Web App Exchange Rate Scraper (Priority: P2)

The existing ASMX web service used to fetch NBS exchange rates is occasionally unavailable (service outages, maintenance). When this happens, all rate lookups fail and no filings can be created.

A new scraper that fetches rates from the NBS web application (HTML page) serves as a fallback data source. The system tries the primary ASMX service first; if it fails, it falls back to the HTML scraper. The NBS web app returns a Bootstrap HTML table with columns: currency code (OZNAKA VALUTE), currency number (ŠIFRA VALUTE), country name (NAZIV ZEMLJE), unit (VAŽI ZA), buying rate (KUPOVNI KURS), and selling rate (PRODAJNI KURS). The middle rate is derived as the average of buying and selling rates.

**Why this priority**: This is a resilience improvement that addresses a secondary failure mode. The ASMX service is the primary source and works most of the time; the scraper provides redundancy.

**Independent Test**: Can be fully tested by fetching a known date's rates via the scraper and comparing the parsed middle rate against the known NBS published rate for that date.

**Acceptance Scenarios**:

1. **Given** the NBS web app returns a valid HTML page for a requested date, **When** the scraper parses the page, **Then** it extracts all currency rates with correct currency codes, units, and middle rates (average of buying and selling rates).
2. **Given** the scraper successfully parses rates, **When** the rate is stored, **Then** it is cached in the same local store as ASMX-sourced rates.
3. **Given** the ASMX service returns an error or timeout, **When** a rate is requested, **Then** the system falls back to the HTML scraper and returns the scraped rate.
4. **Given** both the ASMX service and the HTML scraper fail, **When** a rate is requested, **Then** the system returns an error with code `RATE_NOT_FOUND` containing details about both attempted sources.
5. **Given** the HTML scraper encounters invalid or unexpected HTML structure, **When** parsing fails, **Then** the system returns an error with code `NBS_SCRAPE_ERROR` and a descriptive message.
6. **Given** the NBS web app returns a page for a date with no published rate (e.g., a holiday), **When** the scraper parses the page, **Then** it reports no rates found (not a parse error).
7. **Given** rates with decimal numbers using comma as separator (e.g., "117,0539"), **When** the scraper parses values, **Then** it correctly interprets them as decimal numbers.

---

### User Story 4 - Actionable Diagnostics in Logs (Priority: P2)

When filing creation fails, the current log messages are generic (e.g., "Exchange rate not found for USD on 2024-01-13"). Users and support cannot easily determine what happened or what to do about it.

After this fix, log messages include structured, actionable information: the income event details (entity, date, amount, currency), the rate resolution steps attempted (exact lookup, fallback dates tried), and a clear recommendation (e.g., "Rate for USD on 2024-01-13 not found; tried fallback to 2024-01-12 (holiday), 2024-01-11 (weekend), 2024-01-10 — no cached rate. Manually import rate or check NBS availability.").

**Why this priority**: Diagnostics don't directly fix failures but drastically reduce time-to-resolution and enable users to self-serve. Essential for operational reliability.

**Independent Test**: Can be fully tested by triggering a rate lookup failure and verifying the log output contains the expected structured information (entity, date, currency, resolution attempts, recommendation).

**Acceptance Scenarios**:

1. **Given** a successful rate resolution using exact-date lookup, **When** the event is logged, **Then** the log entry includes the date, currency, rate source ("exact"), and rate value.
2. **Given** a successful rate resolution using business day fallback, **When** the event is logged, **Then** the log entry includes the original date, fallback date used, currency, and a notice that fallback was used with error code `RATE_FALLBACK_USED`.
3. **Given** a failed rate resolution, **When** the event is logged, **Then** the log entry includes the income event (entity name, date, amount, currency), each date attempted during fallback, the reason each date failed, and a recommendation.
4. **Given** partial success processing of a report, **When** processing completes, **Then** a summary log entry lists: total events, successful filings, failed events with reasons, and the resulting report status.

---

### User Story 5 - Regression Tests for Known Failures (Priority: P3)

Known failing scenarios (specific USD dates on weekends/holidays, mixed success batches) must have automated regression tests to prevent future regressions.

**Why this priority**: Tests lock in the fixes from Stories 1-4. Lower priority because they don't deliver user-facing value independently, but are essential for long-term reliability.

**Independent Test**: Can be verified by running the test suite and confirming all known-failure date scenarios pass with correct fallback behavior.

**Acceptance Scenarios**:

1. **Given** a known failing date (e.g., USD on a Saturday), **When** the regression test runs, **Then** it verifies the fallback rate is resolved correctly and a filing is created.
2. **Given** a batch with mixed success outcomes, **When** the regression test runs, **Then** it verifies the correct number of filings, the correct report status (PartialError), and the correct error details for failed events.
3. **Given** a date within a consecutive holiday/weekend block, **When** the regression test runs, **Then** it verifies the fallback walks backward correctly past all non-business days.

---

### Edge Cases

- What happens when a rate is requested for a date more than 10 business days in the past with no NBS rate available? The system returns `RATE_NOT_FOUND` after exhausting the maximum lookback window.
- How does the system handle NBS publishing a rate list with missing currencies (e.g., a rarely traded currency not in the day's list)? The system treats it as `RATE_NOT_FOUND` for that specific currency, not as a service error.
- What happens when the NBS HTML page structure changes unexpectedly? The scraper returns `NBS_SCRAPE_ERROR`; the system continues to try ASMX as primary and logs a warning about the scraper failure.
- How does the system handle concurrent processing of multiple reports that need the same exchange rate? Rates are cached after first fetch; subsequent lookups hit the cache. Cache writes are idempotent.
- What happens when the cross-rate fallback (IBKR embedded rate × USD/RSD) also fails because USD rate is missing? The business day fallback applies to the USD rate lookup as well, falling back to the previous business day's USD rate before declaring failure.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST attempt exact-date exchange rate lookup first, then fall back to the previous business day's rate, skipping weekends and Serbian public holidays as defined in HolidayConf.
- **FR-002**: System MUST record rate provenance metadata on each created filing, indicating whether the rate used was from the exact income date or a fallback date, and which source date was used.
- **FR-003**: System MUST support a configurable maximum lookback window for business day fallback (default: 10 calendar days). If no rate is found within this window, it MUST return error code `RATE_NOT_FOUND`.
- **FR-004**: System MUST process each income event within a report independently, creating filings for events where rates resolve successfully and recording errors for events that fail.
- **FR-005**: System MUST set report status to "PartialError" when some income events succeed and others fail within the same report.
- **FR-006**: System MUST preserve existing behavior: report status "Processed" when all events succeed; "Error" when all events fail.
- **FR-007**: System MUST store per-event error details including: income event identifier (entity name, date, currency, amount), error code, and human-readable error message.
- **FR-008**: System MUST implement an NBS web application HTML scraper that fetches exchange rates from the NBS ExchangeRateWebApp IndexByDate endpoint.
- **FR-009**: The HTML scraper MUST parse the NBS HTML table with columns OZNAKA VALUTE (currency code), ŠIFRA VALUTE (currency number), NAZIV ZEMLJE (country name), VAŽI ZA (unit), KUPOVNI KURS (buying rate), PRODAJNI KURS (selling rate), deriving the middle rate as the average of buying and selling rates.
- **FR-010**: The HTML scraper MUST handle Serbian decimal format (comma as decimal separator) when parsing rate values.
- **FR-011**: System MUST try the primary ASMX service first for rate fetching; if it fails, it MUST fall back to the HTML scraper before declaring failure.
- **FR-012**: The HTML scraper MUST cache successfully parsed rates in the same local cache as ASMX-sourced rates.
- **FR-013**: System MUST log structured diagnostic information for each rate resolution attempt, including: date, currency, resolution strategy used (exact/fallback), source (ASMX/scraper), and outcome.
- **FR-014**: System MUST log a summary after processing each report: total income events, filings created, events failed, and resulting report status.
- **FR-015**: System MUST include automated regression tests for known failing USD date scenarios (weekends, Serbian public holidays) and mixed success batch processing.
- **FR-016**: Business day fallback MUST also apply to intermediate rate lookups (e.g., USD rate used in cross-rate calculations), not only to the primary income currency rate.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: The rate resolution strategy (business day fallback) is Domain logic, living in a new Domain service or extending existing services. The NBS HTML scraper is an Infrastructure concern implementing the existing `IExchangeRateFetcher` interface (or a new sibling interface). The orchestration of ASMX-first-then-scraper fallback lives in Infrastructure as a composite fetcher. ProcessReportsCommandHandler changes stay in the Application layer. Clean Architecture boundaries are preserved.
- **CA-002 (Money and Dates)**: All exchange rates use `decimal`. All dates use `DateOnly`. Rate provenance dates are `DateOnly`. No `DateTime` or `double` usage.
- **CA-003 (Privacy and Security)**: No new personal data is stored. Rate data is public NBS data. Storage remains local-first in SQLite. No credentials needed for NBS web app access (public endpoint).
- **CA-004 (Network Scope)**: Outbound calls are limited to: (1) existing NBS ASMX endpoint (`webservices.nbs.rs`), (2) new NBS web app endpoint (`webappcenter.nbs.rs`). Both are official NBS (National Bank of Serbia) endpoints already within the allowed network scope.
- **CA-005 (Async and UI)**: All HTTP calls (ASMX and HTML scraper) are async. Rate fallback logic involves multiple sequential async calls. No UI blocking operations introduced. Processing runs in background.
- **CA-006 (Testing Impact)**: Domain tests needed for business day fallback logic and HolidayConf integration. Application tests needed for ProcessReportsCommandHandler partial success behavior. Infrastructure tests needed for HTML scraper parsing. Regression tests for known failing date scenarios.

### Key Entities *(include if feature involves data)*

- **ExchangeRate**: Existing value object (DateOnly date, string currency, decimal rateToRsd). No structural changes needed; provenance is tracked at a higher level.
- **RateProvenance**: New concept representing how a rate was resolved — whether the rate is from the exact requested date or a fallback business day, and which source date was actually used.
- **Filing**: Existing aggregate root. Extended with optional provenance metadata to indicate rate resolution source (exact date vs. fallback date used).
- **Report**: Existing entity. ReportStatus enum extended with a new "PartialError" value for reports where some events succeeded and others failed.
- **ProcessReportsResult**: Existing DTO. Extended with per-event error details (entity name, date, currency, error code, message) instead of flat string list.
- **HolidayConf**: Existing value object. Used by the business day fallback logic to identify Serbian public holidays. No structural changes needed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Reports containing income events on weekends or Serbian public holidays produce filings using the previous business day's rate, achieving a filing creation success rate of 100% for dates within 10 calendar days of a valid rate.
- **SC-002**: When a report contains a mix of resolvable and unresolvable income events, the system creates filings for all resolvable events — partial success never drops to zero filings when at least one event can be resolved.
- **SC-003**: Users can identify which specific income events failed and why within 30 seconds of viewing the processing result, without needing to consult raw log files.
- **SC-004**: When the primary NBS ASMX service is unavailable, rate fetching succeeds via the HTML scraper fallback with no user intervention required.
- **SC-005**: All previously known failing USD date scenarios (weekends, Serbian holidays) pass automated regression tests with correct fallback behavior.
- **SC-006**: Processing log entries for failed events include the income event identifier, each fallback date attempted, and a clear recommendation, enabling self-service troubleshooting.
- **SC-007**: Each created filing carries provenance metadata indicating exact vs. fallback rate source, enabling auditability for tax filing purposes.

## Assumptions

- The NBS publishes exchange rates on every business day (Monday–Friday, excluding Serbian public holidays). No rate is published on weekends or public holidays — this is the root cause of the current failures.
- The HolidayConf value object is maintained with current Serbian public holidays. The business day fallback depends on accurate holiday data.
- The NBS web application HTML table structure (as observed in `nbs-scraped.txt`) is stable. If the structure changes, the scraper will fail gracefully with `NBS_SCRAPE_ERROR` and the ASMX service remains the primary source.
- The NBS HTML page uses Serbian locale decimal formatting (comma as decimal separator) for rate values.
- A maximum lookback window of 10 calendar days is sufficient to find a valid rate in all realistic scenarios (the longest Serbian public holiday block plus surrounding weekends does not exceed this).
- The middle rate derived from the HTML page (average of buying and selling rates) is equivalent to the Middle_Rate returned by the ASMX service for practical purposes.
- Existing cross-rate logic (IBKR embedded rate × USD/RSD) continues to work; the business day fallback applies to the USD lookup within cross-rate calculations as well.
- The `ReportStatus` enum can be extended with a new `PartialError` value without breaking existing database records or UI code.
- Filing provenance metadata is informational and does not affect tax calculation logic or filing validity.
