---
description: "Task list for 011 Filing Generation Pipeline"
---

# Tasks: 011 Filing Generation Pipeline

**Input**: `.specify/specs/011-filing-generation-pipeline/` (spec.md, plan.md, data-model.md, contracts/, research.md, quickstart.md)
**Prerequisite**: Feature 010 (Report enrichment + ReportRepository) merged first. Feature 011 assumes:
- `Report` entity has: `Status` (ReportStatus), `AttachmentContent` (byte[]?), `SetStatus(ReportStatus)` method, factory `Report.Create(importerId, name, bytes, messageId)`
- `IReportRepository` has: `GetByStatusAsync(ReportStatus, CT)`, `UpdateAsync(Report, CT)`
- `AppDbContext.Reports` DbSet exists
- Migration 0007 (Reports table) exists

**Tests**: All three test files are required. Tests are written before or alongside implementation — ensure they compile (failing is fine until the implementation task completes).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with other [P] tasks in the same phase (different files, no conflicts)
- **[US1]**: Belongs to the single pipeline user story
- Include exact file paths in every description

---

## Phase 1: Setup

**Purpose**: Confirm prerequisites; no new files in this phase.

> ⚠️ **Gate**: Feature 010 must be merged and `dotnet build` must pass before any task below is started.
> Run `dotnet build Rentier.slnx` from repo root and confirm 0 errors.

No coding tasks. Proceed to Phase 2 once the build is green.

---

## Phase 2: Foundational — Domain Enrichment

**Purpose**: Enrich the `Filing` aggregate root with the eight new income fields and the `CreateFromIncome` factory. All subsequent phases depend on this.

**⚠️ CRITICAL**: Tasks T001 and T002 must be complete before any Phase 3/4/5 work begins — every downstream type depends on the enriched `Filing`.

- [ ] T001 Add eight new auto-property fields to `Filing` entity in `src/Rentier.Domain/Entities/Filing.cs`: `IncomeDate` (DateOnly), `IncomeType` (IncomeType enum from `Rentier.Domain.Enums`), `PayingEntity` (string), `GrossIncomeRsd` (decimal), `WhtPaidRsd` (decimal), `GrossTaxPayableRsd` (decimal), `TaxPayableRsd` (decimal), `FilingDeadline` (DateOnly), `ReportId` (Guid? — nullable). All properties have private setters. Keep the existing public constructor, `Id`, `TaxpayerProfileId`, `TaxPeriod`, `Status`, and `AdvanceStatus` method unchanged.

- [ ] T002 Add `Filing.CreateFromIncome` static factory method to `src/Rentier.Domain/Entities/Filing.cs`. Signature: `public static Filing CreateFromIncome(Guid taxpayerProfileId, IncomeType incomeType, string payingEntity, DateOnly incomeDate, decimal grossIncomeRsd, decimal whtPaidRsd, decimal grossTaxPayableRsd, decimal taxPayableRsd, DateOnly filingDeadline, Guid? reportId = null)`. Implementation: (1) Trim `payingEntity`; throw `DomainException("PayingEntity must not be empty")` if result is null/empty/whitespace. (2) Throw `DomainException("GrossIncomeRsd must not be negative")` if `grossIncomeRsd < 0`. (3) Same guard for `whtPaidRsd`, `grossTaxPayableRsd`, `taxPayableRsd`. (4) Return `new Filing { Id = Guid.NewGuid(), TaxpayerProfileId = taxpayerProfileId, TaxPeriod = incomeDate, Status = FilingStatus.Init, IncomeType = incomeType, PayingEntity = trimmedEntity, IncomeDate = incomeDate, GrossIncomeRsd = grossIncomeRsd, WhtPaidRsd = whtPaidRsd, GrossTaxPayableRsd = grossTaxPayableRsd, TaxPayableRsd = taxPayableRsd, FilingDeadline = filingDeadline, ReportId = reportId }`. Note: `TaxPeriod` is set equal to `IncomeDate` (backward compat). The factory requires a private parameterless constructor for EF navigation and a private setter on Id — add them.

**Checkpoint**: `dotnet build` passes. `Filing` now has all 9 new members.

---

## Phase 3: User Story 1 — Application Layer

**Story**: As the system, I process all `Init` reports, parse IBKR CSV attachments, calculate tax and deadlines, and persist one `Filing` per income event.

**Goal**: Command record, result DTO, updated repository contract, and orchestrating handler are all in place.

**Independent Test**: With all dependencies mocked, `ProcessReportsCommandHandler.HandleAsync` returns `FilingsCreated=1` for a single Init report containing one dividend record.

### Implementation for User Story 1 — Application

- [ ] T003 [P] [US1] Create `ProcessReportsResult` DTO in `src/Rentier.Application/DTOs/ProcessReportsResult.cs`: `public sealed record ProcessReportsResult(int FilingsCreated, int ReportsProcessed, int ReportsErrored, IReadOnlyList<string> Errors);` Namespace: `Rentier.Application.DTOs`.

- [ ] T004 [P] [US1] Create `ProcessReportsCommand` in `src/Rentier.Application/Commands/ProcessReportsCommand.cs`: `public sealed record ProcessReportsCommand;` Namespace: `Rentier.Application.Commands`. No input parameters — scans all `Init` reports.

- [ ] T005 [P] [US1] Add two new methods to `IFilingRepository` in `src/Rentier.Application/Repositories/IFilingRepository.cs` (keep all existing methods): `Task<bool> ExistsByIncomeAsync(Guid taxpayerProfileId, string payingEntity, DateOnly incomeDate, decimal grossIncomeRsd, CancellationToken ct = default);` and `Task<IReadOnlyList<Filing>> GetByReportIdAsync(Guid reportId, CancellationToken ct = default);`

- [ ] T006 [US1] Implement `ProcessReportsCommandHandler` in `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs`. Class: `public sealed class ProcessReportsCommandHandler : ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>`. Constructor injects: `IReportRepository`, `IImporterRepository`, `IFilingRepository`, `IExchangeRateFetcher`, `IHolidayRepository`, `IStatementParser`. All `CancellationToken` propagated to every async call; never catch `OperationCanceledException`.

  **HandleAsync pipeline**:
  1. Load holidays once: `var holidayDto = await _holidayRepository.GetHolidayConfAsync(ct)` → `var holidays = new HolidayConf(holidayDto.Holidays.Select(h => h.Date).ToList())`.
  2. `var reports = await _reportRepository.GetByStatusAsync(ReportStatus.Init, ct)`.
  3. Init counters: `int filingsCreated = 0, reportsProcessed = 0, reportsErrored = 0; var errors = new List<string>()`.
  4. For each report:
     - Get importer: `var importer = await _importerRepository.GetByIdAsync(report.ImporterId, ct)`. If null → `errors.Add($"Report {report.Id}: importer not found"); report.SetStatus(ReportStatus.Error); await _reportRepository.UpdateAsync(report, ct); reportsErrored++; continue`.
     - Check `importer.TaxpayerProfileId`: if null → same error pattern with message `"Report {report.Id}: importer has no TaxpayerProfileId"`.
     - Check `report.AttachmentContent`: if null or empty → same error pattern with message `"Report {report.Id}: no attachment content"`.
     - Parse: `var parseResult = await _statementParser.ParseAsync(new MemoryStream(report.AttachmentContent), ct)`. If `parseResult.IsFailure` → `errors.Add($"Report {report.Id}: parse failed — {parseResult.Error.Message}"); report.SetStatus(ReportStatus.Error); await _reportRepository.UpdateAsync(report, ct); reportsErrored++; continue`.
     - Build rate provider delegate: `Func<DateOnly, string, Task<ExchangeRate>> rateProvider = BuildRateProvider(parsed, ct)` (private helper). The delegate: (a) try `FetchRateAsync(date, currency)` — if success return value; (b) find `parsed.EmbeddedRates.FirstOrDefault(r => r.FromCurrency.Equals(currency, StringComparison.OrdinalIgnoreCase))`; (c) if found, fetch USD rate `FetchRateAsync(date, "USD")` and return `new ExchangeRate(date, currency, ibkr.Rate * usdRate.RateToRsd)`; (d) else `throw new InvalidOperationException($"Exchange rate not found for {currency} on {date}")`.
     - `bool reportHadError = false`.
     - **Dividend loop**: foreach `DividendRecord d in parsed.Dividends`: wrap in try/catch(Exception ex when ex is not OperationCanceledException). Inside: find WHT `var wht = parsed.Withholdings.FirstOrDefault(w => w.Date == d.Date && w.EntityName == d.EntityName && w.Currency == d.Currency)`; call `TaxCalculationService.CalculateAsync(IncomeType.Dividend, d.EntityName, d.Date, d.Amount, d.Currency, wht?.Amount ?? 0m, d.Currency, rateProvider, ct)`; `var deadline = FilingDeadlineCalculator.CalculateDeadline(d.Date, holidays)`; check duplicate `if (await _filingRepository.ExistsByIncomeAsync(importer.TaxpayerProfileId.Value, info.PayingEntity, info.IncomeDate, info.GrossIncomeRsd, ct)) continue`; create `var filing = Filing.CreateFromIncome(importer.TaxpayerProfileId.Value, IncomeType.Dividend, info.PayingEntity, info.IncomeDate, info.GrossIncomeRsd, info.WhtPaidRsd, info.GrossTaxPayableRsd, info.TaxPayableRsd, deadline, report.Id)`; `await _filingRepository.AddAsync(filing, ct)`; `filingsCreated++`. On catch: `errors.Add($"Report {report.Id}, dividend {d.EntityName} {d.Date}: {ex.Message}"); reportHadError = true`.
     - **Interest loop**: foreach `InterestRecord i in parsed.Interest` **where `i.Type == InterestType.Credit`** (skip `InterestType.Debit` — debit interest records are expense charges such as margin interest, not taxable income): same try/catch pattern. Find WHT by `(Date, EntityName)` 2-field match (no currency field): `var wht = parsed.Withholdings.FirstOrDefault(w => w.Date == i.Date && w.EntityName == i.EntityName)`. Call `TaxCalculationService.CalculateAsync(IncomeType.Interest, i.EntityName, i.Date, i.Amount, i.Currency, wht?.Amount ?? 0m, i.Currency, rateProvider, ct)`. Rest same as dividend loop with `IncomeType.Interest`.
     - After both loops: if `reportHadError` → `report.SetStatus(ReportStatus.Error); await _reportRepository.UpdateAsync(report, ct); reportsErrored++` else `report.SetStatus(ReportStatus.Processed); await _reportRepository.UpdateAsync(report, ct); reportsProcessed++`.
  5. Return `Result<ProcessReportsResult, Error>.Success(new ProcessReportsResult(filingsCreated, reportsProcessed, reportsErrored, errors))`.

**Checkpoint**: `dotnet build` passes. Application layer is complete.

---

## Phase 4: User Story 1 — Infrastructure Layer

**Goal**: EF configuration, FilingRepository implementation, AppDbContext update, migration, and DI registration.

**Independent Test**: `FilingRepository.ExistsByIncomeAsync` returns true for an exact 4-key match and false for any field mismatch.

### Implementation for User Story 1 — Infrastructure

- [ ] T007 [P] [US1] Create `FilingConfiguration` EF type configuration in `src/Rentier.Infrastructure/Persistence/Configurations/FilingConfiguration.cs`. Class: `public sealed class FilingConfiguration : IEntityTypeConfiguration<Filing>`. In `Configure`: `builder.HasKey(f => f.Id)`; `builder.Property(f => f.Id).ValueGeneratedNever()`; `builder.Property(f => f.PayingEntity).IsRequired().HasMaxLength(500)`; all four monetary properties (`GrossIncomeRsd`, `WhtPaidRsd`, `GrossTaxPayableRsd`, `TaxPayableRsd`): `.HasPrecision(18, 2)`; `builder.HasOne<TaxpayerProfile>().WithMany().HasForeignKey(f => f.TaxpayerProfileId).OnDelete(DeleteBehavior.Cascade)`; `builder.HasOne<Report>().WithMany().HasForeignKey(f => f.ReportId).IsRequired(false).OnDelete(DeleteBehavior.SetNull)`; `builder.HasIndex(f => f.TaxpayerProfileId)`; `builder.HasIndex(f => f.ReportId)`. No explicit table name needed — EF pluralizes to `Filings` by convention.

- [ ] T008 [P] [US1] Add `DbSet<Filing>` to `AppDbContext` in `src/Rentier.Infrastructure/Persistence/AppDbContext.cs`: `public DbSet<Filing> Filings => Set<Filing>();` Add `using Rentier.Domain.Entities;` if not already present. No other changes needed — `ApplyConfigurationsFromAssembly` picks up `FilingConfiguration` automatically.

- [ ] T009 [US1] Implement `FilingRepository` in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs`. Constructor: `public FilingRepository(AppDbContext context)`. Implement all 8 `IFilingRepository` members:
  - `GetByIdAsync`: `return await _context.Filings.FindAsync([id], ct)`
  - `GetAllAsync`: `return await _context.Filings.ToListAsync(ct)`
  - `GetByTaxPeriodAsync`: `return await _context.Filings.FirstOrDefaultAsync(f => f.TaxpayerProfileId == taxpayerProfileId && f.TaxPeriod == taxPeriod, ct)`
  - `AddAsync`: `await _context.Filings.AddAsync(filing, ct); await _context.SaveChangesAsync(ct)`
  - `UpdateAsync`: `_context.Filings.Update(filing); await _context.SaveChangesAsync(ct)`
  - `DeleteAsync`: `var entity = await _context.Filings.FindAsync([id], ct); if (entity is not null) { _context.Filings.Remove(entity); await _context.SaveChangesAsync(ct); }` — use `FindAsync+Remove`, NOT `ExecuteDeleteAsync`
  - `ExistsByIncomeAsync`: `return await _context.Filings.AnyAsync(f => f.TaxpayerProfileId == taxpayerProfileId && f.PayingEntity == payingEntity && f.IncomeDate == incomeDate && f.GrossIncomeRsd == grossIncomeRsd, ct)`
  - `GetByReportIdAsync`: `return await _context.Filings.Where(f => f.ReportId == reportId).ToListAsync(ct)`

- [ ] T010 [US1] Generate EF migration `0008_FilingsTable` for `src/Rentier.Infrastructure`. Run from repo root: `dotnet ef migrations add 0008_FilingsTable --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop`. Verify generated migration in `src/Rentier.Infrastructure/Persistence/Migrations/` creates table `Filings` with all columns, indexes `IX_Filings_TaxpayerProfileId` + `IX_Filings_ReportId`, FK to `TaxpayerProfiles` (CASCADE) and FK to `Reports` (SET NULL). Verify migration compiles: `dotnet build Rentier.slnx`.

- [ ] T011 [US1] Register two new services in `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` inside `AddInfrastructureServices`: `services.AddTransient<IFilingRepository, FilingRepository>();` and `services.AddTransient<ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>, ProcessReportsCommandHandler>();`. Add required usings for `ProcessReportsCommand`, `ProcessReportsResult`, `FilingRepository`, `IFilingRepository`, `ProcessReportsCommandHandler`. Use `AddTransient` only (no `AddScoped`).

**Checkpoint**: `dotnet build` passes. `dotnet ef database update` applies migration without errors.

---

## Phase 5: User Story 1 — Tests

**Goal**: Full test coverage for domain factory invariants, handler orchestration paths, and repository duplicate-detection logic.

**Independent Test**: All three test files compile and run green after their respective implementation tasks are complete.

### Tests for User Story 1

- [ ] T012 [P] [US1] Create `tests/Rentier.Domain.Tests/FilingCreateFromIncomeTests.cs` with 11 xUnit facts using FluentAssertions. Arrange helper: `static Filing ValidFiling(string? entity = "AAPL", Guid? profileId = null, Guid? reportId = null) => Filing.CreateFromIncome(profileId ?? Guid.NewGuid(), IncomeType.Dividend, entity ?? "AAPL", new DateOnly(2024, 3, 15), 10000m, 1500m, 1500m, 0m, new DateOnly(2024, 4, 14), reportId)`. Test cases:
  1. `CreateFromIncome_ValidParams_ReturnsFilingWithCorrectFields` — call with all valid inputs; assert `Id != Guid.Empty`, `TaxpayerProfileId`, `IncomeType == Dividend`, `PayingEntity == "AAPL"`, `IncomeDate == new DateOnly(2024,3,15)`, `GrossIncomeRsd == 10000m`, `WhtPaidRsd == 1500m`, `GrossTaxPayableRsd == 1500m`, `TaxPayableRsd == 0m`, `FilingDeadline`, `ReportId == null`, `Status == FilingStatus.Init`.
  2. `CreateFromIncome_TaxPeriodEqualsIncomeDate` — assert `TaxPeriod == IncomeDate`.
  3. `CreateFromIncome_EmptyPayingEntity_ThrowsDomainException` — `payingEntity = ""` → `Action act = () => Filing.CreateFromIncome(...); act.Should().Throw<DomainException>()`.
  4. `CreateFromIncome_WhitespacePayingEntity_ThrowsDomainException` — `payingEntity = "  "` → throws `DomainException`.
  5. `CreateFromIncome_PayingEntityIsTrimmed` — `payingEntity = "  AAPL  "` → `filing.PayingEntity.Should().Be("AAPL")`.
  6. `CreateFromIncome_NegativeGrossIncome_ThrowsDomainException` — `grossIncomeRsd = -0.01m` → throws `DomainException`.
  7. `CreateFromIncome_NegativeWhtPaid_ThrowsDomainException` — `whtPaidRsd = -1m` → throws `DomainException`.
  8. `CreateFromIncome_NegativeGrossTax_ThrowsDomainException` — `grossTaxPayableRsd = -1m` → throws `DomainException`.
  9. `CreateFromIncome_NegativeTaxPayable_ThrowsDomainException` — `taxPayableRsd = -1m` → throws `DomainException`.
  10. `CreateFromIncome_NullReportId_IsAllowed` — `reportId = null` → `filing.ReportId.Should().BeNull()`.
  11. `CreateFromIncome_AlwaysNewGuid` — call twice with same params → `filing1.Id.Should().NotBe(filing2.Id)`.

- [ ] T013 [P] [US1] Create `tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs` with 10 xUnit facts using FluentAssertions. Use `IAsyncLifetime` pattern (same as `ImporterRepositoryTests`): open `SqliteConnection("Data Source=:memory:")`, build `DbContextOptions<AppDbContext>` with `.UseSqlite(_connection)`, call `EnsureCreatedAsync()`, instantiate `FilingRepository`. Helper: `static Filing MakeFiling(Guid? profileId = null, Guid? reportId = null, string entity = "AAPL", DateOnly? date = null, decimal gross = 10000m) => Filing.CreateFromIncome(profileId ?? Guid.NewGuid(), IncomeType.Dividend, entity, date ?? new DateOnly(2024,3,15), gross, 0m, 1500m, 1500m, new DateOnly(2024,4,14), reportId)`. Note: `EnsureCreatedAsync` creates tables from EF config — no need for a seeded `TaxpayerProfile` because FK enforcement is off by default in SQLite. Test cases:
  1. `ExistsByIncomeAsync_ExactMatch_ReturnsTrue` — AddAsync a filing, then ExistsByIncomeAsync with same 4-field key → `true`.
  2. `ExistsByIncomeAsync_NoMatch_DifferentEntity_ReturnsFalse` — AddAsync, query with different `payingEntity` → `false`.
  3. `ExistsByIncomeAsync_DifferentTaxpayerProfile_ReturnsFalse` — AddAsync, query with different `taxpayerProfileId` → `false`.
  4. `ExistsByIncomeAsync_DifferentIncomeDate_ReturnsFalse` — AddAsync, query with date off by one day → `false`.
  5. `ExistsByIncomeAsync_DifferentGrossIncomeRsd_ReturnsFalse` — AddAsync, query with gross differing by 0.01m → `false`.
  6. `AddAsync_ValidFiling_PersistsToDatabase` — AddAsync, then GetByIdAsync → entity not null, all fields preserved (`IncomeDate`, `GrossIncomeRsd`, `Status`, `PayingEntity`, etc.).
  7. `GetByReportIdAsync_MultipleFilings_ReturnsOnlyMatching` — seed 3 filings (2 with same `reportId`, 1 with different), GetByReportIdAsync → list of 2.
  8. `GetAllAsync_EmptyDatabase_ReturnsEmptyList` — no seed → empty list.
  9. `UpdateAsync_StatusChange_PersistsUpdate` — AddAsync, call `filing.AdvanceStatus(FilingStatus.Filed)`, UpdateAsync, reload via new context → `Status == FilingStatus.Filed`.
  10. `DeleteAsync_ExistingFiling_RemovesFromDatabase` — AddAsync, DeleteAsync, GetByIdAsync → `null`.

- [ ] T014 [US1] Create `tests/Rentier.Application.Tests/ProcessReportsCommandHandlerTests.cs` with 14 xUnit facts using FluentAssertions + NSubstitute. Mock all 6 dependencies: `IReportRepository`, `IImporterRepository`, `IFilingRepository`, `IExchangeRateFetcher`, `IHolidayRepository`, `IStatementParser`. Standard setup helper: `SetupHappyPathMocks(Guid profileId, Report report, Importer importer, StatementParseResult parsed)` that configures: `_reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CT>()).Returns([report])`, `_importerRepo.GetByIdAsync(importer.Id, CT).Returns(importer)`, `_holidayRepo.GetHolidayConfAsync(CT).Returns(new HolidayConfDto([]))`, `_parser.ParseAsync(Arg.Any<Stream>(), CT).Returns(Result<StatementParseResult, Error>.Success(parsed))`, `_rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), CT).Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(DateOnly.MinValue, "USD", 117.5m)))`, `_filingRepo.ExistsByIncomeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<decimal>(), CT).Returns(false)`. Create SUT: `new ProcessReportsCommandHandler(_reportRepo, _importerRepo, _filingRepo, _rateFetcher, _holidayRepo, _parser)`. Test cases:
  1. `HandleAsync_HappyPath_SingleDividend_CreatesFiling` — 1 Init report, 1 dividend record, rate found, no duplicate → `result.IsSuccess`, `FilingsCreated == 1`, `ReportsProcessed == 1`, `ReportsErrored == 0`, `Errors` empty, `_filingRepo.Received(1).AddAsync(Arg.Any<Filing>(), CT)`.
  2. `HandleAsync_HappyPath_DividendWithWht_CreatesFilingWithWhtPaid` — WHT record matching dividend's (date, entity, currency) → `AddAsync` called with `Filing` where `WhtPaidRsd > 0`. Verify by capturing arg: `_filingRepo.AddAsync(Arg.Do<Filing>(f => captured = f), CT)`.
  3. `HandleAsync_HappyPath_InterestRecord_CreatesFiling` — 1 interest record, no WHT → `FilingsCreated == 1`, captured filing has `IncomeType == IncomeType.Interest`.
  4. `HandleAsync_ParseFailure_SetsReportStatusError` — parser returns `Result.Failure(new Error(...))` → `ReportsErrored == 1`, `Errors` has 1 entry, `_reportRepo.Received(1).UpdateAsync(Arg.Is<Report>(r => r.Status == ReportStatus.Error), CT)`, `_filingRepo.DidNotReceive().AddAsync(...)`.
  5. `HandleAsync_ExchangeRateNotFound_NoEmbeddedRate_SkipsRecord` — rate fetcher returns Failure, parsed has no embedded rates → `FilingsCreated == 0`, `Errors` has 1 entry, `ReportsErrored == 1` (reportHadError → Error).
  6. `HandleAsync_CrossRateUsed_WhenDirectRateMissing` — rate fetcher fails for "GBP" but returns success for "USD"; `StatementParseResult.EmbeddedRates` contains `IbkrExchangeRate(date, "GBP", "USD", 1.27m)` → `FilingsCreated == 1`.
  7. `HandleAsync_DuplicateDetected_SkipsInsertion` — `ExistsByIncomeAsync` returns true → `FilingsCreated == 0`, `_filingRepo.DidNotReceive().AddAsync(...)`, no error added, `ReportsProcessed == 1` (not an error condition).
  8. `HandleAsync_NullImporter_SetsReportError` — `_importerRepo.GetByIdAsync` returns `null` → `ReportsErrored == 1`, `Errors` has 1 entry containing "importer", `_parser.DidNotReceive().ParseAsync(...)`.
  9. `HandleAsync_NullTaxpayerProfileId_SetsReportError` — importer has `TaxpayerProfileId == null` → `ReportsErrored == 1`, error message contains "TaxpayerProfileId".
  10. `HandleAsync_EmptyAttachment_SetsReportError` — `report.AttachmentContent == null` or `Array.Empty<byte>()` → `ReportsErrored == 1`, error message contains "attachment".
  11. `HandleAsync_CancellationRequested_ThrowsOperationCancelled` — pass a pre-cancelled `CancellationToken` → `Func<Task> act = () => sut.HandleAsync(..., cancelledCt); await act.Should().ThrowAsync<OperationCanceledException>()`.
  12. `HandleAsync_NoInitReports_ReturnsZeroCountResult` — `GetByStatusAsync` returns empty list → `result.IsSuccess`, all counts 0, `Errors` empty.
  13. `HandleAsync_MultipleReports_MixedOutcomes_CountsCorrectly` — 2 reports: first parses OK with 1 dividend, second fails parse → `FilingsCreated == 1`, `ReportsProcessed == 1`, `ReportsErrored == 1`.
  14. `HandleAsync_InterestRecord_WhtMatchedFromWithholdings_PassesNonZeroWht` — interest record + matching `WithholdingTaxRecord` (same date + entityName, any currency) → capture arg to `TaxCalculationService`... Since `TaxCalculationService` is static, verify indirectly: captured `Filing.WhtPaidRsd > 0` after `AddAsync`.

**Checkpoint**: `dotnet test` passes. All 35 new test cases green.

---

## Final Phase: Polish & Cross-Cutting Concerns

- [ ] T015 Run `dotnet test Rentier.slnx` from repo root; confirm all tests pass including existing `FilingStatusTransitionTests`, `FilingDeadlineCalculatorTests`, and `DiRegistrationSmokeTests`. If `DiRegistrationSmokeTests` scans for handler registrations, verify `ProcessReportsCommandHandler` appears. Run quickstart.md validation: seed one Init report with real IBKR CSV bytes and execute handler end-to-end in a test harness to confirm `FilingsCreated > 0`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 2 (Domain)**: Depends only on Feature 010 being merged — start immediately after build is green
- **Phase 3 (Application)**: All tasks depend on T001 + T002 completing
- **Phase 4 (Infrastructure)**: T007/T008 depend on T001; T009 depends on T007+T008; T010 depends on T009; T011 depends on T006+T009
- **Phase 5 (Tests)**: T012 depends on T001+T002; T013 depends on T009+T008; T014 depends on T006
- **Polish (T015)**: Depends on all previous phases

### Within-Phase Parallel Groups

**Phase 3** — after T001+T002 complete:
```
T003 [P] ProcessReportsResult DTO
T004 [P] ProcessReportsCommand
T005 [P] IFilingRepository additions
```
→ all three can run simultaneously (different files, no conflicts)
→ T006 (handler) starts after T003 + T004 + T005 all complete

**Phase 4** — after T001+T002 complete:
```
T007 [P] FilingConfiguration   (independent of Phase 3)
T008 [P] AppDbContext.Filings  (independent of Phase 3)
```
→ can start in parallel with Phase 3 work
→ T009 starts after T007 + T008
→ T010 starts after T009
→ T011 starts after T006 + T009

**Phase 5** — after respective Phase 3/4 tasks:
```
T012 [P] Domain tests          (needs T001+T002 only)
T013 [P] Infrastructure tests  (needs T009+T008)
```
→ T012 and T013 can run in parallel
→ T014 starts after T006 (handler implementation)

### Parallel Execution Example

```bash
# After T001 + T002 (Domain) are done:

# Stream A: Application
Task A1: T003 ProcessReportsResult DTO
Task A2: T004 ProcessReportsCommand
Task A3: T005 IFilingRepository additions
# → then T006 ProcessReportsCommandHandler

# Stream B: Infrastructure (can start alongside Stream A)
Task B1: T007 FilingConfiguration
Task B2: T008 AppDbContext.Filings
# → then T009 FilingRepository → T010 Migration → T011 DI

# Stream C: Tests (start as soon as prerequisites in A/B complete)
Task C1: T012 FilingCreateFromIncomeTests  (can start immediately after T001+T002)
Task C2: T013 FilingRepositoryTests        (start after T009+T008)
# → then T014 ProcessReportsCommandHandlerTests (after T006)
```

---

## Implementation Strategy

### MVP First (all in one story)

This feature has a single user story — the pipeline itself. The MVP is all of Phase 2–4 (production code). Tests in Phase 5 gate the merge.

1. Complete Phase 2 (Domain) → build green
2. Complete Phase 3 (Application) → build green
3. Complete Phase 4 (Infrastructure) → migration verified
4. Complete Phase 5 (Tests) → all 35 tests green
5. Run T015 (Polish) → `dotnet test` clean

### Task Count Summary

| Phase | Tasks | Parallel |
|---|---|---|
| Phase 2: Domain | T001–T002 | 0 |
| Phase 3: Application | T003–T006 | T003, T004, T005 |
| Phase 4: Infrastructure | T007–T011 | T007, T008 |
| Phase 5: Tests | T012–T014 | T012, T013 |
| Polish | T015 | — |
| **Total** | **15** | **7 [P] tasks** |

### Test Case Count

| File | Cases |
|---|---|
| `FilingCreateFromIncomeTests.cs` | 11 |
| `FilingRepositoryTests.cs` | 10 |
| `ProcessReportsCommandHandlerTests.cs` | 14 |
| **Total new** | **35** |

---

## Notes

- `AddTransient` only — never `AddScoped` (architecture rule)
- All dates `DateOnly`, all money `decimal` — no `DateTime`, no `float`/`double`
- `CancellationToken` propagated to every `async` call; never `.Result`/`.Wait()`
- EF deletes: `FindAsync + Remove + SaveChangesAsync` only — never `ExecuteDeleteAsync`
- `TaxCalculationService.CalculateAsync`: `whtCurrency` must equal `incomeCurrency` when `whtAmount > 0`. For dividends, always pass `d.Currency` as `whtCurrency`. For interest, always pass `i.Currency` as `whtCurrency` (the currencies match whether or not whtAmount is zero, satisfying the constraint in all cases).
- `FilingDeadlineCalculator.CalculateDeadline` is synchronous and pure — no DI needed
- `TaxCalculationService.CalculateAsync` is static — no DI needed
- `OperationCanceledException` is never caught in the handler (let it propagate)
- Duplicate skip (ExistsByIncomeAsync=true) is silent — NOT counted as an error, report still becomes Processed
- `Report.SetStatus` is called once per report at the very end, after both dividend and interest loops finish
