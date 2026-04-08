# Analysis: Filing Creation Reliability Fixes — Root Cause

**Feature**: 019-filing-creation-reliability-fixes  
**Date**: 2025-07-16

## Problem Statement

Users report that processing reports containing income events on certain dates produces "created 0 filing(s)" with no error explanation. The system marks the report as "Processed" even though no filings were created.

## Root Cause Analysis

### Failure Chain

```text
1. User receives dividend on Saturday, 2024-01-13
2. ProcessReportsCommandHandler processes the report
3. TaxCalculationService calls rateProvider(2024-01-13, "USD")
4. rateProvider → IExchangeRateFetcher.FetchRateAsync(2024-01-13, "USD")
5. NbsExchangeRateFetcher fetches from NBS ASMX for 2024-01-13
6. NBS returns empty response (no rates published on Saturday)
7. NbsExchangeRateFetcher returns Error("RATE_NOT_FOUND", ...)
8. rateProvider tries cross-rate fallback: FetchRateAsync(2024-01-13, "USD")
   → Same date, same failure (still Saturday)
9. rateProvider throws InvalidOperationException("Exchange rate not found for USD on 2024-01-13")
10. Per-event catch block in ProcessReportAsync catches the exception
11. Error added to errors list: "Dividend APPLE INC 2024-01-13: Exchange rate not found for USD on 2024-01-13"
12. Loop continues to next event (if any)
13. After all events processed, report.SetStatus(ReportStatus.Processed)  ← BUG
14. ProcessReportsResult reports: FilingsCreated=0, ReportsProcessed=1, Errors=[...]
```

### Root Causes Identified

| # | Root Cause | Impact | Component |
|---|---|---|---|
| **RC-1** | No business day fallback for exchange rates | Any income event on a weekend or Serbian holiday produces zero filings | `BuildRateProvider` in `ProcessReportsCommandHandler` |
| **RC-2** | Report always marked "Processed" even when all events fail | User sees "Processed" status with 0 filings — misleading | `ProcessReportsCommandHandler.HandleAsync` line 60 |
| **RC-3** | Cross-rate fallback uses same date as direct lookup | When USD rate is also missing (same date), cross-rate fallback cannot help | `BuildRateProvider` lines 222-225 |
| **RC-4** | Error messages are unstructured strings | Cannot programmatically identify which events failed or why | `ProcessReportsResult.Errors` type |
| **RC-5** | No rate provenance tracking | User cannot verify which date's rate was used for their filing | `Filing` entity lacks provenance fields |
| **RC-6** | Single NBS data source | ASMX service outage causes complete rate lookup failure | `NbsExchangeRateFetcher` is the only implementation |

### Why the Bug is Silent

The per-event `catch` block (lines 149-152, 199-202) catches `InvalidOperationException` and appends it to the errors list. However:
1. The outer `try` block (lines 56-63) does **not** throw — the individual catches prevent escalation
2. Line 60: `report.SetStatus(ReportStatus.Processed)` is reached regardless of per-event failures
3. The `ProcessReportsResult.Errors` list contains the error messages, but the UI only checks `FilingsCreated` count — if 0, it says "created 0 filing(s)" without showing errors
4. The report status is "Processed" — the user has no reason to suspect a problem

### Contributing Factors

1. **NBS publishes no rates on weekends/holidays**: This is expected behavior, not a bug in NBS. The system must account for it.
2. **IBKR reports income on payment date**: Payment dates often fall on weekends (ex-dividend + settlement cycle). This is normal broker behavior.
3. **Serbian public holidays are variable**: Orthodox Easter, Sretenje (Feb 15-16), and other holidays create multi-day gaps. The system has `HolidayConf` but doesn't use it for rate lookups.
4. **Existing `FilingDeadlineCalculator`** already implements forward business day skipping — the same pattern is needed for backward date walking.

## Fix Strategy

### Fix for RC-1: Business Day Fallback

**Before**: `FetchRateAsync(exactDate, currency)` → fail if no rate for that date  
**After**: Try exact date → walk backward through business days (skip weekends + holidays from `HolidayConf`) → fail only after 10-day lookback exhausted

**New components**:
- `BusinessDayResolver` (Domain): Static service with `IsBusinessDay()`, `FindPreviousBusinessDay()`, `WalkBackward()` 
- `ExchangeRateResolver` (Application): Orchestrates date fallback using `BusinessDayResolver` and `IExchangeRateFetcher`

### Fix for RC-2: Correct Report Status Determination

**Before**: Always `report.SetStatus(ReportStatus.Processed)` after event loop  
**After**: Track `succeededCount` and `failedCount` per report:
- All succeeded → `Processed`
- Mixed → `PartialError` (new status)
- All failed → `Error`

### Fix for RC-3: Business Day Fallback Applies to Cross-Rate USD Lookup

**Before**: Cross-rate uses `FetchRateAsync(sameDate, "USD")` — fails for same reason  
**After**: The `ExchangeRateResolver` handles fallback for both the primary currency and any intermediate currency lookups (including USD in cross-rate calculations)

### Fix for RC-4: Structured Error Records

**Before**: `IReadOnlyList<string> Errors` — opaque error strings  
**After**: `IReadOnlyList<FilingCreationError> EventErrors` with entity name, date, currency, amount, error code, message

### Fix for RC-5: Rate Provenance on Filing

**Before**: Filing stores only converted RSD amounts — no record of which rate was used  
**After**: Filing gains `ExchangeRateSourceDate` (DateOnly?) and `ExchangeRateSourceType` (Exact/Fallback?) — populated during creation

### Fix for RC-6: NBS Web App Scraper as Fallback Source

**Before**: Single `NbsExchangeRateFetcher` (ASMX) implementation  
**After**: `CompositeExchangeRateFetcher` tries ASMX first, then `NbsWebScraper` (HTML page) on HTTP/parse errors

## Impact Assessment

| Area | Impact | Risk |
|---|---|---|
| Domain layer | New `BusinessDayResolver` static service, `ExchangeRateSourceType` enum | Low — additive, no existing behavior changed |
| Application layer | New `ExchangeRateResolver`, updated `ProcessReportsCommandHandler`, updated DTOs | Medium — handler logic changes, but per-event structure preserved |
| Infrastructure layer | New `NbsWebScraper`, new `CompositeExchangeRateFetcher`, DI registration change | Medium — new HTTP client, but existing fetcher unchanged |
| Database schema | 2 new nullable columns on Filings, new enum value on ReportStatus | Low — additive, nullable, no data migration |
| UI | Must handle `PartialError` status display | Low — additive status value |
| Existing tests | May need updates for `ProcessReportsResult` signature change | Medium — DTO field renamed/added |

## Scenario Walkthrough: Saturday Dividend (After Fix)

```text
1. User receives APPLE INC dividend on Saturday, 2024-01-13 (USD)
2. ProcessReportsCommandHandler processes the report
3. Handler calls ExchangeRateResolver.ResolveAsync(2024-01-13, "USD", holidays)
4. ExchangeRateResolver:
   a. Try FetchRateAsync(2024-01-13, "USD") → RATE_NOT_FOUND (Saturday)
   b. BusinessDayResolver.WalkBackward(2024-01-13, holidays):
      - 2024-01-12 (Friday) → IsBusinessDay = true → yield
   c. Try FetchRateAsync(2024-01-12, "USD") → Success (Friday rate exists)
   d. Return RateResolution(rate, sourceDate=2024-01-12, sourceType=Fallback)
5. TaxCalculationService uses the rate for RSD conversion
6. Filing created with:
   - ExchangeRateSourceDate = 2024-01-12
   - ExchangeRateSourceType = Fallback
7. Log: "Rate for USD on 2024-01-13 resolved via fallback to 2024-01-12 (Friday)"
8. Report status: Processed (all events succeeded)
9. User sees: "created 1 filing(s)" ✓
```
