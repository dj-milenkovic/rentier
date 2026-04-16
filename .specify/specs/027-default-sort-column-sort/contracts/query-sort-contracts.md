# Contracts: Query Sort Parameters

**Feature**: 027-default-sort-column-sort  
**Date**: 2025-07-24

## GetFilingsQuery Contract

### Input

```csharp
public sealed record GetFilingsQuery(
    FilingFilterMode Filter = FilingFilterMode.Unpaid,
    int Page = 1,
    int PageSize = 20,
    Guid? ReportIdFilter = null,
    FilingSortColumn SortColumn = FilingSortColumn.FilingDeadline,
    bool SortDescending = true);
```

### Output

```csharp
// Unchanged
public sealed record FilingsPageResult(
    IReadOnlyList<FilingRowDto> Rows,
    int TotalCount,
    int TotalPages);
```

### Validation

| Rule | Response |
|------|----------|
| `Page < 1` | `Result.Failure("VALIDATION_ERROR", "Page must be >= 1.")` |
| `PageSize < 1 \|\| PageSize > 100` | `Result.Failure("VALIDATION_ERROR", "PageSize must be between 1 and 100.")` |
| `SortColumn` is undefined enum value | `Result.Failure("VALIDATION_ERROR", "Invalid sort column.")` |

### Sort Behavior

| Condition | Sort Applied |
|-----------|-------------|
| Default (no sort params) | `FilingDeadline DESC, Id ASC` |
| `SortColumn=TaxPayable, SortDescending=false` | `TaxPayableRsd ASC, Id ASC` |
| `ReportIdFilter` is set | Sort params ignored (all linked filings returned unsorted in single page) |

---

## GetReportsQuery Contract

### Input

```csharp
public sealed record GetReportsQuery(
    bool SortDescending = true);
```

### Output

```csharp
// Unchanged
Result<IReadOnlyList<ReportRowDto>, Error>
```

### Sort Behavior

| Condition | Sort Applied |
|-----------|-------------|
| Default (`SortDescending=true`) | `ImportDate DESC, Id ASC` |
| `SortDescending=false` | `ImportDate ASC, Id ASC` |

---

## IFilingRepository.GetPagedAsync Contract

### Signature

```csharp
Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(
    FilingFilterMode filter,
    int skip,
    int take,
    FilingSortColumn sortColumn = FilingSortColumn.FilingDeadline,
    bool sortDescending = true,
    CancellationToken ct = default);
```

### Column-to-Property Mapping

| `FilingSortColumn` Value | Filing Property | EF Core Expression |
|--------------------------|-----------------|-------------------|
| `FilingDeadline` | `FilingDeadline` | `f => f.FilingDeadline` |
| `Status` | `Status` | `f => (int)f.Status` |
| `IncomeType` | `IncomeType` | `f => (int)f.IncomeType` |
| `PayingEntity` | `PayingEntity` | `f => f.PayingEntity` |
| `TaxPayable` | `TaxPayableRsd` | `f => f.TaxPayableRsd` |
| `PaymentReference` | `PaymentReference` | `f => f.PaymentReference` |

### Ordering Guarantee

All queries include a deterministic secondary sort by `Filing.Id` ascending to prevent row-order flickering when the primary sort column has duplicate values.

---

## IReportRepository.GetAllAsync Contract

### Signature

```csharp
Task<IReadOnlyList<Report>> GetAllAsync(
    bool sortDescending = true,
    CancellationToken ct = default);
```

### Ordering Guarantee

All queries include a deterministic secondary sort by `Report.Id` ascending.

---

## FilingsViewModel Sort Command Contract

### ApplySortCommand Input

```text
Parameter: (string ColumnTag, bool? CurrentDirection)
  - ColumnTag: matches FilingSortColumn enum name (e.g., "FilingDeadline", "TaxPayable")
  - CurrentDirection: null if column has no prior sort; true = descending, false = ascending
```

### State Transition Rules

| Current Column | Clicked Column | Effect |
|----------------|---------------|--------|
| `FilingDeadline` | `FilingDeadline` | Toggle `SortDescending`, keep page |
| `FilingDeadline` | `TaxPayable` | Set column=`TaxPayable`, `SortDescending=false` (ascending), reset page to 1 |
| `TaxPayable` | `TaxPayable` | Toggle `SortDescending`, keep page |

### Exposed Properties (for DataGrid header indicators)

| Property | Type | Description |
|----------|------|-------------|
| `SortColumn` | `FilingSortColumn` | Currently active sort column |
| `SortDescending` | `bool` | Current sort direction |
