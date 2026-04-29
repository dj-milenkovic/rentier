# Quickstart: Filings Per-Report Filter Chip

**Feature**: 043-filings-filter-chip  
**Estimated Complexity**: Low (Desktop layer only, ~50 lines ViewModel + ~20 lines AXAML)

## What This Feature Does

Adds a dismissible chip ("Filtered by report ✕") to the Filings page filter bar when the user navigates from the Reports page via "View Filings". Clicking ✕ clears the filter and reloads all filings.

## Files to Modify

| File | Change |
|---|---|
| `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` | Add `HasReportFilter` OAPH, `ClearReportFilterCommand` |
| `src/Rentier.Desktop/Views/FilingsView.axaml` | Add chip element in filter bar |
| `src/Rentier.Desktop/Resources/Strings.resx` | Add 2 localization keys |
| `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs` | Add chip visibility + dismiss tests |
| `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs` | Add chip rendering tests |

## Implementation Sequence

1. **ViewModel** (`FilingsViewModel.cs`):
   - Add `HasReportFilter` as `ObservableAsPropertyHelper<bool>` derived from `ReportIdFilter`
   - Add `ClearReportFilterCommand` as `ReactiveCommand.Create(() => ReportIdFilter = null)` with `canExecute: HasReportFilter`
   - Wire in `WhenActivated` block

2. **Localization** (`Strings.resx`):
   - Add `Filings_FilterChip_Report` = "Filtered by report"
   - Add `Filings_FilterChip_Dismiss` = "Remove report filter"

3. **View** (`FilingsView.axaml`):
   - Insert chip after the radio buttons StackPanel, inside the existing DockPanel
   - Chip structure: `Border` > `StackPanel` > `TextBlock` + `Button`
   - Bind `IsVisible` to `HasReportFilter`, dismiss `Command` to `ClearReportFilterCommand`

4. **Tests**:
   - ViewModel: chip visible when `ReportIdFilter` set, hidden when null, dismiss clears filter
   - Headless: chip renders when filter active, disappears on dismiss

## Key Design Decisions

- **No new Application/Domain code** — filter is UI state, clearing it triggers existing reactive pipeline
- **Follows existing badge pattern** — `Border` + `CornerRadius` matching status badges
- **Standard Button for ✕** — inherits keyboard accessibility (Tab, Enter/Space)
- **ObservableAsPropertyHelper** for `HasReportFilter` — canonical ReactiveUI pattern for derived properties

## Build & Test

```bash
# Build
dotnet build src/Rentier.Desktop/Rentier.Desktop.csproj

# Run relevant tests
dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~FilingsViewModel"
```
