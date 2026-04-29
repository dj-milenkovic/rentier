# Quickstart: Filings Visual Sorting

## What This Feature Does

Adds visual sort arrows (↑/↓) to sortable DataGrid column headers on the Filings page, removes the redundant Unpaid/All filter toggle buttons, and removes the text-based sort indicator from the toolbar.

## Key Files to Change

| File | Change |
|---|---|
| `src/Rentier.Desktop/Views/FilingsView.axaml` | Remove radio buttons + sort text; add custom header templates with sort arrows |
| `src/Rentier.Desktop/Views/FilingsView.axaml.cs` | Update `DataGrid_Sorting` to implement 3-state cycle |
| `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` | Make `SortColumn` nullable, default `ShowAll=true`, remove `SortIndicatorDisplay` |
| `src/Rentier.Desktop/Converters/SortArrowConverter.cs` | **NEW** — converter to determine arrow geometry from sort state |
| `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs` | Update sort tests for 3-state cycle, update default filter expectation |
| `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs` | Add test verifying sort arrows render and radio buttons are absent |

## Architecture Impact

- **Desktop layer only** — no Domain, Application, or Infrastructure changes
- `FilingSortColumn` enum in Application layer is unchanged (nullable wrapper is Desktop-only)
- `GetFilingsQuery` already accepts `FilingSortColumn` — when ViewModel sends unsorted state, it falls back to default database ordering

## How to Verify

1. Build: `dotnet build src/Rentier.Desktop`
2. Run tests: `dotnet test tests/Rentier.UnitTests --filter "Category=Desktop|Category=UI"`
3. Manual: Launch app → navigate to Filings → click column headers → verify arrows cycle correctly
4. Verify: No radio buttons visible in toolbar, no sort text in toolbar
