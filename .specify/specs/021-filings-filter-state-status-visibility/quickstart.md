# Quickstart: Filings Page Filter State & Status Visibility

**Feature**: `001-filings-filter-status`
**Date**: 2025-07-15

## Prerequisites

- .NET 8 SDK installed
- Avalonia workload (comes with the project; no additional install needed)
- Repository cloned and on branch `001-filings-filter-status`

## Build & Run

```bash
# From repository root
cd src/Rentier.Desktop
dotnet build
dotnet run
```

Navigate to the **Filings** page in the left sidebar to see the filter buttons and filing list.

## What This Feature Changes

### 1. ToggleButton Active-State (User Story 1)

**Files**: `src/Rentier.Desktop/Views/FilingsView.axaml`

The "All" and "Unpaid" ToggleButtons at the top of the Filings page now show a clear visual distinction when active. The checked button uses the FluentTheme accent colour (solid background with white text), while the unchecked button uses the default subtle style.

**How to verify**:
1. Open the Filings page
2. The "Unpaid" button should be highlighted (accent colour) by default
3. Click "All" — it becomes highlighted, "Unpaid" returns to default
4. Click the already-active button — nothing changes

### 2. Status Badge / Pill (User Story 2)

**Files**:
- `src/Rentier.Desktop/ViewModels/FilingRowViewModel.cs` (new property)
- `src/Rentier.Desktop/Converters/FilingStatusToBadgeBrushConverter.cs` (new file)
- `src/Rentier.Desktop/Views/FilingsView.axaml` (new badge column)

Each filing row now displays a colour-coded pill badge showing the filing status:
- **Init** → Amber badge (`#D4A017`)
- **Filed** → Blue badge (`#0063B1`)
- **Paid** → Green badge (`#107C10`)

The badge appears as a new column next to the existing Status dropdown. It is read-only — users cannot click or interact with it.

**How to verify**:
1. Open the Filings page
2. Each row should show a coloured pill with the status text
3. Verify colours match the status (amber/blue/green)
4. Confirm the badge cannot be clicked or edited

### 3. Filter Change Feedback (User Story 3)

**Files**: No new changes — existing `ProgressBar` + `IsLoading` mechanism already handles this.

**How to verify**:
1. Switch between "All" and "Unpaid" filters
2. Observe the thin progress bar at the top during reload
3. Observe the rows clearing and repopulating

## Running Tests

```bash
# Run all Desktop tests (existing + new)
cd tests/Rentier.Desktop.Tests
dotnet test

# Run only the new tests for this feature
dotnet test --filter "FullyQualifiedName~FilingRowViewModel" 
dotnet test --filter "FullyQualifiedName~FilingStatusToBadgeBrush"
```

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Scoped `:checked` styles on ToggleButtons | Uses Avalonia pseudo-classes; no custom controls needed |
| `Border` + `TextBlock` for badge | Simplest Avalonia primitive composition for a pill shape |
| Single converter with parameter for bg/fg | Reduces file count while supporting both background and foreground |
| `StatusDisplayText` property on ViewModel | Enables unit testing without Avalonia runtime |
| Fixed colours (not theme resources) | Spec requires specific status-colour associations |

## Files Changed (Summary)

| File | Change Type | Description |
|------|-------------|-------------|
| `src/Rentier.Desktop/Views/FilingsView.axaml` | Modified | Add toggle styles + badge column |
| `src/Rentier.Desktop/ViewModels/FilingRowViewModel.cs` | Modified | Add `StatusDisplayText` property |
| `src/Rentier.Desktop/Converters/FilingStatusToBadgeBrushConverter.cs` | New | Status → brush converter |
| `tests/Rentier.Desktop.Tests/ViewModels/FilingRowViewModelTests.cs` | New | Badge text tests |
| `tests/Rentier.Desktop.Tests/Converters/FilingStatusToBadgeBrushConverterTests.cs` | New | Colour mapping tests |

## Architecture Notes

- **Clean Architecture**: All changes in `Rentier.Desktop` only. No Domain/Application/Infrastructure changes.
- **No new packages**: Uses existing Avalonia UI primitives and FluentTheme resources.
- **No data model changes**: Reads existing `FilingStatus` enum and `FilingRowDto.Status` property.
- **Existing ComboBox preserved**: The status dropdown remains for editing; the badge supplements it for scanning.
