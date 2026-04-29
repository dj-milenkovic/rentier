# Data Model: 050 — Filings Filter Header Flyouts

## Modified Entities

### FilingColumnFilter (Application Layer — extends existing record)

**File**: `src/Rentier.Application/Queries/FilingColumnFilter.cs`

Current:
```csharp
public sealed record FilingColumnFilter(
    FilingStatus? Status = null,
    IncomeType? IncomeType = null,
    string? PayingEntity = null,
    DateOnly? FilingDeadline = null,
    string? PaymentReference = null);
```

Extended (additive — no breaking changes):
```csharp
public sealed record FilingColumnFilter(
    FilingStatus? Status = null,
    IncomeType? IncomeType = null,
    string? PayingEntity = null,
    DateOnly? FilingDeadline = null,
    string? PaymentReference = null,
    IReadOnlySet<FilingStatus>? Statuses = null,
    IReadOnlySet<IncomeType>? IncomeTypes = null,
    string? FilingDeadlineText = null);
```

| Field               | Type                         | Usage                                                     |
|---------------------|------------------------------|-----------------------------------------------------------|
| `Statuses`          | `IReadOnlySet<FilingStatus>?` | Multi-select status filter from flyout checkboxes         |
| `IncomeTypes`       | `IReadOnlySet<IncomeType>?`   | Multi-select income type filter from flyout checkboxes    |
| `FilingDeadlineText`| `string?`                     | Text-based deadline search (e.g. "2025-07")               |

**Validation rules**:
- `Statuses` null or empty → no status filter (same as all selected)
- `IncomeTypes` null or empty → no income type filter
- `FilingDeadlineText` null or whitespace → no deadline text filter
- Old single-value fields (`Status`, `IncomeType`, `FilingDeadline`) remain for backward compatibility but are not used by flyout UI

### FilingRepository WHERE clause additions

```csharp
// Existing single-value filters remain unchanged
// New multi-select additions:
if (columnFilter.Statuses is { Count: > 0 })
    query = query.Where(f => columnFilter.Statuses.Contains(f.Status));
if (columnFilter.IncomeTypes is { Count: > 0 })
    query = query.Where(f => columnFilter.IncomeTypes.Contains(f.IncomeType));
if (!string.IsNullOrEmpty(columnFilter.FilingDeadlineText))
    query = query.Where(f => EF.Functions.Like(
        f.FilingDeadline.ToString(), $"%{columnFilter.FilingDeadlineText}%"));
```

## New ViewModels (Desktop Layer)

### CheckableItem\<T\>

Simple item for enum flyout checkbox lists.

```csharp
public sealed class CheckableItem<T> : ReactiveObject
{
    public T Value { get; }
    public string Label { get; }
    public bool IsChecked { get; set; } // RaiseAndSetIfChanged
}
```

### EnumFilterFlyoutViewModel\<T\>

Manages working state for an enum checkbox flyout.

| Property            | Type                              | Description                          |
|---------------------|-----------------------------------|--------------------------------------|
| `Items`             | `ObservableCollection<CheckableItem<T>>` | Checkbox items                |
| `IsOpen`            | `bool`                            | Popup open state                     |
| `IsActive`          | `bool`                            | True when filter is applied (not all selected) |
| `ApplyCommand`      | `ReactiveCommand<Unit, Unit>`     | Commits checked items, closes popup  |
| `SelectAllCommand`  | `ReactiveCommand<Unit, Unit>`     | Checks all items                     |
| `ClearCommand`      | `ReactiveCommand<Unit, Unit>`     | Unchecks all items                   |

**Behavior**:
- On open: snapshot current filter state into `Items`
- On Apply: commit checked items to parent VM filter property, set `IsOpen = false`
- On light-dismiss (`IsOpen` set to false externally): discard changes (no commit)
- `IsActive`: true when the committed set != all values

### TextFilterFlyoutViewModel

Manages working state for a text search flyout.

| Property            | Type                              | Description                          |
|---------------------|-----------------------------------|--------------------------------------|
| `SearchText`        | `string?`                         | Working copy of filter text          |
| `IsOpen`            | `bool`                            | Popup open state                     |
| `IsActive`          | `bool`                            | True when committed filter text is non-empty |
| `ApplyCommand`      | `ReactiveCommand<Unit, Unit>`     | Commits text, closes popup           |

**Behavior**:
- On open: copy current committed text into `SearchText`
- On Apply: commit `SearchText` to parent VM, set `IsOpen = false`
- On light-dismiss: discard `SearchText` changes

### FilingsViewModel — Modified Properties

| Change                            | From                                      | To                                        |
|-----------------------------------|-------------------------------------------|-------------------------------------------|
| `FilterDeadline`                  | `DateTimeOffset?`                         | `string?`                                 |
| `StatusFilterOptions`             | removed (was ComboBox items)              | —                                         |
| `IncomeTypeFilterOptions`         | removed (was ComboBox items)              | —                                         |
| `IsFilterRowEnabled`              | removed (filter row removed)              | —                                         |
| New: `StatusFlyout`               | —                                         | `EnumFilterFlyoutViewModel<FilingStatus>`  |
| New: `IncomeTypeFlyout`           | —                                         | `EnumFilterFlyoutViewModel<IncomeType>`    |
| New: `PayingEntityFlyout`         | —                                         | `TextFilterFlyoutViewModel`                |
| New: `DeadlineFlyout`             | —                                         | `TextFilterFlyoutViewModel`                |
| New: `PaymentReferenceFlyout`     | —                                         | `TextFilterFlyoutViewModel`                |

**Filter property semantics change**:
- `FilterStatus` remains `FilingStatus?` but is **no longer used** for single-select. Multi-select uses `StatusFlyout.GetSelectedValues()` → `IReadOnlySet<FilingStatus>` directly in `LoadPageAsync`.
- Same for `FilterIncomeType`.
- `FilterDeadline` changes from `DateTimeOffset?` to `string?`.

### HasActiveFilters Computation Update

```csharp
HasActiveFilters =
    StatusFlyout.IsActive ||
    IncomeTypeFlyout.IsActive ||
    PayingEntityFlyout.IsActive ||
    DeadlineFlyout.IsActive ||
    PaymentReferenceFlyout.IsActive;
```

## New UI Resources

### Icons (Icons.axaml)

```xml
<!-- Lucide filter (funnel) — 24×24 viewport -->
<StreamGeometry x:Key="FilterIcon">M22 3H2l8 9.46V19l4 2v-8.54L22 3z</StreamGeometry>
```

### Localized Strings (Strings.resx)

| Key                       | Value (sr-Latn)  | Usage                    |
|---------------------------|------------------|--------------------------|
| `Filter_Search`           | `Pretraži...`    | Text flyout placeholder  |
| `Filter_Apply`            | `Primeni`        | Apply button label       |
| `Filter_SelectAll`        | `Izaberi sve`    | Enum flyout select all   |
| `Filter_Clear`            | `Obriši`         | Enum flyout clear        |
