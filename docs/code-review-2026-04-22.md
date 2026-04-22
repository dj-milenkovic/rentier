# Rentier — Comprehensive Code Review
**Date:** 2026-04-22  
**Model:** Claude Opus 4.6  
**Scope:** All 4 layers (Domain, Application, Infrastructure, Desktop) + all test suites

---

## Table of Contents
1. [Executive Summary](#1-executive-summary)
2. [Domain Layer Review](#2-domain-layer-review)
3. [Application Layer Review](#3-application-layer-review)
4. [Infrastructure Layer Review](#4-infrastructure-layer-review)
5. [Desktop / UI Layer Review](#5-desktop--ui-layer-review)
6. [Domain Test Coverage Review](#6-domain-test-coverage-review)
7. [Application Test Coverage Review](#7-application-test-coverage-review)
8. [Infrastructure Test Coverage Review](#8-infrastructure-test-coverage-review)
9. [Desktop Test Coverage Review](#9-desktop-test-coverage-review)

---

## 1. Executive Summary

| Layer | Critical | Major | Minor |
|-------|----------|-------|-------|
| Domain | 3 | 6 | 8 |
| Application | 3 | 8 | 8 |
| Infrastructure | 3 | 6 | 8 |
| Desktop / UI | 4 | 8 | 6 |
| **Total (code)** | **13** | **28** | **30** |

| Test Suite | Critical Gaps | Major Gaps | Minor Gaps |
|------------|---------------|------------|------------|
| Domain tests | 3 | 14 | 15 |
| Application tests | 0 | 6 | 8 |
| Infrastructure tests | 2 | 6 | 3 |
| Desktop tests | see §9 | see §9 | see §9 |

### Top Priorities (cross-cutting)

1. **🔴 `ManualFilingViewModel` calls `ITaxpayerProfileRepository` directly** — architecture violation (Desktop → Infrastructure bypass)
2. **🔴 `DataGrid` sort handler is a no-op** — data correctness bug across pages in FilingsView
3. **🔴 `Money` has zero validation** — any `new Money(-100, null!)` compiles and propagates
4. **🔴 `ProcessReportsCommandHandler` uses exception-as-flow-control** — constitution violation
5. **🔴 `HolidayConf` `with`-expression bypasses null check** — invariant can be broken at runtime
6. **🔴 MacOS credential store exposes password in process args** — security issue visible via `ps`
7. **🔴 `_rowSubscriptions` never disposed** — memory leak on deactivation in Filings/Reports views

---

## 2. Domain Layer Review

*24 files reviewed: 7 entities, 6 value objects, 3 services, 6 enums, 1 exception.*

### 🔴 Critical

**C1 — `HolidayConf` invariant bypassed by `with` expression** (`ValueObjects/HolidayConf.cs`)  
`Holidays` uses `{ get; init; }`, so `conf with { Holidays = null! }` compiles and bypasses the constructor null-check. Fix: change to `{ get; }` and assign in the constructor body (same pattern as `ExchangeRate` which correctly uses get-only properties). Also make the record `sealed`.

**C2 — `TaxCalculationService` doesn't null-check `rateProvider` return** (`Services/TaxCalculationService.cs:46`)  
If `rateProvider` returns `null`, `incomeAmount * incomeRate.RateToRsd` throws an unguarded `NullReferenceException`. Add: `if (incomeRate is null) throw new DomainException(...)`.

**C3 — `Money` has zero validation** (`ValueObjects/Money.cs`)  
No guard against `null`/empty `Currency` or negative `Amount`. In a financial app, this is the single most critical value object — anyone can construct `new Money(-100, null!)`. Fix: add a constructor with validation (like `ExchangeRate`), and use get-only properties.

### 🟠 Major

**M1 — `MailboxCursor` is not a discriminated union** (`ValueObjects/MailboxCursor.cs`)  
Constitution requires: *"MailboxCursor is a discriminated union via abstract record."* Actual implementation is a flat record `(DateOnly? LastSyncDate, long? LastUid)`. Consider: `abstract record MailboxCursor` with subtypes `NeverSynced` and `SyncedTo(DateOnly Date, long? Uid)`.

**M2 — `Importer.UpdateDetails` validates length before trimming** (`Entities/Importer.cs:49-50`)  
A 201-char string with trailing spaces is rejected even though the trimmed version is ≤ 200. Fix: trim first, then validate length (same pattern as `Filing.CreateFromIncome`). Same bug in `Create` (line 25-28).

**M3 — `FilingInfo` positional record has no validation** (`ValueObjects/FilingInfo.cs`)  
Anyone can construct `new FilingInfo(…, grossIncomeRsd: -999, …)`. Since this represents computed tax data, add non-negative guards matching the checks in `Filing.CreateFromIncome`.

**M4 — `Filing` internal constructor inaccessible from tests** (`Entities/Filing.cs:47`)  
Comment says *"used for EF Core hydration and test seeding"* but `Rentier.Domain.csproj` has no `InternalsVisibleTo` attribute. Either add `InternalsVisibleTo` for the test project or remove the misleading comment.

**M5 — `Filing.PayingEntity` has no max-length validation** (`Entities/Filing.cs:71-82`)  
`Ticker` is capped at 20 chars, `PaymentReference` at 200, but `PayingEntity` has no limit — could cause DB truncation errors downstream.

**M6 — Domain service is async** (`Services/TaxCalculationService.cs:16`)  
`CalculateAsync` takes a `Func<…, Task<ExchangeRate>>`, leaking an I/O concern into the domain. Consider accepting a pre-resolved rate dictionary (or synchronous lookup) and moving the async resolution to the Application handler.

### 🟡 Minor

**m1** — `FilingStatus` enum defined inside `Filing.cs` — all other enums live in `Enums/`. Move there.

**m2** — `Rentier.Domain.Enums.ExchangeRateSourceType?` fully-qualified unnecessarily in `Filing.cs:68` — there's already a `using` for it.

**m3** — `DateTime.UtcNow` used directly in `Mailbox.cs:37`, `Report.cs:42,85`, `SyncParameters.cs:28` — not testable. Consider `System.TimeProvider` (.NET 8+).

**m4** — `BusinessDayResolver` O(n) holiday lookup on every call — `holidays.Holidays.Contains(date)` is a linear scan. A `HashSet<DateOnly>` built in `HolidayConf`'s constructor would drop this to O(1).

**m5** — Dead code branch in `TaxCalculationService.cs:54` — inside `if (whtAmount > 0)` the ternary's else branch is unreachable due to earlier enforcement that `upperWht == upperIncome`. Simplify to `var whtRate = incomeRate;`.

**m6** — `TaxpayerProfile` has no field-length limits — `FullName`, `Address`, `OpstinaCode` have no max-length checks — could cause DB truncation.

**m7** — `BusinessDayResolver.FindPreviousBusinessDay` re-checks the input date unnecessarily after confirming it's not a business day.

**m8** — `DomainException` is missing the standard parameterless constructor.

### ✅ What's Done Well
- `decimal` everywhere for money/rates/tax — no `double`/`float`
- `DateOnly` everywhere for dates — no `DateTime` leaking into domain
- Filing state machine `Init→Filed→Paid` correctly enforced with pattern matching
- Report state machine `Init→Processed/Error/PartialError` correctly enforced
- No external dependencies in `.csproj` — pure C# domain
- Defensive copies in `HolidayConf` constructor (`.ToArray()`)
- `ExchangeRate` is `sealed` with get-only properties preventing `with` bypass
- Filing deadline correctly adds 30 days then advances past weekends/holidays
- WHT credit cap: `Math.Max(grossTax - whtPaid, 0)` correctly prevents negative tax

---

## 3. Application Layer Review

*78 files reviewed: 24 commands, 31 handlers, 7 queries, 21 DTOs, 10 interfaces, 6 repositories, 1 service, 7 parsing.*

### 🔴 Critical

**C1 — `ProcessReportsCommandHandler` uses exception-as-flow-control** (`Handlers/ProcessReportsCommandHandler.cs:115-129`)  
Lines 115, 118, 121, 129 throw `InvalidOperationException` for data integrity failures (missing attachment, importer, taxpayer profile, parse failure). These are caught by the outer `catch (Exception)` and converted to `ReportStatus.Error`. They work but violate the constitution's *"no exception-as-flow-control"* rule. Fix: return a `Result<T, Error>` from `ProcessReportAsync` and handle upstream.

**C2 — `SyncMailboxCommand` embeds `IProgress<SyncProgress>` in the command record** (`Commands/SyncMailboxCommand.cs`)  
A command record should be a pure data bag. Embedding `IProgress<T>` (a UI/infrastructure concern) couples it to the presentation layer and makes the command non-serializable. Fix: pass `IProgress<SyncProgress>` as a separate handler parameter.

**C3 — `UpdateFilingStatusCommand` imports `FilingStatus` from `Rentier.Domain.Entities`** (`Commands/UpdateFilingStatusCommand.cs:1`)  
`using Rentier.Domain.Entities;` is needed solely for `FilingStatus`, because the enum is defined inside `Filing.cs` rather than in `Domain.Enums`. Root cause: see Domain M1 (`FilingStatus` placement). Same issue in several DTOs.

### 🟠 Major

**M1 — `IHolidayRepository` in `Interfaces/` while all others are in `Repositories/`**  
Inconsistent placement. Move to `Repositories/` for consistency.

**M2 — ~60 lines duplicated between `CalculateManualFilingCommandHandler` and `CreateManualFilingCommandHandler`**  
Both share: validation, holiday loading, rate resolution, WHT computation, tax calculation. Only the last step differs (return preview vs persist). Extract a shared `ManualFilingCalculator` internal service.

**M3 — `GetReportsQueryHandler` does N+1 queries per report** (`Handlers/GetReportsQueryHandler.cs:48-49`)  
For each report: `GetFilingCountByReportIdAsync` + `GetEarliestIncomeDateByReportIdAsync` = 2 DB calls per row. Fix: add a batch-projected repository method returning `(ReportId, FilingCount, EarliestDate)` in one query.

**M4 — `GetReportsQueryHandler` paginates in memory** (`Handlers/GetReportsQueryHandler.cs:40-62`)  
`_reports.GetAllAsync()` fetches all reports into memory, then `.Skip().Take()`. Implement `GetPagedAsync` on `IReportRepository` like `IFilingRepository` already has.

**M5 — `BulkDeleteReportsCommandHandler` cascade deletion is not atomic** (`Handlers/BulkDeleteReportsCommandHandler.cs:37-41`)  
If `DeleteManyAsync` fails after some `DeleteByReportIdAsync` calls succeeded, orphaned filings may have been deleted without their parent report. Wrap in a transaction or unit-of-work.

**M6 — `ExchangeRateResolver` is a concrete class with no interface**  
Handlers depend directly on the concrete `ExchangeRateResolver`. Extract an `IExchangeRateResolver` interface for testability and DI correctness.

**M7 — `AddImporterCommandHandler` calls `Create` then immediately `UpdateDetails`** (`Handlers/AddImporterCommandHandler.cs:39-48`)  
Double mutation on a new entity is a design smell — `Create` should accept all parameters, or `UpdateDetails` should be folded into a full factory method.

**M8 — `EnsureHolidaysSeededCommandHandler` seeds only current year but claims +3 year range** (`Handlers/EnsureHolidaysSeededCommandHandler.cs:33-46`)  
Only `currentYear` holidays are seeded, but `HolidayYearRange(currentYear, currentYear + 3)` is written. Years `+1` through `+3` are claimed but empty — potentially causing missed deadline calculations for future-dated income.

### 🟡 Minor

**m1** — Inconsistent error code naming — `DOMAIN_ERROR`, `DOMAIN_VALIDATION`, `NOT_FOUND`, `IMPORTER_NOT_FOUND`, `BULK_DELETE_FILINGS_INVALID` etc. Define error code constants (`Error.Codes.*`) for consistency.

**m2** — `BulkDeleteFilingsCommand.FilingIds` null check is dead code (NRT-enabled, required positional param cannot be null at runtime).

**m3** — `FetchHolidaysFromWebCommandHandler` returns success on partial failure with no indication of which years failed.

**m4** — `SyncAllCommandHandler` hardcodes `mailboxesSynced = 1` regardless of actual mailbox count (`Handlers/SyncAllCommandHandler.cs:43`).

**m5** — `Regex` validation in `AddImporterCommandHandler`/`UpdateImporterCommandHandler` creates throwaway `new Regex(pattern)` instances. Use `Regex.IsMatch` or a static `TryCreate` helper.

**m6** — `Result<T, TError>` lacks `Map`/`Bind` combinators, forcing verbose `if (!result.IsSuccess) return Failure(...)` chains throughout every handler.

**m7** — `IXmlFilingSerializer.Serialize` is synchronous — minor inconsistency with the "all I/O methods are async" rule (though XML serialization is CPU-bound).

**m8** — Regex validation creates throwaway instances — `new Regex(pattern)` allocated solely for validation before being GC'd.

### ✅ Strengths
- Layer separation is excellent — zero references to `Rentier.Infrastructure` or `Rentier.Desktop`
- Result pattern used consistently across all handler return types
- `OperationCanceledException` is always re-thrown (correct cancellation pattern)
- Commands/Queries are clean immutable records with good naming
- Interface segregation is solid — small, focused repository interfaces
- Domain exceptions are caught and mapped to `Error` at handler boundaries consistently

---

## 4. Infrastructure Layer Review

*31 files reviewed across 8 subdirectories.*

### 🔴 Critical

**C1 — `MacOsCredentialStore` — password visible in process arguments** (`Security/MacOsCredentialStore.cs:26`)  
The `-w secret` argument is passed to `security add-generic-password`, making the password visible in `ps aux` while the process runs. Fix: pipe the secret via stdin instead (`-w` without a value reads stdin on macOS).

**C2 — `NbsWebScraper` — bare `catch {}` swallows all exceptions including `OperationCanceledException`** (`Scraping/NbsWebScraper.cs:104`)  
`try { await _cache.SaveBatchAsync(...); } catch { }` catches everything, meaning a user cancellation during cache write is silently swallowed. Fix: `catch (Exception ex) when (ex is not OperationCanceledException)`.

**C3 — `WindowsCredentialStore.GetCredentialAsync` — read blob not zeroed** (`Security/WindowsCredentialStore.cs:113-115`)  
After decoding the credential blob, `byte[] blob` containing the plaintext secret is never `Array.Clear`-ed. The write path correctly zeroes it (line 90) but the read path does not, leaving the secret in managed memory longer than necessary.

### 🟠 Major

**M1 — `ImapMailboxSyncService` — N+1 per-attachment DB round-trips** (`Sync/ImapMailboxSyncService.cs:117-129`)  
For every attachment in every email: one `ExistsByImporterAndNameAsync` call + one `AddAsync` + one `SaveChangesAsync`. For a large mailbox this is extremely slow. Fix: collect all candidates, do a single `WHERE IN` existence check, then `AddRange` + one `SaveChangesAsync`.

**M2 — `PpOpoXmlSerializer.MapIncomeType` throws on unknown `IncomeType`** (`Serialization/PpOpoXmlSerializer.cs:97-102`)  
`ArgumentOutOfRangeException` thrown for unknown income types violates the Result pattern rule. Adding a new `IncomeType` enum value crashes at runtime instead of returning a user-friendly error.

**M3 — `ExchangeRateCacheConfiguration` — `HasPrecision(18, 6)` is a no-op on SQLite** (`Persistence/ExchangeRateCacheConfiguration.cs:22`)  
SQLite has no native decimal/numeric type so EF Core `HasPrecision` is documentation-only. Add a comment or explicit `HasColumnType("TEXT")` to prevent future confusion. Same applies to `FilingConfiguration` precision annotations.

**M4 — `HolidayRepository.SaveHolidaysAsync` — mixed transaction paradigm** (`Repositories/HolidayRepository.cs:44-65`)  
`ExecuteDeleteAsync` runs directly against the DB (bypasses change tracker), then `AddRange` through the tracker, then `ExecuteUpdateAsync` again — without an explicit transaction. On failure this could leave holidays deleted but year range not updated.

**M5 — `InfrastructureServiceExtensions` registers Application handlers** (`InfrastructureServiceExtensions.cs:60-65`)  
`SyncMailboxCommandHandler` and `ProcessReportsCommandHandler` are Application layer classes registered in the Infrastructure DI extension. Handler registration should live in an Application-layer extension method.

**M6 — `AppDbContext` registered as `Transient`** (`InfrastructureServiceExtensions.cs:27-29`)  
`Transient` means every repository injection gets a fresh context, defeating unit-of-work patterns. Two repositories injected into the same handler get different contexts. `Scoped` is the standard EF Core lifetime. If Transient was intentional for the desktop app (no DI scopes), document why.

### 🟡 Minor

**m1** — `MacOsCredentialStore` — `Task.Run(async () => ...)` anti-pattern (lines 22, 39, 59) — unnecessary overhead wrapping an already async-compatible lambda.

**m2** — `NbsExchangeRateFetcher` — `HttpResponseMessage response` is not in a `using` block (`Scraping/NbsExchangeRateFetcher.cs:51-65`). Use `using var response = ...`.

**m3** — `TaxpayerProfileRepository` uses `_context` while all other repositories use `_db`. Minor but breaks consistency.

**m4** — `ImapMailboxSyncService` is not `sealed` with a `protected virtual CreateClient()`. Consider an `IImapClientFactory` interface instead for better DI alignment.

**m5** — `CompositeExchangeRateFetcher` constructor takes concrete `NbsExchangeRateFetcher` and `NbsWebScraper` instead of an `IExchangeRateFetcher` interface.

**m6** — `IbkrCsvParser.StripIsin` — `description.Split('(')[0].Trim()` breaks if entity name contains `(`. Use a regex matching the ISIN pattern `\([A-Z]{2}[A-Z0-9]{9}\d\)$` instead.

**m7** — `TimeAndDateHolidayScraper` — `Configuration.Default.WithDefaultLoader()` enables HTTP loading inside AngleSharp unnecessarily (HTML is already fetched via `HttpClient`). Use `Configuration.Default`.

**m8** — `FilingRepository.GetFilingStatsAsync` — two DB round-trips (one for GroupBy counts, one for unpaid amounts). Could be combined into a single projected query.

### ✅ Strengths
- Result pattern used consistently at all boundaries (HTTP, IMAP, credential stores)
- No passwords in SQLite — strict OS credential store usage
- `DateOnly` used throughout; `DateTime` conversion happens at infrastructure boundaries
- `decimal` for all monetary values
- `CancellationToken` propagated through virtually all async methods
- EF Core configurations properly separated into `IEntityTypeConfiguration<T>` classes
- Composite pattern for exchange rate fetching with intelligent fallback logic
- `InternalsVisibleTo` in place for test project access to internal members

---

## 5. Desktop / UI Layer Review

*54 files reviewed: 18 ViewModels, 22 Views (axaml + code-behind), 9 Converters, 2 Dialogs, 3 Extensions, App, Program, ViewLocator, CompositionRoot.*

### 🔴 Critical

**C1 — `ManualFilingViewModel` injects `ITaxpayerProfileRepository` directly** (`ViewModels/ManualFilingViewModel.cs:41,130`)  
The ViewModel calls `_profileRepository.GetAsync()` (line 208), bypassing the Application layer. Constitution rule: *"Desktop calls Application use cases only — never touches Infrastructure directly."* Fix: create a `GetTaxpayerProfileQuery` handler call instead of injecting the repository interface.

**C2 — `_rowSubscriptions` never disposed on ViewModel teardown** (`FilingsViewModel.cs:172`, `ReportsViewModel.cs:180`)  
Both ViewModels hold a `CompositeDisposable _rowSubscriptions` that is cleared on page load but never added to `WhenActivated` disposables and never disposed on deactivation. Memory leak on every activate/deactivate cycle. Fix: `Disposable.Create(() => _rowSubscriptions.Clear()).DisposeWith(disposables)` inside `WhenActivated`.

**C3 — Fire-and-forget `LoadProfileAsync()` in constructor** (`ManualFilingViewModel.cs:199`)  
`_ = LoadProfileAsync()` discards the Task. An unexpected uncaught exception becomes an unobserved task exception. Fix: use `Observable.FromAsync` inside `WhenActivated` consistent with all other ViewModels.

**C4 — `DataGrid_Sorting` is a no-op stub** (`Views/FilingsView.axaml.cs:32-35`)  
The handler sets `e.Handled = false` but never routes to `ApplySortCommand`. DataGrid performs local in-memory sort of the current page only. Users see a sort happening but data across pages remains server-ordered — a data correctness bug. Fix: wire the handler to `ApplySortCommand`, or set `CanUserSortColumns="False"` and use explicit sort buttons.

### 🟠 Major

**M1 — `MainWindowViewModel` does not implement `IActivatableViewModel`**  
The only ViewModel without it. The `WhenAnyValue` subscription on `SelectedEntry` (line 101) is never disposed.

**M2 — Undisposed `.Subscribe()` calls in property setters** (`FilingsViewModel.cs:67,79`, `ReportsViewModel.cs:159`)  
`ShowAll`, `ReportIdFilter`, and `SortDescending` setters all call `LoadPageCommand.Execute().Subscribe()` without capturing or disposing the `IDisposable`. Use `InvokeCommand` on the observable instead.

**M3 — `SyncView.axaml.cs` auto-scroll always scrolls to end** (`SyncView.axaml.cs:17-22`)  
Comment says "only if user is near bottom" but `ScrollToEnd()` is called unconditionally, preventing users from scrolling up to review earlier log entries during a long sync.

**M4 — Hardcoded strings bypassing `Strings.resx` localization** — `SyncView.axaml`, `SyncViewModel.cs:153-171`, `ImportDialogHelper.cs:117,133`, `HolidaySettingsView.axaml:39`, `ReportTypeExtensions.cs:9`. Extract to resource strings.

**M5 — `SyncProgressEntryViewModel` does not extend `ReactiveObject`** (`ViewModels/SyncProgressEntryViewModel.cs:5`)  
All ViewModels extend `ReactiveObject` per project convention. This one is a plain class. The `ViewLocator.Match` check for `ReactiveObject` may not find it correctly.

**M6 — `FilingStatusBrushConverter` allocates a new `SolidColorBrush` on every conversion** (`Converters/FilingStatusBrushConverter.cs`)  
Called per-row, per-render. Cache brushes as static fields.

**M7 — `ReactiveUserControl<T>` vs `UserControl` inconsistency in XAML root tag** — Several views (`ReportsView`, `SyncView`, `SettingsView`, `HolidaySettingsView`, `MailboxSettingsView`, `ImporterSettingsView`) use raw `<UserControl>` in AXAML while their code-behind correctly inherits `ReactiveUserControl<T>`. The AXAML root tag should also be `<reactive:ReactiveUserControl>` for the `WhenActivated` lifecycle to work reliably.

**M8 — `MailboxSettingsViewModel` stores password in a plain `string` property** (`ViewModels/MailboxSettingsViewModel.cs:27,52-56`)  
The raw password string is bound to a ViewModel property and passed through the CQRS pipeline. Verify the handler routes to the OS credential store and that the string is not logged, serialized, or persisted anywhere.

### 🔵 Minor

**m1** — `DashboardView.axaml` has `x:CompileBindings="False"` — loses compile-time binding validation despite `AvaloniaUseCompiledBindingsByDefault=true` in csproj.

**m2** — `HasOverdueFilings` derived property manually raised on line 145 — fragile. Use `ObservableAsPropertyHelper` derived from `OverdueFilings` count changes.

**m3** — `MailboxItemViewModel.DisplayName` computed property not backed by `ObservableAsPropertyHelper` — if `Host`, `Port`, or `Username` change independently, `DisplayName` won't update reactively.

**m4** — `ProfileSettingsView.axaml` has `x:DataType` set but bindings aren't actually compiled. Either enable compiled bindings or remove the unused directive.

**m5** — Several ViewModels use `System.Reactive.Unit` fully-qualified — add `using System.Reactive;` at the top.

**m6** — `InvertBoolConverter` uses the two-arg `FuncValueConverter<bool, bool>` overload which is easy to misread in reviews — add a brief XML doc comment.

### 🧭 User Flow Analysis

**Dashboard → Filings** ✅ — Summary cards, overdue list, upcoming deadlines all work. "View All Filings" navigates correctly.

**Filings → Manual Filing → Back to Filings** ✅ — "New Filing" navigates to form. Cancel returns. Save computes tax, creates filing, navigates back. `ShowAll = true` ensures new filing is visible on return.

**⚠️ Filings sort is broken** — DataGrid header click triggers local in-memory sort only (current page), not server-side sort. Cross-page data is inconsistent. (See C4)

**⚠️ Filings: No "active filter" indicator when navigating from Reports** — `ReportIdFilter` is set from Reports → View Filings navigation, but the Filings view shows no banner indicating a filter is active, and provides no "Clear filter" button. Users see a subset of filings with no explanation.

**Reports → Filings drill-down** ⚠️ — Clicking "View Filings" on a report navigates correctly, but the missing filter indicator (above) leaves users confused.

**Settings: Profile → Save** ✅ — Form validation disables Save when JMBG invalid or required fields empty. Success/error messages shown.

**Settings: Holidays** ✅ — Year-range filter, add/delete rows, fetch-from-web, save all wired. Empty state shown. ⚠️ No unsaved-changes warning when navigating away (`HasUnsavedChanges` is tracked but never guarded on navigation).

**⚠️ Settings: Mailboxes → Delete** — `DeleteMailboxCommand` fires immediately with no confirmation dialog. Destructive operation — add a confirmation step consistent with filing/report delete flows.

**⚠️ Settings: Importers → Delete** — Same issue. `OnDeleteAsync` deletes without user confirmation.

**⚠️ Sync → Auto-navigate on success** — If sync completes with zero errors, `_navigateToFilings()` fires automatically (`SyncViewModel.cs:227`). User may want to review the sync log first. Consider keeping on the Sync page with a "View Filings" button instead.

**Manual Filing: No IncomeDate null guard** — Form allows submitting without selecting a date. While `CalculateCommand` checks `date != null`, a binding misbehaviour could produce `DateOnly.MinValue`.

---

## 6. Domain Test Coverage Review

*14 test files reviewed against 15 production files. ~120 test methods total.*

### FilingCreateFromIncomeTests ✅ Good

- All main paths covered (valid, null/empty PayingEntity, negative amounts, with/without ReportId, exchange rate variants)
- ⚠️ Minor: Does not assert `Id != Guid.Empty`
- ⚠️ Minor: Zero amounts (boundary at 0m) not explicitly asserted
- ⚠️ Minor: Several test names missing the "state" part (e.g., `CreateFromIncome_TrimsPayingEntity` → should be `CreateFromIncome_PayingEntityWithWhitespace_TrimsAndStores`)

### FilingPaymentReferenceTests 🟢 Complete

All paths covered: null→null, empty→null, whitespace→null, valid string trimmed, exactly 200 chars, over 200 chars, second-call overwrite.

### FilingStatusTransitionTests ⚠️ Gaps

- ✅ `Init→Filed`, `Filed→Paid` (valid)
- ✅ `Paid→Init`, `Init→Paid`, `Filed→Init` (invalid)
- 🔴 **Critical:** Same-state transitions not tested: `Init→Init`, `Filed→Filed`, `Paid→Paid` (all should throw)
- 🟠 **Major:** `Paid→Filed` not tested (invalid, should throw)
- 🟠 **Major:** Valid transitions only assert `NotThrow()` — the actual resulting `Status` value is not verified

### FilingTickerTests 🟢 Complete

Excellent boundary coverage: valid, null, whitespace-only, empty, trimming, exactly 20, over 20.

### HolidayYearRangeTests ⚠️ Gaps

- ✅ Valid range, below min, exceeds max span, end < start, end == start+10
- 🟠 **Major:** Properties not asserted — no test checks `Id == SingletonId`, `StartYear`, `EndYear` values after construction
- ⚠️ Minor: Single-year range (`StartYear == EndYear`) not tested as boundary

### ImporterTests ⚠️ Gaps

- ✅ `Create`: valid, empty, whitespace, >200, ==200, default ReportType, unique Id
- ✅ `UpdateDetails`: valid, empty name, null FKs, null filters→"", notes >4000
- 🟠 **Major:** `UpdateDetails` with `displayName > 200` not tested (independent code path from Create)
- 🟠 **Major:** `Create` trimming not verified
- ⚠️ Minor: `UpdateDetails` trimming not tested
- ⚠️ Minor: `Create` with explicit `ReportType` parameter (only default tested)
- ⚠️ Minor: `UpdateDetails` with `PaymentNotes` exactly 4000 chars boundary untested

### MailboxTests ⚠️ Gaps

- ✅ `Create`: valid, empty/whitespace host, port 0/65536/65535, empty username, cursor defaults, unique Id
- ✅ `UpdateDetails`: valid, empty host, invalid port
- ✅ `UpdateCursor`: valid, null
- 🟠 **Major:** `UpdateDetails` empty/whitespace username not tested (production validates it)
- ⚠️ Minor: Port boundaries (port=1, port=-1) not tested

### PublicHolidayTests ⚠️ Gaps

- ✅ Valid inputs, empty name, `Year` matches `Date.Year`
- 🟠 **Major:** `null` name not tested (should throw DomainException)
- ⚠️ Minor: Only 3 tests total — thin coverage for an entity

### ReportTests 🔴 Critical Gaps

- ✅ `Create`: valid, empty name, >500, null attachment, today's date
- ✅ `SetStatus`: `Init→Processed`
- ✅ `CreateRevision`: linked, Init status, new Id, long-name truncation, null original
- 🔴 **Critical:** `SetStatus` — `Init→Error` and `Init→PartialError` not tested (valid transitions)
- 🔴 **Critical:** `SetStatus` — invalid transitions completely missing (`Processed→*`, `Error→*`, `PartialError→*`)
- 🟠 **Major:** `Create` with `emailDate` parameter never tested
- 🟠 **Major:** `Create` name trimming not verified
- 🟠 **Major:** Name exactly 500 chars boundary untested

### SyncParametersTests ⚠️ Gaps

- ✅ Default, ReplayFromDate±, Incremental/FullReplay with date, `GetEffectiveStartDate` (3 modes), `ScopeImporterId`
- 🟠 **Major:** `GetEffectiveStartDate` with Incremental + null cursor → should return null (untested)
- ⚠️ Minor: `FullReplay` + `ScopeImporterId` combination untested

### TaxpayerProfileTests 🟢 Near-Complete

- ✅ Valid args, optional fields, invalid JMBG (5 cases), null/empty/whitespace for required string fields
- ⚠️ Minor: `null` JMBG not in InlineData (only empty/whitespace tested)

### BusinessDayResolverTests ⚠️ Gaps

- ✅ `IsBusinessDay`: Mon/Sat/Sun, weekday holiday, weekday no holiday
- ✅ `WalkBackward`: Mon/Sun/Sat, holidays, consecutive holidays, max lookback edge cases, weekend blocks
- ✅ `FindPreviousBusinessDay`: on business day, on weekend
- 🟠 **Major:** Null holidays → DomainException for all 3 methods (none tested)
- 🟠 **Major:** `maxLookbackDays < 1` → DomainException (untested)
- 🟠 **Major:** `FindPreviousBusinessDay` on a holiday weekday (should walk backward) untested
- 🟠 **Major:** `FindPreviousBusinessDay` with no business day found → DomainException untested

### FilingDeadlineCalculatorTests 🟢 Near-Complete

Excellent coverage: weekday, Sat→Mon, Sun→Mon, weekday holiday, holiday-on-Sat, consecutive holidays, Sat→holiday-Mon, leap year, null holidays, max iterations, empty holidays.

### TaxCalculationServiceTests ⚠️ Gaps

- ✅ Dividend+WHT, WHT>tax (clamp), zero income, zero WHT skips rate call, negative income/WHT, currency mismatch, null/empty entity, null provider, Interest type, rounding, currency normalization
- 🟠 **Major:** Null/empty/whitespace `incomeCurrency` → DomainException (untested)
- 🟠 **Major:** Null/empty/whitespace `whtCurrency` → DomainException (untested)
- ⚠️ Minor: `CancellationToken` honored (`OperationCanceledException`) not tested

### 🚫 Missing Test Classes

| Severity | Value Object | Missing Tests For |
|----------|-------------|-------------------|
| 🔴 Critical | `ExchangeRate` | Null/whitespace currency check, rate ≤ 0 check, uppercase normalization — zero domain unit tests |
| 🟠 Major | `HolidayConf` | Null holidays check — zero domain unit tests |
| ⚠️ Minor | `Money`, `MailboxCursor`, `FilingInfo` | Plain records with no validation — tests optional |

### Domain Test Structural Issues

- **Namespace**: All test files use `namespace Rentier.UnitTests;` — Service sub-tests don't use a sub-namespace. Consistent but flat.
- **Naming**: ~8 tests have names missing the state part (e.g., `CreateFromIncome_TrimsPayingEntity`)
- No redundant tests or implementation-detail tests found
- FluentAssertions used correctly throughout

---

## 7. Application Test Coverage Review

*34 test files reviewed against 31 handlers. All 31 handlers have test coverage — no untested handlers.*

### 🔴 Critical — None found

No handler is missing its primary success path or a critical safety-net test.

### 🟠 Major (6 findings)

**1 — `CreateManualFilingCommandHandler` — Missing validation path tests**  
`DATE_REQUIRED` (default IncomeDate) and `NET_NEGATIVE` (negative NetReceived) are tested in `CalculateManualFiling` but **not in `CreateManualFiling`**, which is a separate handler with its own validation code paths.

**2 — `DeleteMailboxCommandHandler` — Credential-delete failure path untested**  
Handler has a branch: when `credResult.Error.Code != "CREDENTIAL_NOT_FOUND"`, it logs a warning but still deletes from DB. No test verifies this "log-and-continue" behavior.

**3 — `UpdateMailboxCommandHandler` — Domain validation path untested**  
Handler catches `DomainException` from `mailbox.UpdateDetails()` and returns `DOMAIN_VALIDATION`. No test triggers this path (e.g., empty host on update). `AddMailboxCommandHandlerTests` tests this for `Create` but `Update` is a separate handler.

**4 — `ProcessReportsCommandHandler` — Multi-report batch untested**  
All tests process exactly 1 report. Counter accumulation across 2+ reports is untested — a counter bug would be invisible.

**5 — `ProcessReportsCommandHandler` — WHT matching logic not validated**  
Tests create dividends and verify filings, but never verify WHT records are correctly matched by `(Date, EntityName, Currency)`.

**6 — `SyncMailboxCommandHandler` — Multiple importers per mailbox untested**  
Handler groups by `MailboxId` and passes importer lists per sync call. Tests only ever have one importer per mailbox — grouping/parameter-passing bugs are invisible.

### 🟡 Minor (8 findings)

**7** — `ExportFilingCommandHandler` — Filing with `ReportId` set but report deleted from DB → `paymentNotes` stays empty (untested edge case).

**8** — `ExportFilingCommandHandler` — Both `Ticker` and `PayingEntity` null/whitespace → filename should use `"filing"` fallback. Untested.

**9** — `GetReportsQueryHandler` — `EmailDate` priority in display name `(r.EmailDate ?? earliest ?? r.ImportDate)` — the `EmailDate` scenario never exercised.

**10** — `GetFilingsQueryHandler` — `PageSize > 100` validation path not tested (only `PageSize = 0`).

**11** — `SaveTaxpayerProfileCommandHandler` — `PhoneNumber` and `Email` optional parameters not verified in persistence assertions.

**12** — `SyncAllCommandHandler` — Both sync **and** process failing simultaneously not tested.

**13** — `ProcessReportsCommandHandler` — Interest duplicate detection (existing interest filing skipped) not explicitly tested; only dividend duplicates.

**14** — `AddImporterCommandHandler` — Valid non-empty regex path not tested (all valid tests use empty regex `""`).

### ✅ Excellently Covered Handlers

`AddMailboxCommandHandler`, `BulkDeleteFilingsCommandHandler`, `BulkDeleteReportsCommandHandler`, `CalculateManualFilingCommandHandler`, `DeleteFilingCommandHandler`, `DeleteImporterCommandHandler`, `DeleteReportCommandHandler`, `EnsureHolidaysSeededCommandHandler`, `FetchHolidaysFromWebCommandHandler`, `GetHolidayConfQueryHandler`, `GetImportersQueryHandler`, `GetMailboxesQueryHandler`, `GetTaxpayerProfileQueryHandler`, `ImportHolidaysFromWebCommandHandler`, `ImportReportCommandHandler`, `UpdateFilingStatusCommandHandler`, `UpdateImporterCommandHandler`, `UpdatePaymentReferenceCommandHandler`, `ExchangeRateResolver`, `GetDashboardQueryHandler`

### Application Test Quality

- ✅ All test methods follow `MethodName_StateUnderTest_ExpectedBehavior`
- ✅ FluentAssertions used consistently and correctly
- ✅ NSubstitute mocks test handler behavior, not mock setup
- ✅ Domain objects are real (no mocking of domain)
- ✅ No `.Result` / `.Wait()` — all async

---

## 8. Infrastructure Test Coverage Review

*25 test files reviewed against 16 production classes.*

### `ExchangeRateCacheRepository` 🟢 Excellent (7 tests)
`GetAsync`, `SaveAsync`, `SaveBatchAsync`, `GetByDateRangeAsync` — all covered. Upsert, case-insensitive currency lookup, date range filtering — all tested. No gaps.

### `FilingRepository` ⚠️ Two Critical Gaps (26 + 11 + 2 tests)
CRUD, paging, sorting, filtering, dashboard stats — thoroughly covered.
- 🔴 **Critical:** `GetByTaxPeriodAsync` — untested (used for dedup in production)
- 🔴 **Critical:** `GetEarliestIncomeDateByReportIdAsync` — untested (used in report display)
- 🟠 **Major:** `DeleteManyAsync` — untested (batch delete)
- 🟠 **Major:** `GetPagedAsync` with `FilingSortColumn.IncomeType`, `TaxPayable`, `PaymentReference` — untested sort columns

### `HolidayRepository` 🟢 Good (4 tests)
All 3 public methods covered. Replace-all semantics, sorting, year-range singleton tested. No gaps.

### `ImporterRepository` 🟢 Excellent (7 tests)
Full CRUD coverage with positive + negative paths. No gaps.

### `MailboxRepository` 🟢 Excellent (7 tests)
Full CRUD with positive + negative paths. No gaps.

### `ReportRepository` 🔴 Critical Gaps (7 tests)
Add, GetAll, GetByStatus, ExistsByImporterAndName, duplicate constraint — covered.
- 🔴 **Critical:** `GetByIdAsync` — untested
- 🔴 **Critical:** `GetByImporterAsync` — untested
- 🟠 **Major:** `UpdateAsync` — untested
- 🟠 **Major:** `DeleteAsync` — untested
- 🟠 **Major:** `DeleteManyAsync` — untested
- ⚠️ Minor: `GetAllAsync(sortDescending: false)` ascending path untested

### `TaxpayerProfileRepository` 🟢 Good (4 tests)
Get, Save (insert + update), Delete — all covered. No gaps.

### `NbsExchangeRateFetcher` 🟢 Excellent (10 tests)
Unsupported currency, cache hit, cache miss+parse, currency-not-in-response, HTTP error, malformed XML, unit scaling (JPY), case-insensitive, empty response — all covered.

### `NbsWebScraper` 🟢 Good (7 tests)
Valid HTML, comma decimal, empty rows, no table, wrong column count, HTTP 500, connection exception — covered.
- ⚠️ Minor: Cache hit path untested (mock always returns null)
- ⚠️ Minor: Currency not found in parsed table untested

### `CompositeExchangeRateFetcher` 🟠 Architectural Issue (6 tests)
Tests use a hand-rolled `TestComposite` class that re-implements the fallback logic instead of testing the real `CompositeExchangeRateFetcher`. **The actual production class is untested.** Changes to the real class won't be caught by these tests.

### `IbkrCsvParser` 🟢 Excellent (12 tests + 9 CSV fixtures)
Happy path, multi-dividend aggregation, different dates, interest debit/credit, WHT currency mismatch, WHT unmatched, malformed row skip, empty sections, null stream, WHT positive amount, FX non-positive — covered. `StripIsin` internal tested directly.
- ⚠️ Minor: `INVALID_FORMAT` (no recognized sections) untested
- ⚠️ Minor: `PARSE_EXCEPTION` catch-all path untested
- ⚠️ Minor: Duplicate FX rate (`RATE_DUPLICATE`) warning untested

### `TimeAndDateHolidayScraper` 🟢 Good (6 tests + fixture)
Real fixture with 11 holidays, first-entry validation, specific holiday checks, no-table error, hide-rows-only error, duplicate dedup — covered.
- ⚠️ Minor: HTTP failure path (`HOLIDAY_IMPORT_FAILED`) untested

### `PpOpoXmlSerializer` 🟢 Excellent (18 tests + 1 snapshot)
Root element, namespace, encoding, section structure, monetary formatting, date formatting, OsnovicaZaPorez mapping, income type codes, null phone, payment notes, CDATA absence, zero amounts, Kamata section — thoroughly covered.
- ⚠️ Minor: Unsupported `IncomeType` throwing `ArgumentOutOfRangeException` untested

### `ImapMailboxSyncService` 🟠 Gaps (6 tests)
Credential failures, null guards, `BuildReportName` — covered.
- 🟠 **Major:** The happy path (successful IMAP connect → message processing → report creation → cursor update) is **completely untested**. Both `ImapSyncIntegrationTests` tests are permanently skipped with `Skip = "Requires live IMAP server"`.

### `CredentialStoreFactory` 🟢 Good (7 tests)
Platform-conditional tests with graceful skip. `ProviderInfo.ToString`, `Error.UnsupportedPlatform` — covered. No gaps.

### `WindowsCredentialStore` / `MacOsCredentialStore` / `LinuxCredentialStore` 🟢 Good
All credential stores: Save/Get round-trip, overwrite, absent key, delete, idempotent delete — covered. Platform-gated correctly.

### `NullCredentialStore` 🟠 Completely Untested
No tests exist. 3 methods that always return the injected error. Simple to test; verifies the fallback DI contract.

### Infrastructure Test Quality

| Area | Assessment |
|------|-----------|
| Naming convention | ✅ `MethodName_StateUnderTest_ExpectedBehavior` throughout |
| FluentAssertions | ✅ Correct and idiomatic |
| SQLite in-memory setup | ✅ `SqliteConnection` kept open + `EnsureCreatedAsync` |
| `IAsyncLifetime` disposal | ✅ `InitializeAsync` / `DisposeAsync` properly implemented |
| Trait categorization | ✅ `[Trait("Category", "Integration")]` and `"Live"` correctly applied |

### Infrastructure Test Gap Summary

| Severity | Class | Missing |
|----------|-------|---------|
| 🔴 Critical | `FilingRepository` | `GetByTaxPeriodAsync`, `GetEarliestIncomeDateByReportIdAsync` |
| 🔴 Critical | `ReportRepository` | `GetByIdAsync`, `GetByImporterAsync` |
| 🟠 Major | `ReportRepository` | `UpdateAsync`, `DeleteAsync`, `DeleteManyAsync` |
| 🟠 Major | `FilingRepository` | `DeleteManyAsync`, 3 untested sort columns |
| 🟠 Major | `CompositeExchangeRateFetcher` | Tests verify a re-implementation, not the real class |
| 🟠 Major | `ImapMailboxSyncService` | No happy-path test for IMAP connect → process → persist flow |
| 🟠 Major | `NullCredentialStore` | Entirely untested |

---

## 9. Desktop Test Coverage Review

*18 ViewModels, 17 unit test files, 3 scenario tests, 1 E2E file, 5 headless Avalonia view tests reviewed. ~210 test methods total.*

---

### 🟢 Excellent Coverage

**FilingRowViewModel** — 22 tests. All commands, computed properties, status transitions, delegates, `CanExecute` conditions, and DTO mapping tested. No gaps.

**ManualFilingViewModel** — 25 tests. Initial state, `CalculateCommand` (success/error/loading), `SaveCommand` (success/duplicate/navigate), `CancelCommand`, input-clears-preview, no-profile error, `CanExecute` conditions. Outstanding.

**MailboxItemViewModel** — 4 tests. `From`/`UpdateFrom` mapping, `DisplayName` format, null optional fields. Adequate for a data VM.

**DashboardViewModel** — 8 unit tests + 5 headless view tests. Load success (collections, stats, formatting), load failure, `NavigateToFilingsCommand`, headless rendering/progress bar/error/overdue section.

**ReportsViewModel** — 24 tests + 18 bulk delete tests. Activation, load success/failure, import (cancel/success/fail), delete (cancel/success/fail), `ViewFilings` navigation, sync status, full pagination suite, sort reset, bulk delete (all `IsAllSelected` states, confirm/cancel).

---

### 🟡 Good Coverage — Minor Gaps

**FilingsViewModel** — 18 tests + 20 bulk delete tests. Core well covered.

| Severity | Missing Test |
|----------|-------------|
| 🟠 Major | `SavePaymentRefCommand` — zero tests (success, failure, reload) |
| 🟠 Major | `ExportCommand` — zero tests (success export+saveFile, failure) |
| ⚠️ Minor | `ClearErrorCommand` not tested |
| ⚠️ Minor | `NewFilingCommand` navigation delegate not tested |
| ⚠️ Minor | `ReportIdFilter` setter resetting page and reloading |
| ⚠️ Minor | `PageIndicator` formatting |

**HolidaySettingsViewModel** — 12 + 5 fetch tests. Core add/delete/save/fetch covered.

| Severity | Missing Test |
|----------|-------------|
| ⚠️ Minor | `FilteredEntries` / `IsFilteredEmpty` year-range filtering |
| ⚠️ Minor | `StartYear` / `EndYear` reactive rebuild |
| ⚠️ Minor | `SaveCommand` failure path |
| ⚠️ Minor | `LoadAsync` failure path |

**ProfileSettingsViewModel** — 9 tests (2 files). `CanExecute` validation, save success/failure, load existing/empty.

| Severity | Missing Test |
|----------|-------------|
| ⚠️ Minor | `IsLoading` state during save/load |
| ⚠️ Minor | `PhoneNumber` / `Email` optional field handling |

**ImporterSettingsViewModel** — 15 tests. Form population, add/edit/save well covered.

| Severity | Missing Test |
|----------|-------------|
| 🟠 Major | `DeleteCommand` execution — success/failure flow, not just `CanExecute` |
| ⚠️ Minor | `IsLoading` during operations |
| ⚠️ Minor | `LoadAsync` failure path |

**ReportRowViewModel** — 4 tests. Date formatting and DTO mapping covered.

| Severity | Missing Test |
|----------|-------------|
| ⚠️ Minor | `IsSelected` reactive property change |

---

### 🟠 Significant Gaps

**SyncViewModel** — 12 tests. Core sync flow tested (success/failure/cancel/progress/navigation). The **entire sync mode/strategy UI state machine is untested**:

| Severity | Missing Test |
|----------|-------------|
| 🟠 Major | `SelectedSyncMode` changes → `IsReplayFromDateMode` / `IsReplayMode` / `IsFullReplayMode` |
| 🟠 Major | `ImpactSummary` derived text (9 mode×strategy combos) |
| 🟠 Major | `ValidationError` (date required for ReplayFromDate, future date rejection) |
| 🟠 Major | `SyncCommand` `CanExecute` gated by `ValidationError` |
| 🟠 Major | `ReplayFromDateOffset` / `ReplayFromDate` conversion |
| ⚠️ Minor | `OperationCanceledException` handler (cancel adds log entry + summary) |

**MailboxSettingsViewModel** — 8 tests. Happy path only.

| Severity | Missing Test |
|----------|-------------|
| 🟠 Major | `SaveCommand` failure path |
| 🟠 Major | `DeleteCommand` failure path |
| 🟠 Major | `LoadAsync` failure → `ErrorMessage` |
| ⚠️ Minor | `IsLoading` during save/delete/load |
| ⚠️ Minor | `SuccessMessage` / `ErrorMessage` state management |

**MainWindowViewModel** — 2 smoke tests only (navigation entry count, initial ViewModel).

| Severity | Missing Test |
|----------|-------------|
| 🟠 Major | `SelectedEntry` → `CurrentViewModel` sync subscription |
| 🟠 Major | All cross-VM navigation flows: Dashboard→Filings, Reports→Filings(reportId), Sync→Filings, NewFiling→ManualFiling→Back |
| ⚠️ Minor | `ReportIdFilter` cross-VM navigation |

---

### 🔴 No Dedicated Tests

| ViewModel | Severity | Notes |
|-----------|----------|-------|
| `SyncProgressEntryViewModel` | ⚠️ Minor | Icon mapping (`Error→⚠`, `Info→•`), timestamp formatting untested. Only used indirectly in `SyncViewModelTests`. Simple data class. |
| `ImporterItemViewModel` | ⚠️ Minor | `From` mapping, `ReportTypeDisplay` untested. Only indirectly used. |
| `HolidayEntryViewModel` | ⚠️ Minor | `FromDto`/`ToDto` round-trip, `Name` property untested. 1 indirect test exists. |
| `NavigationEntry` | — | Trivial record — no tests needed. |

---

### Naming Assessment

✅ Almost all tests follow `MethodOrProperty_StateOrCondition_ExpectedBehavior`.

⚠️ **One misnamed file**: `SettingsViewModelTests.cs` actually tests `ProfileSettingsViewModel`. `SettingsViewModel` (the container for 4 tab VMs) has no direct tests.

---

### Scenario & E2E Tests

| Layer | Assessment |
|-------|-----------|
| Scenarios | 7 tests covering Filing lifecycle (state transitions) and TaxpayerProfile (CRUD + validation). Good integration coverage. Not Desktop-specific. |
| E2E | 2 tests, both `[Skip]`ped. FlaUI-based. Placeholder stage only. |

---

### Desktop Test Priority Summary

| Priority | ViewModel | Gap |
|----------|-----------|-----|
| 1 | `SyncViewModel` | 5 major gaps — entire mode/strategy UI state machine untested |
| 2 | `FilingsViewModel` | `SavePaymentRefCommand` + `ExportCommand` — 2 commands with zero tests |
| 3 | `MainWindowViewModel` | Only smoke-tested — all cross-VM navigation flows untested |
| 4 | `MailboxSettingsViewModel` | No failure-path tests for save/delete/load |
| 5 | `ImporterSettingsViewModel` | `DeleteCommand` execution (not just `CanExecute`) untested |

---

*Review generated by GitHub Copilot fleet (Claude Opus 4.6) on 2026-04-22.*
