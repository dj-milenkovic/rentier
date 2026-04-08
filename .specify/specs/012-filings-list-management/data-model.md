# Data Model: Filings List and Management UI (012)

**Feature**: `012-filings-list-management`  
**Created**: 2025-01-08  
**Layers affected**: Domain · Application · Infrastructure

---

## 1. Domain Entity Changes

### `Filing` (aggregate root) — additions only

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `PaymentReference` | `string?` | nullable, ≤ 200 chars, trimmed | New in 012; null → no reference entered |

**New method**:

```
SetPaymentReference(string? reference)
  - Trims input
  - Empty string after trim → stored as null
  - Length > 200 → throws DomainException
  - No status precondition (UI enforces Filed-only editable; domain does not)
```

**Existing state machine** (unchanged):

```
FilingStatus.Init ──AdvanceStatus(Filed)──► FilingStatus.Filed
FilingStatus.Filed──AdvanceStatus(Paid)───► FilingStatus.Paid
Any other transition → DomainException
```

---

## 2. Application DTOs

### `FilingFilterMode` (enum)

| Value | Int | Meaning |
|-------|-----|---------|
| `Unpaid` | 0 | Show `Init` and `Filed` filings only |
| `All` | 1 | Show all filings regardless of status |

### `FilingRowDto` (read model — 7 fields)

| Field | Type | Source |
|-------|------|--------|
| `Id` | `Guid` | `Filing.Id` |
| `Status` | `FilingStatus` | `Filing.Status` |
| `IncomeType` | `IncomeType` | `Filing.IncomeType` |
| `PayingEntity` | `string` | `Filing.PayingEntity` |
| `FilingDeadline` | `DateOnly` | `Filing.FilingDeadline` |
| `TaxPayable` | `decimal` | `Filing.TaxPayableRsd` (renamed at boundary) |
| `PaymentReference` | `string?` | `Filing.PaymentReference` |

### `FilingsPageResult`

| Field | Type | Meaning |
|-------|------|---------|
| `Rows` | `IReadOnlyList<FilingRowDto>` | Items for the current page |
| `TotalCount` | `int` | Total matching records (across all pages) |
| `TotalPages` | `int` | `⌈TotalCount / PageSize⌉` |

---

## 3. Application Commands & Queries

### `GetFilingsQuery`

| Field | Type | Default |
|-------|------|---------|
| `Filter` | `FilingFilterMode` | — |
| `Page` | `int` | — |
| `PageSize` | `int` | `20` |

**Returns**: `Result<FilingsPageResult, Error>`  
**Validation**: `Page ≥ 1`, `PageSize ∈ [1, 100]`

### `UpdateFilingStatusCommand`

| Field | Type |
|-------|------|
| `FilingId` | `Guid` |
| `NewStatus` | `FilingStatus` |

**Returns**: `Result<VoidResult, Error>`  
**Domain rule**: delegates to `Filing.AdvanceStatus(newStatus)`; wraps `DomainException` as `Error`

### `UpdatePaymentReferenceCommand`

| Field | Type |
|-------|------|
| `FilingId` | `Guid` |
| `PaymentReference` | `string?` |

**Returns**: `Result<VoidResult, Error>`  
**Domain rule**: delegates to `Filing.SetPaymentReference(reference)`; wraps `DomainException` as `Error`

### `DeleteFilingCommand`

| Field | Type |
|-------|------|
| `FilingId` | `Guid` |

**Returns**: `Result<VoidResult, Error>`  
**Behaviour**: idempotent — no error if filing not found (DeleteAsync is a no-op if missing)

---

## 4. Repository Interface Extension

### `IFilingRepository` additions

```csharp
Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(
    FilingFilterMode filter,
    int page,
    int pageSize,
    CancellationToken ct = default);
```

**Query semantics**:
- Filter `Unpaid` → `WHERE Status IN (0, 1)` (Init or Filed)
- Filter `All` → no status filter
- Sort: `ORDER BY FilingDeadline ASC` (fixed; no user-adjustable sort)
- Pagination: `SKIP (page-1)*pageSize TAKE pageSize`
- Returns `(Items, TotalCount)` where `TotalCount` is the filtered-before-paging count

---

## 5. Database Schema Change

### Migration 0009: `FilingPaymentReference`

**Table**: `Filings`  
**Operation**: `ADD COLUMN PaymentReference TEXT NULL`  
**Constraint**: EF `HasMaxLength(200)` enforced at application boundary; SQLite TEXT has no native length limit but EF validation fires before write  
**Reversible**: `DROP COLUMN PaymentReference`

#### Updated `Filings` table columns (post-012)

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | TEXT (GUID) | NOT NULL | PK |
| `TaxpayerProfileId` | TEXT (GUID) | NOT NULL | FK → TaxpayerProfiles |
| `TaxPeriod` | TEXT (DateOnly) | NOT NULL | |
| `Status` | INTEGER | NOT NULL | 0=Init, 1=Filed, 2=Paid |
| `IncomeType` | INTEGER | NOT NULL | 0=Dividend, 1=Interest |
| `PayingEntity` | TEXT | NOT NULL | max 500 |
| `IncomeDate` | TEXT (DateOnly) | NOT NULL | |
| `GrossIncomeRsd` | DECIMAL(18,2) | NOT NULL | |
| `WhtPaidRsd` | DECIMAL(18,2) | NOT NULL | |
| `GrossTaxPayableRsd` | DECIMAL(18,2) | NOT NULL | |
| `TaxPayableRsd` | DECIMAL(18,2) | NOT NULL | |
| `FilingDeadline` | TEXT (DateOnly) | NOT NULL | |
| `ReportId` | TEXT (GUID) | NULL | FK → Reports |
| `PaymentReference` | TEXT | **NULL** | **NEW in 012**; max 200 |

---

## 6. Desktop View-Model State

### `FilingRowViewModel` (display-only snapshot)

| Property | Type | Derived from |
|----------|------|-------------|
| `Id` | `Guid` | `FilingRowDto.Id` |
| `Status` | `FilingStatus` | `FilingRowDto.Status` |
| `IncomeType` | `IncomeType` | `FilingRowDto.IncomeType` |
| `PayingEntity` | `string` | `FilingRowDto.PayingEntity` |
| `FilingDeadline` | `DateOnly` | `FilingRowDto.FilingDeadline` |
| `TaxPayable` | `decimal` | `FilingRowDto.TaxPayable` |
| `PaymentReference` | `string?` | `FilingRowDto.PaymentReference` |
| `DeadlineDisplay` | `string` | `FilingDeadline.ToString("yyyy-MM-dd")` |
| `TaxPayableDisplay` | `string` | `$"{TaxPayable:N2} RSD"` |
| `IsPaymentReferenceEditable` | `bool` | `Status == FilingStatus.Filed` |

### `FilingsViewModel` reactive state

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `Rows` | `ObservableCollection<FilingRowViewModel>` | empty | Bound to DataGrid |
| `IsLoading` | `bool` | `false` | Shows progress indicator |
| `ErrorMessage` | `string?` | `null` | Shows error banner |
| `ShowAll` | `bool` | `false` | Filter toggle; false = Unpaid |
| `CurrentPage` | `int` | `1` | 1-based page index |
| `TotalPages` | `int` | `1` | Computed from query result |
| `TotalCount` | `int` | `0` | Total matching records |
| `PageIndicator` | `string` | — | Formatted "Page X of Y" |
| `IsEmpty` | `bool` | — | `Rows.Count == 0 && !IsLoading` |
| `HasPreviousPage` | `bool` | — | `CurrentPage > 1` |
| `HasNextPage` | `bool` | — | `CurrentPage < TotalPages` |

### Commands

| Command | Type | canExecute |
|---------|------|------------|
| `LoadPageCommand` | `ReactiveCommand<Unit, Unit>` | always |
| `PreviousPageCommand` | `ReactiveCommand<Unit, Unit>` | `HasPreviousPage && !IsLoading` |
| `NextPageCommand` | `ReactiveCommand<Unit, Unit>` | `HasNextPage && !IsLoading` |
| `AdvanceStatusCommand` | `ReactiveCommand<(Guid, FilingStatus), Unit>` | `!IsLoading` |
| `SavePaymentRefCommand` | `ReactiveCommand<(Guid, string?), Unit>` | `!IsLoading` |
| `DeleteFilingCommand` | `ReactiveCommand<Guid, Unit>` | `!IsLoading` |
| `ClearErrorCommand` | `ReactiveCommand<Unit, Unit>` | always |
