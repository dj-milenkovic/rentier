# Implementation Clarifications: Filings List and Management UI

**Feature**: `012-filings-list-management`  
**Created**: 2025-01-08  
**Source**: Pre-plan static analysis of codebase patterns and spec review  

---

## Overview

Five implementation-level decisions were identified that are not explicitly resolved in the spec but
would cause ambiguity or divergent choices during task execution. Each decision is resolved below
using evidence from the existing codebase (primarily `ImporterSettingsViewModel`,
`GetImportersQueryHandler`, `IFilingRepository`, and `CompositionRoot`).

---

## Decision 1 — FilingRow DTO Field Set

**Question**: What fields does `FilingRowDto` expose — all `Filing` entity fields, or a subset?

**Evidence**:
- The spec already names the DTO `FilingRow` with explicit fields: `Id`, `Status`, `IncomeType`,
  `PayingEntity`, `FilingDeadline` (DateOnly), `TaxPayable` (decimal), `PaymentReference` (string?).
- The `Filing` entity exposes additional fields not required by the UI: `TaxPeriod`, `IncomeDate`,
  `GrossIncomeRsd`, `WhtPaidRsd`, `GrossTaxPayableRsd`, `ReportId`.
- Existing pattern: `ImporterDto` is a read-model subset — it does not expose all repository fields.

**Decision**: `FilingRowDto` is a **projection/subset** containing exactly the 7 fields named in
the spec. Additional entity fields are not exposed through this DTO.

**Field-name alignment note**: The `Filing` entity property is named `TaxPayableRsd`; the DTO field
must be named `TaxPayable` (dropping the `Rsd` suffix) to match the spec column label and to keep
the DTO domain-display-name neutral. The mapping `TaxPayable = filing.TaxPayableRsd` is made inside
`GetFilingsQueryHandler`.

```csharp
// Rentier.Application/DTOs/FilingRowDto.cs
public sealed record FilingRowDto(
    Guid         Id,
    FilingStatus Status,
    IncomeType   IncomeType,
    string       PayingEntity,
    DateOnly     FilingDeadline,
    decimal      TaxPayable,        // maps from Filing.TaxPayableRsd
    string?      PaymentReference);
```

---

## Decision 2 — Pagination Strategy: Server-Side in Query vs Client-Side in VM

**Question**: Does `GetFilingsQuery` accept `page`/`pageSize` params and return a paged result, or
does the ViewModel fetch all records and page client-side?

**Evidence**:
- FR-014 explicitly specifies: "`GetFilingsQuery` — accepts filter mode (All/Unpaid), **page
  number, and page size**; returns a paged result set."
- SC-001 targets ≤ 1 s for up to 500 filings; server-side paging reduces transfer size and is
  consistent with that target.
- Client-side paging of 500+ rows via an `ObservableCollection` would bind the full dataset into
  the DataGrid, complicating filter/page-reset edge cases described in the spec.

**Decision**: Pagination is **server-side (repository-level)**. `GetFilingsQuery` carries
`FilterMode`, `Page` (1-based), and `PageSize`; `GetFilingsQueryHandler` delegates to a new
`IFilingRepository.GetPagedAsync(filter, page, pageSize, ct)` method that uses EF Core `.Skip()`
and `.Take()`. The handler returns a `FilingsPageResult` containing the row list, `TotalCount`, and
`TotalPages`.

```csharp
// Rentier.Application/Queries/GetFilingsQuery.cs
public sealed record GetFilingsQuery(FilingFilterMode Filter, int Page, int PageSize);

// Rentier.Application/DTOs/FilingsPageResult.cs  
public sealed record FilingsPageResult(
    IReadOnlyList<FilingRowDto> Rows,
    int TotalCount,
    int TotalPages);

// Rentier.Application/Enums/FilingFilterMode.cs
public enum FilingFilterMode { Unpaid = 0, All = 1 }
```

`IFilingRepository` gains:
```csharp
Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(
    FilingFilterMode filter, int page, int pageSize, CancellationToken ct = default);
```

---

## Decision 3 — ViewModel Handler Injection Pattern

**Question**: Does `FilingsViewModel` inject `IQueryHandler`/`ICommandHandler` interfaces directly,
or does it go through an intermediate service/facade?

**Evidence**:
- `ImporterSettingsViewModel` injects 6 handler interfaces directly — no facade or mediator.
- `CompositionRoot.AddDesktopServices` registers every handler by its exact generic interface type.
- The constitution does not mention a service aggregation layer; the pattern is handler-per-interface
  injection throughout.

**Decision**: `FilingsViewModel` injects handler interfaces **directly** — no intermediate service.
Constructor parameters follow the same pattern as `ImporterSettingsViewModel`:

```csharp
public FilingsViewModel(
    IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>           getFilings,
    ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>      updateStatus,
    ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>  updateReference,
    ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>            deleteFiling,
    IScheduler? scheduler = null)
```

All four handler types are registered in `CompositionRoot.AddDesktopServices` using
`services.AddTransient<IInterface, Implementation>()`.

---

## Decision 4 — Post-Status-Update UI Refresh Strategy

**Question**: After an inline status change succeeds, does the VM reload the full current page, or
update the row in-place in the `ObservableCollection`?

**Evidence**:
- The spec edge case: *"What happens when a status update succeeds but the currently active filter
  hides the new status? The row disappears from the filtered view immediately."* — this requires
  the displayed set to reflect the post-update filter state, not just the changed row.
- `ImporterSettingsViewModel` always calls `ReloadImportersAsync` after any mutation; in-place
  patching is not used anywhere in the existing codebase.
- Pagination interaction: updating a row in-place cannot account for total-count changes or filter
  changes; a reload keeps the page state consistent.

**Decision**: After a status update (or any mutation — status, payment reference, delete), the VM
calls a **full page reload** via `LoadPageAsync(currentPage, currentFilter)`. The row-level
`FilingRowViewModel` exposes a `ReactiveCommand` that, on success, triggers the parent VM reload.
In-place patching is not used.

Specifically, after `UpdateFilingStatusCommand` returns success:
1. Call `LoadPageAsync(_currentPage, _currentFilter, ct)`.
2. The query returns the updated dataset; the filter drops the row if its new status no longer
   matches (FR-004 edge case satisfied automatically).

---

## Decision 5 — UpdatePaymentReferenceCommand Persistence Path

**Question**: Does `UpdatePaymentReferenceCommandHandler` call `IFilingRepository.UpdateAsync`,
or does it need a dedicated `SetPaymentReferenceAsync(id, reference)` repo method?

**Evidence**:
- `IFilingRepository` already has `UpdateAsync(Filing filing)` — a general entity save, consistent
  with EF Core `_context.Update(entity)` + `SaveChangesAsync`.
- The pattern in `UpdateImporterCommandHandler`: loads entity → mutates via domain method →
  calls `UpdateAsync(entity)`. No dedicated mutation-specific repo method exists in the codebase.
- FR-015 requires the domain entity to expose `SetPaymentReference(string?)` — that method is
  the mutation entry point; persistence then follows the standard `UpdateAsync` path.

**Decision**: `UpdatePaymentReferenceCommandHandler` uses the **existing `UpdateAsync`** path:

```
GetByIdAsync(id) → filing.SetPaymentReference(reference) → UpdateAsync(filing)
```

No new dedicated repository method is needed for this command. The same `UpdateAsync` path is
also used by `UpdateFilingStatusCommandHandler`:

```
GetByIdAsync(id) → filing.AdvanceStatus(newStatus) → UpdateAsync(filing)
```

`DeleteFilingCommand` uses the already-present `IFilingRepository.DeleteAsync(Guid id)`.

---

## Summary Table

| # | Decision | Resolution | Rationale |
|---|----------|------------|-----------|
| 1 | `FilingRowDto` field set | 7-field subset projection; `TaxPayable` maps from `TaxPayableRsd` | Matches spec column list; follows `ImporterDto` subset pattern |
| 2 | Pagination location | Server-side in `GetFilingsQuery` + `GetPagedAsync` | FR-014 mandates it; needed for filter/page-reset edge cases |
| 3 | VM handler injection | Direct `IQueryHandler`/`ICommandHandler` injection, no facade | Established pattern across all existing ViewModels |
| 4 | Post-mutation UI refresh | Full page reload via `LoadPageAsync` | Handles filter-hiding edge case; matches existing reload pattern |
| 5 | `UpdatePaymentReference` persistence | Existing `UpdateAsync(Filing)` path | No dedicated repo method needed; matches `UpdateImporter` pattern |
