# Quickstart: 044-filings-table-always-visible

## What This Feature Does

Makes the filings DataGrid always visible on the Filings page, even when there are zero filings. Previously, the table was hidden and replaced with a full-page "No filings found" message. Now, column headers are always shown and a subtle empty-state hint appears below the table when no data exists.

## Files to Modify

1. **`src/Rentier.Desktop/Views/FilingsView.axaml`** — Remove `IsVisible="{Binding HasItems}"` from DataGrid; relocate/restyle empty-state TextBlock
2. **`tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`** — Verify existing `IsEmpty`/`HasItems` tests still pass (no ViewModel logic changes expected)
3. **`tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs`** — Add/update headless tests to verify DataGrid renders in empty state

## Key Decisions

- DataGrid visibility is no longer bound to `HasItems` — it's always rendered
- The `HasItems` property remains for select-all checkbox `IsEnabled`
- The `IsEmpty` property remains for the subtle empty-state message
- No ViewModel logic changes — this is purely a view-layer binding change
- The `Filings_Empty` localization key is reused (text may be softened to "No filings yet")

## How to Verify

1. Run the app with an empty database → navigate to Filings page → DataGrid with column headers should be visible
2. Add a filing → table populates without layout shift
3. Delete all filings → table stays visible, subtle empty message appears
4. Run existing tests: `dotnet test tests/Rentier.UnitTests`
