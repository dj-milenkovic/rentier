# Data Model: Bulk Delete for Filings and Reports

**Feature**: 025-bulk-delete-fillings-reports  
**Date**: 2025-07-15

## Overview

This feature introduces **no new entities or database schema changes**. It adds new CQRS commands, repository methods, and ViewModel properties that operate on the existing `Filing` and `Report` entities.

---

## Existing Entities (Unchanged)

### Filing (Aggregate Root)

| Field | Type | Nullable | Notes |
|-------|------|----------|-------|
| `Id` | `Guid` | No | PK, ValueGeneratedNever |
| `TaxpayerProfileId` | `Guid` | No | FK → TaxpayerProfile (Cascade) |
| `ReportId` | `Guid?` | Yes | FK → Report (SetNull) |
| `TaxPeriod` | `DateOnly` | No | Constitution Principle III |
| `Status` | `FilingStatus` | No | Init → Filed → Paid |
| `IncomeType` | `IncomeType` | No | Enum |
| `PayingEntity` | `string` | No | Max 500 chars |
| `IncomeDate` | `DateOnly` | No | |
| `GrossIncomeRsd` | `decimal(18,2)` | No | Constitution Principle III |
| `WhtPaidRsd` | `decimal(18,2)` | No | Constitution Principle III |
| `GrossTaxPayableRsd` | `decimal(18,2)` | No | Constitution Principle III |
| `TaxPayableRsd` | `decimal(18,2)` | No | Constitution Principle III |
| `FilingDeadline` | `DateOnly` | No | |
| `PaymentReference` | `string?` | Yes | Max 200 chars |
| `ExchangeRateSourceDate` | `DateOnly?` | Yes | |
| `ExchangeRateSourceType` | `ExchangeRateSourceType?` | Yes | |

**Indexes**: `TaxpayerProfileId`, `ReportId`

### Report (Entity)

| Field | Type | Nullable | Notes |
|-------|------|----------|-------|
| `Id` | `Guid` | No | PK, ValueGeneratedNever |
| `ImporterId` | `Guid` | No | FK → Importer (Cascade) |
| `OriginalReportId` | `Guid?` | Yes | FK → Report self-ref (SetNull) |
| `ImportDate` | `DateOnly` | No | Constitution Principle III |
| `Status` | `ReportStatus` | No | Init → Processed / PartialError / Error |
| `ReportName` | `string` | No | Max 500 chars |
| `AttachmentContent` | `byte[]?` | Yes | |
| `MailboxMessageId` | `long?` | Yes | |
| `EmailDate` | `DateOnly?` | Yes | |

**Indexes**: `ImporterId`, `(ImporterId, ReportName)` unique, `OriginalReportId`

### Relationship: Report → Filing

```text
Report (1) ──── (0..*) Filing
  via Filing.ReportId (nullable FK)
  ON DELETE: SetNull (EF Core configuration)
```

**Important for bulk delete**: The EF Core `SetNull` behavior only nullifies `Filing.ReportId` — it does **not** delete the filings. The spec requires that bulk-deleting reports also deletes linked filings. This is handled at the **application layer** by calling `IFilingRepository.DeleteByReportIdAsync` before deleting the report, consistent with the existing `DeleteReportCommandHandler`.

---

## New CQRS Types

### Commands

#### `BulkDeleteFilingsCommand`

```csharp
namespace Rentier.Application.Commands;

/// <summary>
/// Deletes multiple Filings by their IDs in a single batch operation.
/// Missing IDs are silently skipped (idempotent).
/// </summary>
public sealed record BulkDeleteFilingsCommand(IReadOnlyList<Guid> FilingIds);
```

**Validation**: Handler returns `Error.Domain` if `FilingIds` is null or empty.

#### `BulkDeleteReportsCommand`

```csharp
namespace Rentier.Application.Commands;

/// <summary>
/// Deletes multiple Reports and all their linked Filings in a single operation.
/// Linked filings are deleted first to avoid FK violations.
/// Missing IDs are silently skipped (idempotent).
/// </summary>
public sealed record BulkDeleteReportsCommand(IReadOnlyList<Guid> ReportIds);
```

**Validation**: Handler returns `Error.Domain` if `ReportIds` is null or empty.

### Handler Return Types

Both handlers return `Result<VoidResult, Error>`, consistent with existing delete handlers.

---

## New Repository Methods

### `IFilingRepository` — Addition

```csharp
/// <summary>
/// Deletes all Filing records whose Id is in the given list.
/// Missing IDs are silently skipped. Uses load-then-remove pattern.
/// </summary>
Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
```

### `IReportRepository` — Addition

```csharp
/// <summary>
/// Deletes all Report records whose Id is in the given list.
/// Missing IDs are silently skipped. Uses load-then-remove pattern.
/// Callers MUST delete linked filings before calling this method.
/// </summary>
Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
```

### Implementation Pattern (both repositories)

```csharp
public async Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
{
    if (ids.Count == 0) return;

    var entities = await _db.Filings // or _db.Reports
        .Where(e => ids.Contains(e.Id))
        .ToListAsync(ct);

    if (entities.Count == 0) return;

    _db.Filings.RemoveRange(entities); // or _db.Reports
    await _db.SaveChangesAsync(ct);
}
```

This matches the existing `DeleteByReportIdAsync` pattern in `FilingRepository`.

---

## New ViewModel Properties

### Row ViewModel Additions (both `FilingRowViewModel` and `ReportRowViewModel`)

| Property | Type | Binding | Notes |
|----------|------|---------|-------|
| `IsSelected` | `bool` | TwoWay | Observable, raises `PropertyChanged` |

### Parent ViewModel Additions (both `FilingsViewModel` and `ReportsViewModel`)

| Property | Type | Computed From | Notes |
|----------|------|---------------|-------|
| `SelectedCount` | `int` | `Rows.Count(r => r.IsSelected)` | Reactive via row subscription |
| `HasSelection` | `bool` | `SelectedCount > 0` | Controls "Delete Selected" visibility |
| `DeleteSelectedLabel` | `string` | `string.Format(Strings.BulkDelete_Button_Template, SelectedCount)` | Reactive |

### Parent ViewModel Commands (both)

| Command | Type | CanExecute | Notes |
|---------|------|------------|-------|
| `SelectAllCommand` | `ReactiveCommand<Unit, Unit>` | `HasItems` | Sets `IsSelected = true` on all rows |
| `ClearSelectionCommand` | `ReactiveCommand<Unit, Unit>` | `HasSelection` | Sets `IsSelected = false` on all rows |
| `BulkDeleteCommand` | `ReactiveCommand<Unit, Unit>` | `HasSelection && !IsExecuting` | Shows dialog, dispatches command, reloads |

---

## Localisation Strings (Strings.resx Additions)

| Key | Value | Used In |
|-----|-------|---------|
| `BulkDelete_SelectAll_Button` | `Select All` | Toolbar |
| `BulkDelete_ClearSelection_Button` | `Clear Selection` | Toolbar |
| `BulkDelete_Button_Template` | `Delete Selected ({0})` | Toolbar |
| `BulkDelete_Filings_Confirmation_Title` | `Delete Filings` | Dialog |
| `BulkDelete_Filings_Confirmation_Message` | `You are about to delete {0} filing(s). This action cannot be undone.` | Dialog |
| `BulkDelete_Reports_Confirmation_Title` | `Delete Reports` | Dialog |
| `BulkDelete_Reports_Confirmation_Message` | `You are about to delete {0} report(s). All filings linked to the selected reports will also be deleted. This action cannot be undone.` | Dialog |
| `BulkDelete_Confirm_Button` | `Delete` | Dialog |
| `BulkDelete_Cancel_Button` | `Cancel` | Dialog |
| `BulkDelete_Error_NoSelection` | `No items selected for deletion.` | Error |
| `BulkDelete_Error_Failed` | `Bulk delete failed: {0}` | Error |

---

## State Transitions Diagram

```text
Selection State Machine (per page):

  [Page Load / Navigation]
       │
       ▼
  ┌─────────────┐
  │ No Selection │◄──── Clear Selection
  │ HasSel=false │◄──── Post-Delete Reload
  └──────┬──────┘
         │ Check any row
         ▼
  ┌─────────────┐
  │ Has Selection│──── Select All ──► all rows checked
  │ HasSel=true  │◄─── Uncheck row (if still ≥1)
  └──────┬──────┘
         │ Click "Delete Selected (N)"
         ▼
  ┌─────────────┐
  │ Confirm      │──── Cancel ──► back to Has Selection (unchanged)
  │ Dialog       │
  └──────┬──────┘
         │ Confirm
         ▼
  ┌─────────────┐
  │ Deleting     │  (button disabled, loading indicator shown)
  │ IsExecuting  │
  └──────┬──────┘
         │ Complete
         ▼
  ┌─────────────┐
  │ Reload       │──► selection cleared, list refreshed
  └─────────────┘
```
