# Contract — ProcessReportsCommand Pipeline

## Command

```csharp
namespace Rentier.Application.Commands;

/// <summary>
/// Triggers the filing generation pipeline.
/// Loads all Init reports, parses them, computes tax, and persists Filing records.
/// </summary>
public sealed record ProcessReportsCommand;
```

No input parameters. Scans all reports in `Init` status.

---

## Result

```csharp
namespace Rentier.Application.DTOs;

public sealed record ProcessReportsResult(
    int FilingsCreated,
    int ReportsProcessed,
    int ReportsErrored,
    IReadOnlyList<string> Errors);
```

| Field | Meaning |
|---|---|
| `FilingsCreated` | Total `Filing` records inserted across all processed reports |
| `ReportsProcessed` | Reports that completed without any per-record error |
| `ReportsErrored` | Reports that encountered at least one error (or a fatal parse failure) |
| `Errors` | Human-readable error messages for each skipped record or failed report |

---

## Handler Interface

```csharp
ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>
```

Registered as `AddTransient` in `InfrastructureServiceExtensions`.

---

## Repository Contracts Consumed

### `IReportRepository`

| Method | Purpose |
|---|---|
| `GetByStatusAsync(ReportStatus.Init, ct)` | Load all unprocessed reports |
| `UpdateAsync(report, ct)` | Persist status change (Processed or Error) |

### `IImporterRepository`

| Method | Purpose |
|---|---|
| `GetByIdAsync(importerId, ct)` | Resolve importer to get `TaxpayerProfileId` |

### `IFilingRepository`

| Method | Purpose |
|---|---|
| `ExistsByIncomeAsync(profileId, entity, date, grossRsd, ct)` | Duplicate check |
| `AddAsync(filing, ct)` | Persist new Filing |

### `IHolidayRepository`

| Method | Purpose |
|---|---|
| `GetHolidayConfAsync(ct)` | Load `HolidayConfDto` to build `HolidayConf` |

### `IExchangeRateFetcher`

| Method | Purpose |
|---|---|
| `FetchRateAsync(date, currency, ct)` | NBS rate lookup (with SQLite cache) |

### `IStatementParser`

| Method | Purpose |
|---|---|
| `ParseAsync(stream, ct)` | Parse IBKR CSV attachment bytes |

---

## Rate Provider Delegate Signature

```csharp
Func<DateOnly, string, Task<ExchangeRate>> rateProvider
```

Passed to `TaxCalculationService.CalculateAsync`. Built by `BuildRateProvider(parsed, ct)`:

```
(date, currency) =>
  1. result = IExchangeRateFetcher.FetchRateAsync(date, currency)
  2. if result.IsSuccess → return result.Value
  3. else find IbkrExchangeRate where FromCurrency == currency (case-insensitive)
  4. if found → usd = FetchRateAsync(date, "USD") → return ExchangeRate(date, currency, ibkr.Rate × usd.RateToRsd)
  5. else throw InvalidOperationException("Exchange rate not found for {currency} on {date}")
```

The thrown exception is caught in the per-record try/catch, appended to `errors`, and `reportHadError` is set to true.

---

## Error Handling Contract

| Condition | Behaviour |
|---|---|
| Importer not found | Skip report, SetStatus(Error), add error message |
| Importer.TaxpayerProfileId is null | Skip report, SetStatus(Error), add error message |
| AttachmentContent is null or empty | Skip report, SetStatus(Error), add error message |
| `IStatementParser.ParseAsync` returns failure | Skip report, SetStatus(Error), add parse error message |
| Exchange rate not found (direct + cross-rate) | Skip income record, add error, `reportHadError = true` |
| `TaxCalculationService` throws DomainException | Skip income record, add error, `reportHadError = true` |
| Duplicate detected (`ExistsByIncomeAsync` = true) | Skip income record silently (not an error) |
| `OperationCanceledException` | Propagated; not caught |

At end of each report:
- If `reportHadError` → `SetStatus(Error)`, increment `reportsErrored`
- Otherwise → `SetStatus(Processed)`, increment `reportsProcessed`

---

## Idempotency

The pipeline is designed to be re-runnable. Reports are only loaded if status is `Init`.
After the first successful run, reports are set to `Processed` (or `Error`) and will not be
re-processed. Within a run, `ExistsByIncomeAsync` prevents duplicate `Filing` records even
if the same income event appears in two different reports.
