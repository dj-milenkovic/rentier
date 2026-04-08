# Spec — 011 Filing Generation Pipeline

## Goal
Orchestrate the full tax calculation pipeline: load `Init` reports → parse CSV
attachments → fetch exchange rates → calculate tax → calculate deadlines → persist
`Filing` records. Each `Filing` represents one taxable income event (dividend or
interest) ready for PP-OPO submission.

## Domain Changes

### `Filing` entity enrichment
Add to existing entity (keeping `TaxPeriod` + `Status` + `AdvanceStatus`):
- `IncomeDate` (DateOnly) — the actual date income was received
- `IncomeType` (IncomeType enum) — Dividend | Interest
- `PayingEntity` (string, max 500) — company/payer name
- `GrossIncomeRsd` (decimal, precision 18,2)
- `WhtPaidRsd` (decimal, precision 18,2)
- `GrossTaxPayableRsd` (decimal, precision 18,2)
- `TaxPayableRsd` (decimal, precision 18,2)
- `FilingDeadline` (DateOnly)
- `ReportId` (Guid?) — nullable FK to Reports

Factory method: `Filing.CreateFromIncome(taxpayerProfileId, incomeType, payingEntity, incomeDate, grossIncomeRsd, whtPaidRsd, grossTaxPayableRsd, taxPayableRsd, filingDeadline, reportId)`

### EF Migration 0008
Creates `Filings` table (entity didn't have a DB table before):
- All monetary fields: `HasPrecision(18, 2)`
- FK: `ReportId → Reports.Id ON DELETE SET NULL`
- FK: `TaxpayerProfileId → TaxpayerProfiles.Id ON DELETE CASCADE`

## Application Layer

### `ProcessReportsCommand`
```csharp
public sealed record ProcessReportsCommand;
```

### `ProcessReportsResult`
```csharp
public sealed record ProcessReportsResult(
    int FilingsCreated, int ReportsProcessed, int ReportsErrored,
    IReadOnlyList<string> Errors);
```

### `ProcessReportsCommandHandler`
Implements `ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>`.

For each Init report:
1. Load importer → get `TaxpayerProfileId` (skip if null)
2. Parse `report.AttachmentContent` via `IStatementParser.ParseAsync`
3. Build `HolidayConf` from `IHolidayRepository.GetAllAsync()`
4. For each `DividendRecord`:
   - Build `rateProvider` delegate wrapping `IExchangeRateFetcher`
   - Call `TaxCalculationService.CalculateAsync(...)`
   - Call `FilingDeadlineCalculator.CalculateDeadline(incomeDate, holidays)`
   - Check `ExistsByIncomeAsync` → skip if duplicate
   - Create Filing via `Filing.CreateFromIncome(...)` → `IFilingRepository.AddAsync`
5. For each `InterestRecord`: same flow (IncomeType.Interest, WHT lookup from Withholdings by date+entity)
6. On parse failure → `report.SetStatus(Error)` → `IReportRepository.UpdateAsync`
7. On success → `report.SetStatus(Processed)` → `IReportRepository.UpdateAsync`

### `IReportRepository` additions (already in 010)
Used here: `GetByStatusAsync(Init)`, `UpdateAsync`

### `IFilingRepository` additions
- `Task<bool> ExistsByIncomeAsync(Guid taxpayerProfileId, string payingEntity, DateOnly incomeDate, decimal grossIncomeRsd, CT)`
- `Task<IReadOnlyList<Filing>> GetByReportIdAsync(Guid reportId, CT)`

## Infrastructure Layer

### `FilingRepository`
Full CRUD + `ExistsByIncomeAsync` + `GetByReportIdAsync`.

### EF Config `FilingConfiguration`
- `HasKey(f => f.Id)`
- All monetary properties: `.HasPrecision(18, 2)`
- `HasOne<TaxpayerProfile>().WithMany().HasForeignKey(f => f.TaxpayerProfileId).OnDelete(Cascade)`
- `HasOne<Report>().WithMany().HasForeignKey(f => f.ReportId).OnDelete(SetNull)` (nullable)

### DI Registration
- `AddTransient<IReportRepository, ReportRepository>()`  (feature 010)
- `AddTransient<IFilingRepository, FilingRepository>()` (feature 011)
- `AddTransient<ICommandHandler<SyncMailboxCommand,...>, SyncMailboxCommandHandler>()`
- `AddTransient<IMailboxSyncService, ImapMailboxSyncService>()`
- `AddTransient<ICommandHandler<ProcessReportsCommand,...>, ProcessReportsCommandHandler>()`

## Tests
- Unit: `ProcessReportsCommandHandlerTests` — mock all deps, test: full happy path, parse failure → Error status, rate not found → skip+error, duplicate detection → skip
- Unit: `FilingRepositoryTests` — EF in-memory, test ExistsByIncomeAsync
