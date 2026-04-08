# Quickstart — 011 Filing Generation Pipeline

## What Was Built

Feature 011 implements the end-to-end PP-OPO tax filing generation pipeline. Given a set
of IBKR CSV reports sitting in the database with status `Init`, this pipeline:

1. Parses each CSV
2. Fetches NBS exchange rates (with cross-rate fallback via IBKR embedded rates)
3. Calculates tax at 15%
4. Calculates the filing deadline (income date + 30 days, adjusted for weekends and Serbian holidays)
5. Persists a `Filing` record per income event

The `Filing` aggregate root was enriched with eight new fields
(`IncomeDate`, `IncomeType`, `PayingEntity`, `GrossIncomeRsd`, `WhtPaidRsd`,
`GrossTaxPayableRsd`, `TaxPayableRsd`, `FilingDeadline`).
EF migration `0008` creates the `Filings` table.

---

## Running the Pipeline

### From Desktop (recommended)

The `ReportsViewModel` exposes a `ProcessReportsCommand` reactive command wired to
`ProcessReportsCommandHandler`. Click **"Process Reports"** in the Reports view.

### From a Test / Integration Harness

```csharp
// Resolve handler from DI
var handler = serviceProvider.GetRequiredService<
    ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>>();

var result = await handler.HandleAsync(new ProcessReportsCommand(), CancellationToken.None);

if (result.IsSuccess)
{
    Console.WriteLine($"Created {result.Value.FilingsCreated} filings");
    Console.WriteLine($"Processed {result.Value.ReportsProcessed} reports");
    if (result.Value.ReportsErrored > 0)
        foreach (var err in result.Value.Errors) Console.WriteLine(err);
}
```

---

## Running Migrations

```bash
# From repo root
dotnet ef database update \
    --project src/Rentier.Infrastructure \
    --startup-project src/Rentier.Desktop
```

Migration `0008_FilingsTable` creates the `Filings` table. Rollback via `0007`.

---

## Adding a Test Report (Development)

```csharp
// Seed a Report with Init status and IBKR CSV bytes
var report = Report.Create(
    importerId: existingImporter.Id,
    reportName: "IBKR_2024_Activity.csv",
    attachmentContent: File.ReadAllBytes("path/to/IBKR.csv"),
    mailboxMessageId: null);

await reportRepository.AddAsync(report);

// Then run the pipeline
await handler.HandleAsync(new ProcessReportsCommand(), ct);
```

---

## Key Files

| File | Notes |
|---|---|
| `src/Rentier.Domain/Entities/Filing.cs` | Entity + `CreateFromIncome` factory |
| `src/Rentier.Application/Commands/ProcessReportsCommand.cs` | Command record |
| `src/Rentier.Application/DTOs/ProcessReportsResult.cs` | Result record |
| `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs` | Pipeline orchestration |
| `src/Rentier.Application/Repositories/IFilingRepository.cs` | Repository interface |
| `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` | EF implementation |
| `src/Rentier.Infrastructure/Persistence/Configurations/FilingConfiguration.cs` | EF mapping |
| `src/Rentier.Infrastructure/Persistence/Migrations/20260407130000_0008_FilingsTable.cs` | DB migration |

---

## Test Suite (Remaining Work)

Three test files need to be created:

```
tests/Rentier.Domain.Tests/FilingCreateFromIncomeTests.cs
tests/Rentier.Application.Tests/ProcessReportsCommandHandlerTests.cs
tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs
```

See `plan.md → Test Plan` for the full list of test cases.

---

## Common Errors

| Error | Cause | Fix |
|---|---|---|
| `"Exchange rate not found for USD on 2024-01-15"` | NBS does not publish rates on weekends/holidays | Run `ImportHolidaysFromWebCommand` then retry; NBS typically backfills weekend rates |
| `"Report X: importer not found"` | Report references a deleted importer | Re-create the importer or delete the orphaned report |
| `"Report X: no attachment content"` | Report was saved without bytes | Re-sync the mailbox to re-download the attachment |
| `"Report X: parse failed"` | CSV format changed or file is corrupted | Inspect the raw attachment; check `IbkrCsvParser` logs |

---

## Architecture Notes

- `ProcessReportsCommandHandler` lives in **Application** and depends only on interfaces.
- `TaxCalculationService` and `FilingDeadlineCalculator` are **Domain** static services — no DI required.
- The rate provider lambda is built in the handler and closes over the parsed `StatementParseResult`, keeping cross-rate logic in Application (not Domain).
- All money arithmetic uses `decimal` with `MidpointRounding.AwayFromZero` (banker's rounding excluded per Serbian tax rules).
