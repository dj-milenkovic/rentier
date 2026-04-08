# Tasks: Filing Creation Reliability Fixes

**Feature**: 019-filing-creation-reliability-fixes  
**Branch**: `feature/019-filing-creation-reliability-fixes`  
**Input**: Design documents from `specs/feature/019-filing-creation-reliability-fixes/`  
**Spec**: `.specify/specs/019-filing-creation-reliability-fixes/spec.md`  
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, analysis.md ✓, contracts/application-contracts.md ✓

**Tests**: Included — explicitly required by FR-015, spec US5, constitution quality gates, and caller request.  
**Organization**: Grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no shared dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5 from spec.md)
- All file paths are absolute from repository root

---

## Phase 1: Setup

**Purpose**: Verify the feature branch baseline compiles before any changes are made.

- [ ] T001 Verify solution builds cleanly on feature branch: run `dotnet build Rentier.slnx` from repo root and confirm zero errors and zero warnings before any changes

**Checkpoint**: Green build confirmed — safe to start Phase 2.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain types, Application DTOs, Filing entity extensions, and DB schema changes that every user story depends on. **No user story work can begin until this phase is complete.**

**⚠️ CRITICAL**: All five user stories consume types from this phase. Complete sequentially where noted.

- [ ] T002 Create `ExchangeRateSourceType` enum (`Exact = 0`, `Fallback = 1`) in `src/Rentier.Domain/Enums/ExchangeRateSourceType.cs` — add XML doc comments per plan D-004
- [ ] T003 Add `PartialError = 3` to `ReportStatus` enum in `src/Rentier.Domain/Enums/ReportStatus.cs` — extend existing Init/Processed/Error values; add XML doc comment "Some events succeeded, some failed"
- [ ] T004 [P] Create `FilingCreationError` sealed record (`EntityName`, `IncomeDate`, `Currency`, `Amount`, `ErrorCode`, `Message`) in `src/Rentier.Application/DTOs/FilingCreationError.cs` — valid error codes: `RATE_NOT_FOUND`, `UNSUPPORTED_CURRENCY`, `NBS_HTTP_ERROR`, `NBS_PARSE_ERROR`, `NBS_SCRAPE_ERROR`, `DOMAIN_ERROR`
- [ ] T005 [P] Create `RateResolution` sealed record (`ExchangeRate Rate`, `DateOnly SourceDate`, `ExchangeRateSourceType SourceType`) in `src/Rentier.Application/DTOs/RateResolution.cs` — add namespace usings for `Rentier.Domain.Enums` and `Rentier.Domain.ValueObjects`
- [ ] T006 Update `ProcessReportsResult` record in `src/Rentier.Application/DTOs/ProcessReportsResult.cs`: add `int ReportsPartialError` parameter, change `IReadOnlyList<string> Errors` to `IReadOnlyList<FilingCreationError> EventErrors`; update all callers in `ProcessReportsCommandHandler.cs` and existing tests in `tests/Rentier.Application.Tests/ProcessReportsCommandHandlerTests.cs` to use the new field name and type (depends on T004)
- [ ] T007 Extend `Filing` entity in `src/Rentier.Domain/Entities/Filing.cs`: add `public DateOnly? ExchangeRateSourceDate { get; private set; }` and `public ExchangeRateSourceType? ExchangeRateSourceType { get; private set; }` properties; extend `CreateFromIncome` static factory with two new optional parameters `DateOnly? exchangeRateSourceDate = null` and `ExchangeRateSourceType? exchangeRateSourceType = null` that set the new properties (depends on T002); preserve existing calls (default null keeps them unchanged)
- [ ] T008 Update `FilingConfiguration` in `src/Rentier.Infrastructure/Persistence/Configurations/FilingConfiguration.cs`: add `builder.Property(f => f.ExchangeRateSourceDate).IsRequired(false)` and `builder.Property(f => f.ExchangeRateSourceType).IsRequired(false)` mappings (depends on T007)
- [ ] T009 Generate EF migration `0010_FilingRateProvenance` by running `dotnet ef migrations add 0010_FilingRateProvenance --startup-project ../Rentier.Desktop` from `src/Rentier.Infrastructure/`; verify generated migration adds two nullable columns: `"ExchangeRateSourceDate" TEXT NULL` and `"ExchangeRateSourceType" INTEGER NULL` on the `Filings` table (depends on T008); verify Down script removes both columns
- [ ] T010 Update `FilingCreateFromIncomeTests` in `tests/Rentier.Domain.Tests/FilingCreateFromIncomeTests.cs`: add tests verifying (a) `CreateFromIncome` with no provenance args leaves both properties `null`, (b) `CreateFromIncome` with `Exact` provenance sets both fields, (c) `CreateFromIncome` with `Fallback` provenance and a prior business day sets `SourceDate` to that prior date and `SourceType` to `Fallback` (depends on T007)

**Checkpoint**: Domain types, Application DTOs, Filing entity, and DB schema are ready — all user story phases can now begin.

---

## Phase 3: User Story 1 — Resilient Exchange Rate Resolution (Priority: P1) 🎯 MVP Core

**Goal**: Eliminate zero-filing outcomes caused by weekend/holiday income dates by walking backward through business days to find the nearest valid NBS rate. Returns provenance metadata (exact vs. fallback, source date) for audit trail.

**Independent Test**: Create a report with a single dividend event on Saturday 2024-01-13 (USD). Process it. Verify: (1) one filing is created, (2) `ExchangeRateSourceDate = 2024-01-12`, (3) `ExchangeRateSourceType = Fallback`, (4) `ReportStatus = Processed`.

### Tests for User Story 1 ⚠️ Write these FIRST — they must FAIL before implementation

- [ ] T011 [P] [US1] Write `BusinessDayResolverTests` in `tests/Rentier.Domain.Tests/Services/BusinessDayResolverTests.cs`: `IsBusinessDay` cases — (a) Monday = true, (b) Saturday = false, (c) Sunday = false, (d) weekday in `HolidayConf.Holidays` = false, (e) weekday not in holidays = true; use `new HolidayConf(new List<DateOnly>())` and holiday-containing configs
- [ ] T012 [P] [US1] Write `BusinessDayResolverTests` in `tests/Rentier.Domain.Tests/Services/BusinessDayResolverTests.cs`: `WalkBackward` cases — (a) from Monday yields Friday, (b) from Sunday yields Friday, (c) from Saturday yields Friday, (d) from weekday-after-holiday yields the business day before the holiday, (e) consecutive holidays: Friday is holiday → yields Thursday, (f) all days in window are non-business → yields empty sequence (no results within `maxLookbackDays`), (g) `maxLookbackDays = 1` limits to 1 calendar day back; use `[Theory]` with `[InlineData]`

### Implementation for User Story 1

- [ ] T013 [US1] Implement `BusinessDayResolver` static service in `src/Rentier.Domain/Services/BusinessDayResolver.cs`: `IsBusinessDay(DateOnly date, HolidayConf holidays)` returns true if date is Mon–Fri and not in `holidays.Holidays`; `WalkBackward(DateOnly fromDate, HolidayConf holidays, int maxLookbackDays = 10)` yields business days walking from `fromDate.AddDays(-1)` backward for up to `maxLookbackDays` calendar days; `FindPreviousBusinessDay(DateOnly date, HolidayConf holidays)` returns `date` if it is already a business day, otherwise `WalkBackward(date, holidays).First()`; throw `DomainException` if `holidays` is null or `maxLookbackDays < 1` — mirrors `FilingDeadlineCalculator` static pattern (after T011, T012 confirm failing tests)
- [ ] T014 [P] [US1] Write `ExchangeRateResolverTests` in `tests/Rentier.Application.Tests/Services/ExchangeRateResolverTests.cs`: exact-date success — fetcher returns success for requested date → `ResolveAsync` returns `RateResolution` with `SourceType = Exact` and `SourceDate = incomeDate`; use `NSubstitute` mock for `IExchangeRateFetcher`
- [ ] T015 [P] [US1] Write `ExchangeRateResolverTests` in `tests/Rentier.Application.Tests/Services/ExchangeRateResolverTests.cs`: Saturday fallback — fetcher returns `RATE_NOT_FOUND` for Saturday, success for preceding Friday → result has `SourceType = Fallback`, `SourceDate = Friday`, correct `ExchangeRate`; verify fetcher was called exactly twice (Saturday then Friday)
- [ ] T016 [P] [US1] Write `ExchangeRateResolverTests` in `tests/Rentier.Application.Tests/Services/ExchangeRateResolverTests.cs`: max lookback exhausted — fetcher returns `RATE_NOT_FOUND` for all candidate dates within window → result is `Error` with code `RATE_NOT_FOUND` containing the original date, currency, and list of dates tried
- [ ] T017 [P] [US1] Write `ExchangeRateResolverTests` in `tests/Rentier.Application.Tests/Services/ExchangeRateResolverTests.cs`: non-retryable errors — (a) fetcher returns `UNSUPPORTED_CURRENCY` on first call → resolver returns immediately with that error, does not walk backward; (b) fetcher returns `NBS_PARSE_ERROR` on exact date → resolver returns immediately (not retryable per contracts)
- [ ] T018 [US1] Implement `ExchangeRateResolver` service in `src/Rentier.Application/Services/ExchangeRateResolver.cs`: constructor takes `IExchangeRateFetcher fetcher`; `ResolveAsync(DateOnly date, string currency, HolidayConf holidays, int maxLookbackDays = 10, CancellationToken ct = default)` returns `Result<RateResolution, Error>` — (1) try `FetchRateAsync(date, currency)` → if success return `RateResolution(rate, date, Exact)`, if non-retryable error (`UNSUPPORTED_CURRENCY`, `NBS_PARSE_ERROR`) return error immediately; (2) iterate `BusinessDayResolver.WalkBackward(date, holidays, maxLookbackDays)` → try each candidate date, on success return `RateResolution(rate, candidateDate, Fallback)`, skip on `RATE_NOT_FOUND`, skip on other errors (log warning); (3) exhausted → return `Error("RATE_NOT_FOUND", diagnostic message listing currency, original date, and all dates tried) (after T013, T014–T017 confirm failing tests)

**Checkpoint**: `BusinessDayResolver` tests pass, `ExchangeRateResolverTests` pass. Run `dotnet test tests/Rentier.Domain.Tests` and `dotnet test tests/Rentier.Application.Tests` — User Story 1 domain and application logic validated.

---

## Phase 4: User Story 2 — Partial Success Report Processing (Priority: P1)

**Goal**: Each income event is processed independently. Reports get `PartialError` status when some events succeed and others fail. Per-event structured errors replace opaque strings.

**Independent Test**: Create a report with three income events — two with valid rates (USD) and one with currency `XYZ` (unsupported). Verify: (1) two filings created, (2) `ReportStatus = PartialError`, (3) `EventErrors` has exactly one `FilingCreationError` with `ErrorCode = "UNSUPPORTED_CURRENCY"`, `EntityName` matching the failed event.

### Tests for User Story 2 ⚠️ Write these FIRST — they must FAIL before implementation

- [ ] T019 [P] [US2] Write `ProcessReportsCommandHandlerPartialSuccessTests` in `tests/Rentier.Application.Tests/Handlers/ProcessReportsCommandHandlerPartialSuccessTests.cs`: all events succeed → `ReportStatus = Processed`, `FilingsCreated = n`, `EventErrors` is empty, `ReportsPartialError = 0`; mock `ExchangeRateResolver` to return success for all events
- [ ] T020 [P] [US2] Write `ProcessReportsCommandHandlerPartialSuccessTests`: 3 events, 2 succeed, 1 fails with `RATE_NOT_FOUND` → `ReportStatus = PartialError`, `FilingsCreated = 2`, `EventErrors.Count = 1`, error has correct `EntityName`, `IncomeDate`, `Currency`, `ErrorCode = "RATE_NOT_FOUND"`; `ReportsPartialError = 1`
- [ ] T021 [P] [US2] Write `ProcessReportsCommandHandlerPartialSuccessTests`: all events fail → `ReportStatus = Error`, `FilingsCreated = 0`, `EventErrors.Count = n`, `ReportsErrored = 1`, `ReportsProcessed = 0`; also test empty report (no events) → `ReportStatus = Processed`

### Implementation for User Story 2

- [ ] T022 [US2] Refactor `ProcessReportsCommandHandler` constructor and `BuildRateProvider` in `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs`: replace `IExchangeRateFetcher _exchangeRateFetcher` field with `ExchangeRateResolver _exchangeRateResolver`; update constructor parameter accordingly; refactor `BuildRateProvider` return type from `Func<DateOnly, string, Task<ExchangeRate>>` to `Func<DateOnly, string, Task<RateResolution>>` — call `_exchangeRateResolver.ResolveAsync(date, currency, holidays, ct: ct)`, on failure throw `InvalidOperationException` carrying the error code and message so per-event catch blocks can capture it; also apply `ExchangeRateResolver` to the cross-rate USD lookup path (replace `_exchangeRateFetcher.FetchRateAsync(date, "USD", ct)` with resolver call so USD fallback also benefits from business day walk) (depends on T018)
- [ ] T023 [US2] Implement partial success status logic in `ProcessReportsCommandHandler.ProcessReportAsync` in `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs`: change `List<string> errors` parameter to `List<FilingCreationError> errors`; add local `int succeededCount = 0` and `int failedCount = 0` counters; in dividend and interest success paths increment `succeededCount`; in catch blocks create structured `FilingCreationError` records (extract entity name, date, currency, amount from the processing context, error code from exception message if available, else `DOMAIN_ERROR`); increment `failedCount`; return a `(int created, int failed)` tuple so the outer `HandleAsync` loop can determine report status (after T022)
- [ ] T024 [US2] Implement post-loop report status determination in `ProcessReportsCommandHandler.HandleAsync` in `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs`: replace hardcoded `report.SetStatus(ReportStatus.Processed)` with: `if (failedCount == 0) Processed; else if (succeededCount > 0) PartialError; else Error`; track `reportsPartialError` counter; pass all `FilingCreationError` items into final `ProcessReportsResult` (depends on T023)
- [ ] T025 [US2] Propagate rate provenance to `Filing.CreateFromIncome` calls in `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs`: in both dividend and interest success paths, capture `RateResolution` returned by `BuildRateProvider`; pass `resolution.SourceDate` as `exchangeRateSourceDate` and `resolution.SourceType` as `exchangeRateSourceType` to `Filing.CreateFromIncome`; extract `ExchangeRate` from `resolution.Rate` for `TaxCalculationService` (which remains unchanged) (depends on T024)

**Checkpoint**: `ProcessReportsCommandHandlerPartialSuccessTests` pass. Update and re-run existing `ProcessReportsCommandHandlerTests` for the new `ProcessReportsResult` signature. Run `dotnet test tests/Rentier.Application.Tests` — User Story 2 fully validated.

---

## Phase 5: User Story 3 — NBS Web App Exchange Rate Scraper (Priority: P2)

**Goal**: When the primary NBS ASMX service fails, a transparent HTML scraper fallback fetches rates from `webappcenter.nbs.rs`. All callers continue using `IExchangeRateFetcher` — no interface changes needed.

**Independent Test**: Invoke `NbsWebScraper.FetchRateAsync` with a known date (e.g., 2024-01-12) and currency `USD`. Verify the parsed middle rate `= (buyingRate + sellingRate) / 2m` and correct handling of Serbian comma-decimal format.

### Tests for User Story 3 ⚠️ Write these FIRST — they must FAIL before implementation

- [ ] T026 [P] [US3] Write `NbsWebScraperTests` in `tests/Rentier.Infrastructure.Tests/ExchangeRates/NbsWebScraperTests.cs`: valid HTML table parsing — feed inline fixture HTML with 3 currency rows (EUR, USD, GBP), each with 6 `<td>` cells including comma-decimal buying/selling rates; assert (a) all three currencies are cached via `IExchangeRateCacheRepository.SaveBatchAsync`, (b) returned `ExchangeRate` for requested currency has `RateToRsd = (buying + selling) / 2m / unit`, (c) `"117,0539"` parses to `117.0539m`; mock `HttpClient` using `HttpMessageHandler` substitute; mock `IExchangeRateCacheRepository`
- [ ] T027 [P] [US3] Write `NbsWebScraperTests`: empty table (weekend/holiday page) — HTML has the table element but zero `<tr>` data rows; assert result is `Error` with `ErrorCode = "RATE_NOT_FOUND"`, not `NBS_SCRAPE_ERROR`; verify no `SaveBatchAsync` call
- [ ] T028 [P] [US3] Write `NbsWebScraperTests`: malformed HTML scenarios — (a) no table found → `NBS_SCRAPE_ERROR`; (b) row with wrong column count → `NBS_SCRAPE_ERROR`; (c) HTTP 500 response → `NBS_HTTP_ERROR`; (d) `HttpRequestException` thrown → `NBS_HTTP_ERROR`; each as separate `[Theory]` case
- [ ] T029 [US3] Implement `NbsWebScraper` in `src/Rentier.Infrastructure/ExchangeRates/NbsWebScraper.cs`: implements `IExchangeRateFetcher`; constructor takes `HttpClient http` and `IExchangeRateCacheRepository cache`; `FetchRateAsync(DateOnly date, string currency, CancellationToken ct)` — (1) check `IExchangeRateCacheRepository` first (cache-hit fast path), return if found; (2) build URL `https://webappcenter.nbs.rs/ExchangeRateWebApp/ExchangeRate/IndexByDate?isSearchExecuted=true&Date={date:dd.MM.yyyy.}&ExchangeRateListTypeID=1`; (3) `GET` with `HttpClient`, catch `HttpRequestException` → return `NBS_HTTP_ERROR`; (4) parse with `AngleSharp BrowsingContext` using `QuerySelectorAll("tbody tr")` or `"table tr"`; (5) each data row: extract 6 `<td>` text values → code=col[0], unit=`int.Parse(col[3])`, buying=`decimal.Parse(col[4], NumberStyles.Any, new CultureInfo("sr-Latn-RS"))`, selling=`decimal.Parse(col[5], ...)`, middle=`(buying+selling)/2m`, rateToRsd=`middle/unit`; (6) if zero rows parsed → return `RATE_NOT_FOUND`; (7) call `cache.SaveBatchAsync` with all parsed rates; (8) look up requested currency in batch → return `ExchangeRate` or `RATE_NOT_FOUND`; catch parse exceptions → return `NBS_SCRAPE_ERROR` with message (after T026–T028 confirm failing tests)
- [ ] T030 [P] [US3] Write `CompositeExchangeRateFetcherTests` in `tests/Rentier.Infrastructure.Tests/ExchangeRates/CompositeExchangeRateFetcherTests.cs`: primary success path — ASMX (primary) returns success → result is the primary's rate; secondary (`NbsWebScraper`) is never called; verify via `NSubstitute` call count
- [ ] T031 [P] [US3] Write `CompositeExchangeRateFetcherTests`: `NBS_HTTP_ERROR` from primary → secondary is called and its success result is returned; also test `NBS_PARSE_ERROR` from primary → secondary is called
- [ ] T032 [P] [US3] Write `CompositeExchangeRateFetcherTests`: `RATE_NOT_FOUND` from primary → secondary is NOT called (both sources agree — no rate for holiday); result is `RATE_NOT_FOUND`
- [ ] T033 [P] [US3] Write `CompositeExchangeRateFetcherTests`: both primary and secondary fail → result is an error; also test `UNSUPPORTED_CURRENCY` from primary → secondary is NOT called; result is `UNSUPPORTED_CURRENCY`
- [ ] T034 [US3] Implement `CompositeExchangeRateFetcher` in `src/Rentier.Infrastructure/ExchangeRates/CompositeExchangeRateFetcher.cs`: implements `IExchangeRateFetcher`; constructor takes `NbsExchangeRateFetcher primary` and `NbsWebScraper secondary` (concrete types, not interface, to allow DI named registrations); `FetchRateAsync` — try primary; on success return; on `NBS_HTTP_ERROR` or `NBS_PARSE_ERROR` try secondary and return its result; on `RATE_NOT_FOUND` or `UNSUPPORTED_CURRENCY` return primary error immediately without calling secondary (after T029, T030–T033)
- [ ] T035 [US3] Update DI registrations in `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`: (a) change `AddHttpClient<IExchangeRateFetcher, NbsExchangeRateFetcher>()` to `AddHttpClient<NbsExchangeRateFetcher>()`; (b) add `services.AddHttpClient<NbsWebScraper>()`; (c) register `services.AddTransient<IExchangeRateFetcher, CompositeExchangeRateFetcher>()`; (d) register `services.AddTransient<ExchangeRateResolver>()` in Application layer wiring (or in the Application DI extension if one exists) (depends on T034)

**Checkpoint**: `NbsWebScraperTests` and `CompositeExchangeRateFetcherTests` pass. Run `dotnet test tests/Rentier.Infrastructure.Tests` — User Story 3 scraper and composite chain fully validated.

---

## Phase 6: User Story 4 — Actionable Diagnostics in Logs (Priority: P2)

**Goal**: Every rate resolution step and report processing outcome is logged with structured, actionable information so users can self-diagnose failures within 30 seconds.

**Independent Test**: Trigger a `RATE_NOT_FOUND` after max lookback by providing a mock that always fails; capture `ILogger` calls via an `ILogger<ExchangeRateResolver>` mock; verify log entries contain the original date, currency, list of all attempted dates, and recommendation text.

### Tests for User Story 4 ⚠️

- [ ] T036 [P] [US4] Write `ExchangeRateResolverLoggingTests` in `tests/Rentier.Application.Tests/Services/ExchangeRateResolverLoggingTests.cs`: (a) exact success → one `Information` log with date, currency, rate value, source `"exact"`; (b) fallback success → one `Warning` log with original date, fallback date, currency, and error code `RATE_FALLBACK_USED`; (c) all exhausted → one `Error` log containing entity context, each date tried, reason each failed, and a recommendation string; use `NSubstitute` mock for `ILogger<ExchangeRateResolver>` and verify `Log` calls with expected log levels and message substrings

### Implementation for User Story 4

- [ ] T037 [US4] Inject `ILogger<ExchangeRateResolver>` into `ExchangeRateResolver` constructor in `src/Rentier.Application/Services/ExchangeRateResolver.cs`; add log statements: `LogInformation` on exact-date success (date, currency, rate, "exact"); `LogWarning` on fallback success (original date, fallback date used, currency, `RATE_FALLBACK_USED`); `LogError` on exhaustion (currency, original date, all candidate dates tried with each date's failure reason, and recommendation: "Manually import rate or check NBS availability") (after T036)
- [ ] T038 [US4] Add per-report summary log in `ProcessReportsCommandHandler.HandleAsync` in `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs`: after each report's event loop completes, call `LogInformation` with: report ID, total income events processed, filings created, failed events, and resulting `ReportStatus`; inject `ILogger<ProcessReportsCommandHandler>` into constructor if not already present

**Checkpoint**: `ExchangeRateResolverLoggingTests` pass. Run `dotnet test tests/Rentier.Application.Tests` — User Story 4 diagnostic contract validated.

---

## Phase 7: User Story 5 — Regression Tests for Known Failures (Priority: P3)

**Goal**: Automated tests lock in the fix for every previously known failing scenario. These must continue passing on every future branch.

**Independent Test**: Run `dotnet test` with no mocking overrides — all regression scenarios in this phase pass green.

- [ ] T039 [P] [US5] Add regression tests to `tests/Rentier.Domain.Tests/Services/BusinessDayResolverTests.cs`: `WalkBackward` from known failing date Saturday `2024-01-13` (USD weekend) → first yielded date = `2024-01-12` (Friday); `WalkBackward` from `2024-01-14` (Sunday) → first yielded = `2024-01-12`; use `[InlineData]` with specific hardcoded dates; add comment `// Regression: USD weekend failures reported 2024-01`
- [ ] T040 [P] [US5] Add regression tests to `tests/Rentier.Application.Tests/Services/ExchangeRateResolverTests.cs`: Serbian public holiday `2024-02-15` (Sretenje) — mock fetcher returns `RATE_NOT_FOUND` for `2024-02-15` and `2024-02-16` (both holiday days), success for `2024-02-14` (Wednesday before); assert `RateResolution.SourceDate = 2024-02-14` and `SourceType = Fallback`; include `HolidayConf` with `[2024-02-15, 2024-02-16]` in test setup; add comment `// Regression: Sretenje holiday block 2024`
- [ ] T041 [P] [US5] Add regression tests to `tests/Rentier.Domain.Tests/Services/BusinessDayResolverTests.cs`: consecutive block — Friday `2024-02-16` is holiday, Saturday `2024-02-17` and Sunday `2024-02-18` are weekend → `WalkBackward` from `2024-02-18` with `HolidayConf([2024-02-16])` should yield `2024-02-15` (Thursday) as first result (skipping Sat, Sun, and Fri holiday); add comment `// Regression: multi-day block walk`
- [ ] T042 [P] [US5] Add regression test to `tests/Rentier.Application.Tests/Handlers/ProcessReportsCommandHandlerPartialSuccessTests.cs`: mixed batch — report with 3 income events, 2 resolve (USD normal weekday), 1 fails with `RATE_NOT_FOUND` after exhausted lookback; assert `FilingsCreated = 2`, `ReportStatus = PartialError`, `EventErrors.Count = 1`, `EventErrors[0].ErrorCode = "RATE_NOT_FOUND"`, `EventErrors[0].IncomeDate` matches the failing event's date; add comment `// Regression: mixed success batch, zero-filing bug fix`

**Checkpoint**: All 4 regression scenarios pass. `dotnet test Rentier.slnx` green.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final integration, DI smoke test, quickstart validation, and coverage gate confirmation.

- [ ] T043 Update `DiRegistrationSmokeTests` in `tests/Rentier.Application.Tests/DiRegistrationSmokeTests.cs`: add assertions that `IExchangeRateFetcher` resolves to `CompositeExchangeRateFetcher`, `ExchangeRateResolver` is resolvable, and `ProcessReportsCommandHandler` still resolves (all DI registrations intact after T035)
- [ ] T044 [P] Verify full solution build: run `dotnet build Rentier.slnx` and confirm zero errors and zero warnings; fix any warnings introduced by new files or changed signatures
- [ ] T045 [P] Run complete test suite: `dotnet test Rentier.slnx` and confirm all tests pass; resolve any test failures introduced by `ProcessReportsResult` DTO signature change across all test projects including `Rentier.Desktop.Tests` (e.g., `ReportsViewModelTests`, `SyncViewModelTests` if they reference the result)
- [ ] T046 Validate constitution quality gates: (a) `dotnet test tests/Rentier.Domain.Tests` — `BusinessDayResolver` must have 100% branch coverage (all day types, holiday combos, edge cases, max-lookback); (b) `dotnet test tests/Rentier.Application.Tests` — `ExchangeRateResolver` ≥90% line coverage, `ProcessReportsCommandHandler` partial-success paths ≥90%; (c) confirm no `double`/`float` or `DateTime` introduced in any new file by reviewing with `grep`

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)         ──────────────────────────────────────→ Phase 2
Phase 2 (Foundational)  ── BLOCKS ALL USER STORIES ──────────→ Phase 3, 4, 5, 6
Phase 3 (US1)           ── ExchangeRateResolver ready ───────→ Phase 4, 6
Phase 4 (US2)           ── Handler partial success ready ─────→ Phase 6, 8
Phase 5 (US3)           ── Composite fetcher + DI ready ─────→ Phase 8
Phase 6 (US4)           ── Logging in place ─────────────────→ Phase 8
Phase 7 (US5)           ── Regression tests (parallel to 6) ─→ Phase 8
Phase 8 (Polish)        ── Depends on all above complete
```

### User Story Dependencies

- **US1 (P1)** — Depends only on Phase 2 (foundational types). No other US dependency.
- **US2 (P1)** — Depends on Phase 2 + US1 (needs `ExchangeRateResolver` from T018).
- **US3 (P2)** — Depends only on Phase 2. Can proceed in parallel with US1/US2.
- **US4 (P2)** — Depends on US1 (to add logging to `ExchangeRateResolver`) and US2 (to add summary log to handler).
- **US5 (P3)** — Depends on US1, US2. Regression tests require both implementations to exist.

### Within Each Phase

```
Phase 2: T002 → T007 → T008 → T009 (sequential schema chain)
         T003, T004, T005 parallel with T002 chain
         T006 after T004
         T010 after T007

Phase 3: T011, T012 parallel (both test methods in same file)
         T013 after T011 + T012
         T014, T015, T016, T017 parallel
         T018 after T013 + T014–T017

Phase 4: T019, T020, T021 parallel
         T022 after T018
         T023 after T022
         T024 after T023
         T025 after T022

Phase 5: T026, T027, T028 parallel
         T029 after T026–T028
         T030, T031, T032, T033 parallel
         T034 after T029 + T030–T033
         T035 after T034

Phase 6: T036 first
         T037 after T036
         T038 after T037 (or parallel with T037 — different file sections)

Phase 7: T039, T040, T041, T042 all parallel

Phase 8: T043 after T035
         T044, T045 parallel
         T046 after T044 + T045
```

### Parallel Opportunities per Story

```bash
# Phase 2 — run in parallel groups:
# Group A (parallel): T003, T004, T005
# Group B (sequential): T002 → T007 → T008 → T009
# T006 after T004; T010 after T007

# Phase 3 — parallel test writing, sequential implementation:
# Parallel: T011, T012, T014, T015, T016, T017
# Then: T013 (after T011+T012), T018 (after T013+T014–T017)

# Phase 5 — parallel test writing across two test classes:
# Parallel: T026, T027, T028, T030, T031, T032, T033
# Then: T029 (after scraper tests), T034 (after composite tests + T029)

# Phase 7 — all regression tasks fully parallel:
# Parallel: T039, T040, T041, T042
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 — the P1 bug fix)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational — all domain types + DB schema (T002–T010)
3. Complete Phase 3: US1 — BusinessDayResolver + ExchangeRateResolver (T011–T018)
4. Complete Phase 4: US2 — Handler partial success + provenance propagation (T019–T025)
5. **STOP and VALIDATE**: Run `dotnet test Rentier.slnx`. The root cause (RC-1, RC-2, RC-3, RC-4, RC-5) is fully fixed. Users will now get correct filings for weekend/holiday income dates with provenance metadata.
6. Ship MVP if ready.

### Incremental Delivery

1. Setup + Foundational → domain/app layer compiles
2. US1 + US2 → **root bug fixed** → deploy/validate (MVP)
3. US3 → **ASMX outage resilience** → deploy/validate
4. US4 → **self-service diagnostics** → deploy/validate
5. US5 → **regression guard** → merge to main

### Single-Developer Sequential Order

```
T001 → T002 → T003 → T004 → T005 → T006 → T007 → T008 → T009 → T010
     → T011 → T012 → T013 → T014 → T015 → T016 → T017 → T018
     → T019 → T020 → T021 → T022 → T023 → T024 → T025
     → T026 → T027 → T028 → T029 → T030 → T031 → T032 → T033 → T034 → T035
     → T036 → T037 → T038
     → T039 → T040 → T041 → T042
     → T043 → T044 → T045 → T046
```

---

## Task Count Summary

| Phase | Story | Tasks | Parallel [P] |
|---|---|---|---|
| Phase 1: Setup | — | 1 (T001) | 0 |
| Phase 2: Foundational | — | 9 (T002–T010) | 4 |
| Phase 3: US1 Resilient Rate Resolution | US1 | 8 (T011–T018) | 6 |
| Phase 4: US2 Partial Success Processing | US2 | 7 (T019–T025) | 3 |
| Phase 5: US3 NBS Web Scraper | US3 | 10 (T026–T035) | 8 |
| Phase 6: US4 Actionable Diagnostics | US4 | 3 (T036–T038) | 1 |
| Phase 7: US5 Regression Tests | US5 | 4 (T039–T042) | 4 |
| Phase 8: Polish | — | 4 (T043–T046) | 2 |
| **TOTAL** | | **46 tasks** | **28 parallelizable** |

---

## Notes

- [P] tasks = different files, no shared dependencies — safe to implement simultaneously
- Each user story has its own independent test scenario at the phase header
- Tests must be written and confirmed FAILING before implementing the feature code they test
- Commit after each logical group (e.g., after T009 migration is verified, after T013 passes tests)
- `BusinessDayResolver` follows the existing static-service pattern of `FilingDeadlineCalculator` — no constructor, all static methods
- `ExchangeRateResolver` is an Application-layer service (async, I/O via IExchangeRateFetcher) — not Domain
- `CompositeExchangeRateFetcher` is registered as the `IExchangeRateFetcher` implementation — all existing callers get ASMX→scraper fallback transparently with zero interface changes
- No `double`/`float` anywhere — all rates are `decimal`; no `DateTime` — all dates are `DateOnly`
- Pre-existing `FilingCreateFromIncomeTests` must still pass after T007 (new optional parameters default to null)
