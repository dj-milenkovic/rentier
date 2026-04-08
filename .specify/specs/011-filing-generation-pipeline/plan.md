# Implementation Plan — 011 Filing Generation Pipeline

**Branch**: `feature/010-011-sync-pipeline` | **Date**: 2026-04-07 | **Spec**: [spec.md](./spec.md)
**Input**: `.specify/specs/011-filing-generation-pipeline/spec.md` + `clarify.md`

---

## Summary

Implement the end-to-end PP-OPO tax filing generation pipeline.
`ProcessReportsCommandHandler` loads all `Init` reports, parses each IBKR CSV attachment, fetches NBS exchange rates (with IBKR cross-rate fallback), computes tax via `TaxCalculationService`, computes deadlines via `FilingDeadlineCalculator`, deduplicates, and persists `Filing` aggregate roots. The `Filing` entity is enriched with eight new fields; EF migration 0008 creates the `Filings` table. All production code has been written; the remaining deliverable is the full test suite.

---

## Technical Context

| Concern | Value |
|---|---|
| **Language/Version** | C# 12 / .NET 8 |
| **Primary Dependencies** | EF Core 8 (SQLite), xUnit, FluentAssertions, NSubstitute |
| **Storage** | SQLite via `AppDbContext`; new `Filings` table (migration 0008) |
| **Testing** | xUnit + FluentAssertions + NSubstitute; EF in-memory for infra tests |
| **Target Platform** | Windows + macOS desktop (Avalonia) |
| **Project Type** | Desktop application — Clean Architecture |
| **Performance Goals** | Pipeline completes in ≤5 s for typical account (hundreds of dividends); no UI blocking |
| **Constraints** | All monetary values `decimal`; all dates `DateOnly`; all I/O `async` |
| **Scale/Scope** | Single-user, hundreds of filings per tax year per account |

---

## Constitution Check

*Gate evaluated before Phase 0. Re-evaluated post-Phase 1.*

- [x] **I. Clean Architecture** — Handler lives in Application; `Filing` in Domain; `FilingRepository` + config in Infrastructure; DI wired in Desktop. No boundary violations.
- [x] **II. Local-First Security** — No new external endpoints. NBS HTTP calls already approved; IBKR rates sourced from embedded CSV data, not network.
- [x] **III. Financial & Temporal Correctness** — All monetary fields `decimal(18,2)`. All dates `DateOnly`. `TaxCalculationService` uses `decimal` throughout with `MidpointRounding.AwayFromZero`. `FilingDeadlineCalculator` is pure DateOnly logic.
- [x] **IV. Async & UI Responsiveness** — `ProcessReportsCommandHandler.HandleAsync` is fully async. `TaxCalculationService.CalculateAsync` is async. `CancellationToken` propagated to all I/O calls. No `.Result` or `.Wait()`.
- [x] **V. Specification-Driven Quality Gates** — Traced to approved spec under `.specify/specs/011-filing-generation-pipeline/`. Domain tests must cover `CreateFromIncome` invariants (100%). Application tests must cover handler paths (≥90%). Infrastructure tests cover `ExistsByIncomeAsync`.

**GATE RESULT: PASS** — all five principles satisfied.

---

## Project Structure

```text
.specify/specs/011-filing-generation-pipeline/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
└── contracts/
    └── ProcessReportsContract.md  ← Phase 1 output

src/Rentier.Domain/
├── Entities/Filing.cs                        ✅ enriched (feature 011)
├── Enums/IncomeType.cs                       ✅ (feature 008)

src/Rentier.Application/
├── Commands/ProcessReportsCommand.cs         ✅ (feature 011)
├── DTOs/ProcessReportsResult.cs              ✅ (feature 011)
├── Handlers/ProcessReportsCommandHandler.cs  ✅ (feature 011)
├── Repositories/IFilingRepository.cs         ✅ enriched (feature 011)
├── Parsing/DividendRecord.cs                 ✅ (feature 007)
├── Parsing/InterestRecord.cs                 ✅ (feature 007)
├── Parsing/WithholdingTaxRecord.cs           ✅ (feature 007)
├── Parsing/IbkrExchangeRate.cs               ✅ (feature 007)
└── Parsing/StatementParseResult.cs           ✅ (feature 007)

src/Rentier.Infrastructure/
├── Persistence/Configurations/FilingConfiguration.cs          ✅ (feature 011)
├── Persistence/Migrations/20260407130000_0008_FilingsTable.cs  ✅ (feature 011)
└── Repositories/FilingRepository.cs                           ✅ (feature 011)

tests/
├── Rentier.Domain.Tests/
│   └── FilingCreateFromIncomeTests.cs        ❌ NEEDS CREATION
├── Rentier.Application.Tests/
│   └── ProcessReportsCommandHandlerTests.cs  ❌ NEEDS CREATION
└── Rentier.Infrastructure.Tests/
    └── FilingRepositoryTests.cs              ❌ NEEDS CREATION
```

---

## Phase 0: Research

> See [`research.md`](./research.md) for full findings.

All design questions were resolved in `clarify.md` prior to implementation. No NEEDS CLARIFICATION items remain. Key decisions captured:

| Question | Decision |
|---|---|
| Cross-rate fallback strategy | NBS direct → NBS USD × IBKR embedded rate (FromCurrency/USD) |
| WHT matching | Match by (date, entityName, currency); whtAmount=0 if none found |
| Duplicate detection key | (taxpayerProfileId, payingEntity, incomeDate, grossIncomeRsd) |
| Per-record error isolation | Exception skips record, adds to errors; report becomes Error if any record failed |
| TaxPeriod = IncomeDate | Both fields set to dividend/interest date |
| HolidayConf construction | `IHolidayRepository.GetHolidayConfAsync()` → map `.Date` to `HolidayConf` |

---

## Phase 1: Design & Contracts

> See [`data-model.md`](./data-model.md) and [`contracts/ProcessReportsContract.md`](./contracts/ProcessReportsContract.md).

### Filing Entity (enriched)

The `Filing` aggregate root gains eight new fields and a factory method `CreateFromIncome`. Domain invariants are enforced in the factory (non-empty PayingEntity, non-negative monetary values).

### EF Migration 0008

Creates the `Filings` table with:
- FK → `TaxpayerProfiles.Id` ON DELETE CASCADE
- FK → `Reports.Id` ON DELETE SET NULL (nullable)
- All four monetary columns: `HasPrecision(18, 2)`
- Indexes on `TaxpayerProfileId` and `ReportId`

### Rate Provider Delegate

```text
BuildRateProvider(StatementParseResult parsed, CancellationToken ct):
  return async (date, currency) =>
    1. Try NBS: IExchangeRateFetcher.FetchRateAsync(date, currency) → ExchangeRate
    2. On failure: find IBKR embedded rate where FromCurrency == currency
    3. If found: fetch USD rate from NBS; return ExchangeRate(date, currency, ibkrRate × usdRateToRsd)
    4. If not found: throw InvalidOperationException → skips this income record
```

### Pipeline Flow

```text
ProcessReportsCommandHandler.HandleAsync
  │
  ├─ Load holidays → build HolidayConf (once, before report loop)
  ├─ GetByStatusAsync(Init) → reports[]
  │
  └─ foreach report:
       ├─ GetImporterById → skip+error if null
       ├─ Check TaxpayerProfileId → skip+error if null
       ├─ Check AttachmentContent → skip+error if empty
       ├─ IStatementParser.ParseAsync(stream) → Result<StatementParseResult>
       │    └─ on failure: SetStatus(Error), UpdateAsync, continue
       │
       ├─ foreach DividendRecord:
       │    ├─ Find matching WithholdingTaxRecord (date+entity+currency)
       │    ├─ TaxCalculationService.CalculateAsync(Dividend, ...)
       │    ├─ FilingDeadlineCalculator.CalculateDeadline(date, holidays)
       │    ├─ ExistsByIncomeAsync → skip if duplicate
       │    ├─ Filing.CreateFromIncome(...)
       │    └─ IFilingRepository.AddAsync
       │
       ├─ foreach InterestRecord:
       │    ├─ TaxCalculationService.CalculateAsync(Interest, whtAmount=0, ...)
       │    ├─ FilingDeadlineCalculator.CalculateDeadline(date, holidays)
       │    ├─ ExistsByIncomeAsync → skip if duplicate
       │    ├─ Filing.CreateFromIncome(...)
       │    └─ IFilingRepository.AddAsync
       │
       └─ SetStatus(Error if reportHadError else Processed), UpdateAsync
```

---

## Test Plan

### Domain Tests — `Rentier.Domain.Tests/FilingCreateFromIncomeTests.cs`

| Test | Scenario | Expected |
|---|---|---|
| `CreateFromIncome_ValidParams_ReturnsFilingWithCorrectFields` | All valid inputs | Filing has all fields set; Status=Init; TaxPeriod=IncomeDate |
| `CreateFromIncome_EmptyPayingEntity_ThrowsDomainException` | `payingEntity = ""` | DomainException |
| `CreateFromIncome_WhitespacePayingEntity_ThrowsDomainException` | `payingEntity = "  "` | DomainException |
| `CreateFromIncome_NegativeGrossIncome_ThrowsDomainException` | `grossIncomeRsd = -1` | DomainException |
| `CreateFromIncome_NegativeWhtPaid_ThrowsDomainException` | `whtPaidRsd = -1` | DomainException |
| `CreateFromIncome_NegativeGrossTax_ThrowsDomainException` | `grossTaxPayableRsd = -1` | DomainException |
| `CreateFromIncome_NegativeTaxPayable_ThrowsDomainException` | `taxPayableRsd = -1` | DomainException |
| `CreateFromIncome_NullReportId_IsAllowed` | `reportId = null` | Filing.ReportId is null |
| `CreateFromIncome_PayingEntityIsTrimmed` | `payingEntity = "  AAPL  "` | PayingEntity == "AAPL" |
| `CreateFromIncome_TaxPeriodEqualsIncomeDate` | Any valid input | TaxPeriod == IncomeDate |
| `CreateFromIncome_AlwaysNewGuid` | Two calls with same data | Different Ids |

### Application Tests — `Rentier.Application.Tests/ProcessReportsCommandHandlerTests.cs`

All dependencies mocked with NSubstitute.

| Test | Scenario | Expected |
|---|---|---|
| `HandleAsync_HappyPath_SingleDividend_CreatesFiling` | 1 Init report, 1 dividend, rate found, no duplicate | FilingsCreated=1, ReportsProcessed=1, ReportsErrored=0, Errors empty |
| `HandleAsync_HappyPath_DividendWithWht_CreatesFilingWithWht` | WHT record matches dividend | FilingInfo.WhtPaidRsd > 0 |
| `HandleAsync_HappyPath_InterestRecord_CreatesFiling` | 1 interest record, no WHT | FilingsCreated=1, IncomeType.Interest |
| `HandleAsync_ParseFailure_SetsReportError` | Parser returns failure Result | ReportsErrored=1, Errors has message, Report.SetStatus(Error) called |
| `HandleAsync_ExchangeRateNotFound_SkipsRecordAndSetsError` | NBS returns failure, no embedded rate | FilingsCreated=0, Errors has entry, ReportsErrored=1 |
| `HandleAsync_CrossRateUsed_WhenDirectRateMissing` | NBS fails, IBKR rate + USD rate available | FilingsCreated=1 (cross-rate applied) |
| `HandleAsync_DuplicateDetected_SkipsInsertion` | ExistsByIncomeAsync returns true | FilingsCreated=0, no AddAsync called |
| `HandleAsync_NullImporter_SetsReportError` | GetByIdAsync returns null | ReportsErrored=1, Errors has "importer not found" |
| `HandleAsync_NullTaxpayerProfileId_SetsReportError` | Importer.TaxpayerProfileId is null | ReportsErrored=1 |
| `HandleAsync_EmptyAttachment_SetsReportError` | AttachmentContent is null/empty | ReportsErrored=1 |
| `HandleAsync_CancellationRequested_ThrowsOperationCancelled` | CT cancelled mid-loop | OperationCanceledException propagated |
| `HandleAsync_NoInitReports_ReturnsZeroCountResult` | GetByStatusAsync returns empty | All counts 0, Errors empty |
| `HandleAsync_MultipleReports_MixedOutcomes_CountsCorrectly` | 2 reports: 1 success, 1 parse failure | FilingsCreated=1, ReportsProcessed=1, ReportsErrored=1 |
| `HandleAsync_InterestRecord_WhtMatchedFromWithholdings` | Interest + matching Withholding by date+entity | whtAmount passed to TaxCalculationService is non-zero |

### Infrastructure Tests — `Rentier.Infrastructure.Tests/FilingRepositoryTests.cs`

Using EF Core SQLite in-memory provider.

| Test | Scenario | Expected |
|---|---|---|
| `ExistsByIncomeAsync_ExactMatch_ReturnsTrue` | Filing with matching 4-key combination | true |
| `ExistsByIncomeAsync_NoMatch_ReturnsFalse` | Different payingEntity | false |
| `ExistsByIncomeAsync_DifferentTaxpayerProfile_ReturnsFalse` | Different taxpayerProfileId | false |
| `ExistsByIncomeAsync_DifferentIncomeDate_ReturnsFalse` | Off by one day | false |
| `ExistsByIncomeAsync_DifferentGrossIncomeRsd_ReturnsFalse` | 1 penny difference | false |
| `AddAsync_ValidFiling_PersistsToDatabase` | AddAsync then GetByIdAsync | Entity found, all fields preserved |
| `GetByReportIdAsync_MultipleFilings_ReturnsOnlyMatching` | 3 filings, 2 for same report | List of 2 |
| `GetAllAsync_EmptyDatabase_ReturnsEmptyList` | No filings | Empty list |
| `UpdateAsync_StatusChange_PersistsUpdate` | AddAsync, AdvanceStatus, UpdateAsync, reload | New status persisted |
| `DeleteAsync_ExistingFiling_RemovesFromDatabase` | AddAsync then DeleteAsync | Not found after delete |

---

## DI Registration (complete, no changes required)

```csharp
// InfrastructureServiceExtensions.cs — already registered:
services.AddTransient<IFilingRepository, FilingRepository>();
services.AddTransient<ICommandHandler<ProcessReportsCommand,
    Result<ProcessReportsResult, Error>>, ProcessReportsCommandHandler>();
```

---

## Constitution Check (post-design)

All five principles verified unchanged from pre-design check. No new external dependencies, no boundary violations, no float/double usage, no blocking I/O patterns introduced.

**GATE RESULT: PASS**

---

## Implementation Status

| Artifact | Status |
|---|---|
| `Filing` entity enrichment | ✅ Done |
| `Filing.CreateFromIncome` factory | ✅ Done |
| `IFilingRepository` (ExistsByIncomeAsync, GetByReportIdAsync) | ✅ Done |
| `FilingRepository` implementation | ✅ Done |
| `FilingConfiguration` EF mapping | ✅ Done |
| Migration 0008 (Filings table) | ✅ Done |
| `ProcessReportsCommand` | ✅ Done |
| `ProcessReportsResult` | ✅ Done |
| `ProcessReportsCommandHandler` | ✅ Done |
| DI registration | ✅ Done |
| `FilingCreateFromIncomeTests` (domain) | ❌ Pending |
| `ProcessReportsCommandHandlerTests` (application) | ❌ Pending |
| `FilingRepositoryTests` (infrastructure) | ❌ Pending |
