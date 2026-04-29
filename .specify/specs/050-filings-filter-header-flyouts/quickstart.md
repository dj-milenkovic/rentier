# Quickstart: 050 — Filings Filter Header Flyouts

## What This Feature Does

Replaces the inline filter row (feature 045) in the Filings page with Excel-style filter flyout popups inside each column header. Users click a funnel icon next to the sort arrow to open a filter popup — checkboxes for enum columns (Status, Income Type), text search for text/date columns.

## Architecture Overview

```
FilingsView.axaml
  └─ DataGridTemplateColumn headers (per filterable column)
       ├─ TextBlock (column label)
       ├─ PathIcon (sort arrow — existing, unchanged)
       └─ Button (funnel icon) → toggles Popup
            └─ Popup (IsLightDismissEnabled)
                 ├─ EnumFilterFlyout: CheckBox list + Select All / Clear + Apply
                 └─ TextFilterFlyout: TextBox + Apply

FilingsViewModel
  ├─ StatusFlyout: EnumFilterFlyoutViewModel<FilingStatus>
  ├─ IncomeTypeFlyout: EnumFilterFlyoutViewModel<IncomeType>
  ├─ PayingEntityFlyout: TextFilterFlyoutViewModel
  ├─ DeadlineFlyout: TextFilterFlyoutViewModel
  └─ PaymentReferenceFlyout: TextFilterFlyoutViewModel
       │
       ▼ (on Apply → commits filter values)
  FilterStatus / FilterIncomeType / FilterPayingEntity / FilterDeadline / FilterPaymentReference
       │
       ▼ (reactive pipeline → LoadPageAsync)
  FilingColumnFilter (Application layer — extended with Statuses, IncomeTypes, FilingDeadlineText)
       │
       ▼
  GetFilingsQuery → FilingRepository.GetPagedAsync (Infrastructure — WHERE clauses)
```

## Key Files to Touch

| File | Change |
|------|--------|
| `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` | Add flyout VMs, change FilterDeadline type, update filter→query mapping, remove old ComboBox properties |
| `src/Rentier.Desktop/ViewModels/EnumFilterFlyoutViewModel.cs` | **New** — generic enum flyout state management |
| `src/Rentier.Desktop/ViewModels/TextFilterFlyoutViewModel.cs` | **New** — text flyout state management |
| `src/Rentier.Desktop/ViewModels/CheckableItem.cs` | **New** — checkbox item for enum flyouts |
| `src/Rentier.Desktop/Views/FilingsView.axaml` | Remove filter row, add funnel icons + Popups in column headers |
| `src/Rentier.Desktop/Assets/Icons.axaml` | Add FilterIcon geometry |
| `src/Rentier.Desktop/Converters/FilterActiveConverter.cs` | **New** — bool→brush for funnel icon active state |
| `src/Rentier.Desktop/Resources/Strings.resx` | Add Filter_Search, Filter_Apply, Filter_SelectAll, Filter_Clear |
| `src/Rentier.Application/Queries/FilingColumnFilter.cs` | Add Statuses, IncomeTypes, FilingDeadlineText optional fields |
| `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` | Add WHERE clauses for new multi-select fields |
| `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs` | Update filter tests for flyout VMs |
| `tests/Rentier.UnitTests/Desktop/EnumFilterFlyoutViewModelTests.cs` | **New** |
| `tests/Rentier.UnitTests/Desktop/TextFilterFlyoutViewModelTests.cs` | **New** |

## Implementation Order

1. **Backend extension** — Extend `FilingColumnFilter` + repository WHERE clauses (minimal, safe)
2. **Flyout ViewModels** — Create `EnumFilterFlyoutViewModel<T>`, `TextFilterFlyoutViewModel`, `CheckableItem<T>`
3. **ViewModel wiring** — Add flyout VM properties to `FilingsViewModel`, wire Apply→filter→reload pipeline
4. **Icons & strings** — Add `FilterIcon` geometry, localized strings
5. **AXAML view** — Remove filter row, restructure column headers with funnel + Popup
6. **Active indicator** — Add `FilterActiveConverter`, bind funnel icon foreground
7. **Tests** — Unit tests for flyout VMs, update existing ViewModel tests

## Gotchas

- **Popup in DataGrid header**: Avalonia DataGrid column headers are not standard layout containers. The Popup must be placed inside the `DataGridTemplateColumn.Header` content and must use `PlacementTarget` binding for correct positioning.
- **Light-dismiss vs Apply**: The Popup's `IsLightDismissEnabled` handles click-outside → close. The flyout VM must distinguish between "Apply clicked" (commit) and "dismissed" (discard). Use a flag in the Apply command.
- **FilterDeadline type change**: Changing from `DateTimeOffset?` to `string?` affects the reactive pipeline subscription — `FilterDeadline` moves from the "instant reload" group to the "debounced text" group.
- **EF Core DateOnly LIKE**: SQLite stores `DateOnly` as text in ISO format. `EF.Functions.Like(column, pattern)` should work if EF Core generates the right SQL. Verify with integration test.
- **ReportIdFilter interaction**: When `ReportIdFilter` is set, all flyout `IsOpen` should be prevented (disabled funnel buttons) and all flyout IsActive states should be false.
