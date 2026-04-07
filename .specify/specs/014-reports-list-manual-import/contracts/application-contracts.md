# Application Contracts: Reports List & Manual Import

**Feature**: 014-reports-list-manual-import  
**Branch**: `feature/003-reports-manual-import`  
**Date**: 2026-04-07

---

## Overview

This document defines all new and modified Application-layer contracts introduced by this feature:
queries, commands, handlers, and repository extensions. Desktop-layer consumers and test authors
should use these contracts as the authoritative interface specification.

---

## Queries

### `GetReportsQuery`

```csharp
// Rentier.Application/Queries/GetReportsQuery.cs
namespace Rentier.Application.Queries;

/// <summary>
/// Returns all Report records as display rows, with resolved importer name and filing count.
/// No pagination — all reports are returned in a single call.
/// </summary>
public sealed record GetReportsQuery;
```

**Handler**: `GetReportsQueryHandler`  
**Result**: `Result<IReadOnlyList<ReportRowDto>, Error>`  
**Registration**: `services.AddTransient<IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>, GetReportsQueryHandler>()`

---

### `GetFilingsQuery` (extended)

```csharp
// Rentier.Application/Queries/GetFilingsQuery.cs  — MODIFIED
using Rentier.Application.Enums;

namespace Rentier.Application.Queries;

public sealed record GetFilingsQuery(
    FilingFilterMode Filter,
    int              Page,
    int              PageSize       = 20,
    Guid?            ReportIdFilter = null);
//  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
//  NEW parameter — null means standard paginated mode;
//  non-null bypasses pagination and returns only filings for that report.
```

**Behaviour change**:
- When `ReportIdFilter` is `null` (default): existing paginated behaviour via `GetPagedAsync` — unchanged.
- When `ReportIdFilter` is non-null: calls `IFilingRepository.GetByReportIdAsync(ReportIdFilter.Value, ct)`, wraps all results in `FilingsPageResult(rows, rows.Count, totalPages: 1)`. Paging controls are hidden / disabled in the UI when this mode is active.

---

## Commands

### `ImportReportCommand`

```csharp
// Rentier.Application/Commands/ImportReportCommand.cs
namespace Rentier.Application.Commands;

/// <summary>
/// Imports a CSV brokerage statement manually.
/// CsvContent is the raw file bytes read by the Desktop layer via Avalonia StorageProvider
/// BEFORE this command is dispatched — the handler never touches the file system.
/// </summary>
public sealed record ImportReportCommand(
    Guid   ImporterId,
    string FileName,       // used as ReportName; max 500 characters
    byte[] CsvContent);    // raw CSV bytes validated by handler
```

**Handler**: `ImportReportCommandHandler`  
**Result**: `Result<Guid, Error>` — the newly created `Report.Id` on success  
**Registration**: `services.AddTransient<ICommandHandler<ImportReportCommand, Result<Guid, Error>>, ImportReportCommandHandler>()`

**Handler sequence**:
1. `IStatementParser.ParseAsync(new MemoryStream(CsvContent), ct)` — validate CSV format (FR-007)
   - Failure → `Result.Failure(new Error("INVALID_CSV", parseResult.Error.Message))`
2. `IReportRepository.ExistsByImporterAndNameAsync(ImporterId, FileName, ct)` — duplicate check (FR-008)
   - Duplicate → `Result.Failure(new Error("DUPLICATE_REPORT", "..."))`
3. `Report.Create(ImporterId, FileName, CsvContent, mailboxMessageId: null)` (FR-009)
4. `IReportRepository.AddAsync(report, ct)`
5. `ICommandHandler<ProcessReportsCommand, ...>.HandleAsync(new ProcessReportsCommand(), ct)` (FR-010)
   - Pipeline failure → `Result.Failure(processResult.Error)`
6. Return `Result<Guid, Error>.Success(report.Id)`
7. Any unexpected exception → `Result.Failure(new Error("IMPORT_FAILED", ex.Message))`

**Error codes**:

| Code | Trigger |
|---|---|
| `INVALID_CSV` | CSV parse fails; file is not a valid IBKR format |
| `DUPLICATE_REPORT` | `ExistsByImporterAndNameAsync` returns `true` |
| `IMPORT_FAILED` | Unexpected exception during any step |

---

### `DeleteReportCommand`

```csharp
// Rentier.Application/Commands/DeleteReportCommand.cs
namespace Rentier.Application.Commands;

/// <summary>
/// Deletes a Report and all linked Filings.
/// Cascade deletion is performed at the application layer — not via DB FK cascade.
/// </summary>
public sealed record DeleteReportCommand(Guid ReportId);
```

**Handler**: `DeleteReportCommandHandler`  
**Result**: `Result<VoidResult, Error>`  
**Registration**: `services.AddTransient<ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>, DeleteReportCommandHandler>()`

**Handler sequence**:
```
try
{
    // Step 1: delete linked filings (must come first to avoid FK violations)
    await _filingRepository.DeleteByReportIdAsync(command.ReportId, ct);

    // Step 2: delete the report
    await _reportRepository.DeleteAsync(command.ReportId, ct);

    return Result<VoidResult, Error>.Success(VoidResult.Value);
}
catch (Exception ex)
{
    return Result<VoidResult, Error>.Failure(
        new Error("DELETE_REPORT_FAILED", ex.Message));
}
```

**Notes**:
- `DeleteByReportIdAsync` is idempotent — if no filings exist, it is a no-op (no error).
- `DeleteAsync` on `IReportRepository` is also idempotent — if the report is not found, it is a no-op.
- Both operations are wrapped in a single try/catch; any exception returns `Failure` and the caller
  may be left in a partially-deleted state only if an infrastructure failure occurs between the two
  steps. This risk is acceptable for a single-user SQLite application.

**Error codes**:

| Code | Trigger |
|---|---|
| `DELETE_REPORT_FAILED` | Any exception during delete operations |

---

## Repository Extensions

### `IFilingRepository` — two new methods

```csharp
// Rentier.Application/Repositories/IFilingRepository.cs

/// <summary>
/// Returns the count of Filing records linked to the given Report.
/// Used by GetReportsQueryHandler to populate ReportRowDto.FilingCount without
/// loading full Filing entities (count-only EF query).
/// </summary>
Task<int> GetFilingCountByReportIdAsync(
    Guid reportId,
    CancellationToken ct = default);

/// <summary>
/// Deletes all Filing records whose ReportId matches reportId.
/// Used by DeleteReportCommandHandler BEFORE deleting the parent Report.
///
/// IMPLEMENTATION CONSTRAINT: MUST use load-then-remove pattern:
///   var filings = await _db.Filings.Where(f => f.ReportId == reportId).ToListAsync(ct);
///   _db.Filings.RemoveRange(filings);
///   await _db.SaveChangesAsync(ct);
/// ExecuteDeleteAsync is PROHIBITED — it breaks SQLite in-memory tests.
/// </summary>
Task DeleteByReportIdAsync(
    Guid reportId,
    CancellationToken ct = default);
```

---

## Handler Registration Summary

All registrations in `CompositionRoot.AddDesktopServices()` (NOT `InfrastructureServiceExtensions`).  
All lifetimes are `AddTransient`.

```csharp
// --- Reports handlers (NEW) ---
services.AddTransient<
    IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>,
    GetReportsQueryHandler>();

services.AddTransient<
    ICommandHandler<ImportReportCommand, Result<Guid, Error>>,
    ImportReportCommandHandler>();

services.AddTransient<
    ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>,
    DeleteReportCommandHandler>();

// --- Confirmation delegate for report delete (NEW) ---
services.AddTransient<Func<string, string, Task<bool>>>(provider => (title, msg) =>
    ConfirmDialogHelper.ShowAsync(
        title, msg,
        Strings.Reports_Delete_Confirm_Button,
        Strings.Reports_Delete_Cancel_Button));
```

**Existing registrations preserved unchanged**:
- `ICommandHandler<SyncMailboxCommand, ...>` — registered in `InfrastructureServiceExtensions`
- `ICommandHandler<ProcessReportsCommand, ...>` — registered in `InfrastructureServiceExtensions`
- All Filing handlers — already in `CompositionRoot`

---

## DTO Reference

### `ReportRowDto`

```csharp
// Rentier.Application/DTOs/ReportRowDto.cs
using Rentier.Domain.Enums;

namespace Rentier.Application.DTOs;

public sealed record ReportRowDto(
    Guid         Id,
    string       ReportName,
    DateOnly     ImportDate,
    string       ImporterName,
    ReportStatus Status,
    int          FilingCount);
```

---

## Navigation Contract

### `FilingsViewModel.ReportIdFilter` (property extension)

```csharp
// Rentier.Desktop/ViewModels/FilingsViewModel.cs  — ADD

public Guid? ReportIdFilter
{
    get => _reportIdFilter;
    set
    {
        this.RaiseAndSetIfChanged(ref _reportIdFilter, value);
        _currentPage = 1;
        this.RaisePropertyChanged(nameof(CurrentPage));
        LoadPageCommand.Execute().Subscribe();
    }
}
```

**Contract**: When `ReportIdFilter` is set to a non-null `Guid`, the next `LoadPageCommand`
execution passes `ReportIdFilter` to `GetFilingsQuery`, which triggers the
`GetByReportIdAsync` branch in the handler. The result is wrapped in a single-page
`FilingsPageResult`. Setting `ReportIdFilter` to `null` returns to the standard paginated view.

### `Action<Guid> navigateToFilings` (delegate — wired in `MainWindowViewModel`)

```csharp
// Wired inline in MainWindowViewModel constructor:
Action<Guid> navigateToFilings = reportId =>
{
    filingsVm.ReportIdFilter = reportId;
    SelectedEntry = NavigationEntries.First(e => e.ViewModel is FilingsViewModel);
};
```

**Contract**: `ReportsViewModel` receives this delegate as a constructor parameter named
`navigateToFilings`. It is invoked when the user clicks "View Filings" on a report row. It is
purely synchronous — no `async` / `await`. The delegate does not return a value.

---

## Error Type Reference

```csharp
// Rentier.Application.Common.Error (existing type)
// Used as the error carrier in all Result<T, Error> returns.
// Constructor: new Error(string code, string message)
//   or: new Error(string message)  [single-arg overload if present]
```

All handlers in this feature use `new Error("ERROR_CODE", "Human-readable message")` pattern,
consistent with existing handlers.

---

## Test Contract Checklist

| Handler | Test scenarios |
|---|---|
| `GetReportsQueryHandler` | ✓ returns empty list when no reports; ✓ maps all DTO fields correctly; ✓ resolves ImporterName from dictionary; ✓ returns correct FilingCount per report; ✓ returns Failure on repository exception |
| `ImportReportCommandHandler` | ✓ success path (valid CSV, no duplicate); ✓ returns INVALID_CSV for malformed file; ✓ returns DUPLICATE_REPORT when exists; ✓ triggers ProcessReportsCommand on success; ✓ propagates pipeline failure; ✓ does not persist report on parse failure |
| `DeleteReportCommandHandler` | ✓ deletes filings then report on success; ✓ no-op when report has no filings; ✓ returns Failure on exception; ✓ does not call DeleteAsync(report) if DeleteByReportIdAsync throws |
| `FilingRepository` (infra) | ✓ GetFilingCountByReportIdAsync returns 0 for unknown reportId; ✓ returns correct count; ✓ DeleteByReportIdAsync deletes all matching filings; ✓ is idempotent when no filings exist |
