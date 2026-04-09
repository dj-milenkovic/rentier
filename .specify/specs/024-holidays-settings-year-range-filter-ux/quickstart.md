# Quickstart: Holidays Settings — Year-Range Filter & UX Improvements

**Feature**: 024-holidays-settings-year-range-filter-ux  
**Branch**: `004-holidays-year-filter-ux`

## What This Feature Does

Adds in-memory year-range filtering to the Holidays settings page so that changing the Start Year or End Year selectors immediately filters which holidays are displayed in the DataGrid. Also adds helper text explaining the year-range purpose, a visual separator, and a range-specific empty-state message.

## Files to Modify

| # | File | Change Type | Description |
|---|------|------------|-------------|
| 1 | `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs` | **Modify** | Add `FilteredEntries`, `ShowFilterEmptyState`, `ShowGenericEmptyState`, `HasFilteredItems` properties. Add `RefreshFilteredEntries()` method. Wire reactive subscriptions in `WhenActivated`. |
| 2 | `src/Rentier.Desktop/Views/HolidaySettingsView.axaml` | **Modify** | Change DataGrid binding from `Entries` to `FilteredEntries`. Add helper text TextBlock, Separator, and conditional empty-state TextBlocks. |
| 3 | `src/Rentier.Desktop/Resources/Strings.resx` | **Modify** | Add `Holidays_YearRange_HelperText` and `Holidays_FilterEmpty_Message` entries. |
| 4 | `tests/Rentier.Desktop.Tests/HolidaySettingsViewModelTests.cs` | **Modify** | Add unit tests for filtering logic, empty-state properties, and edge cases. |

## Implementation Steps

### Step 1: Add Resource Strings

In `Strings.resx`, add:
```
Holidays_YearRange_HelperText = Showing holidays for the selected year range. The range also determines which years are pre-seeded on first run.
Holidays_FilterEmpty_Message = No holidays configured for this range.
```

### Step 2: Add ViewModel Properties and Filtering Logic

In `HolidaySettingsViewModel.cs`:

1. Add `FilteredEntries` as a new `ObservableCollection<HolidayEntryViewModel>` (initialized in constructor).
2. Add computed properties: `ShowFilterEmptyState`, `ShowGenericEmptyState`, `HasFilteredItems`.
3. Add `RefreshFilteredEntries()` private method:
   ```csharp
   private void RefreshFilteredEntries()
   {
       FilteredEntries.Clear();
       foreach (var entry in Entries.Where(e => e.Date.Year >= StartYear && e.Date.Year <= EndYear))
           FilteredEntries.Add(entry);
       this.RaisePropertyChanged(nameof(HasFilteredItems));
       this.RaisePropertyChanged(nameof(ShowFilterEmptyState));
       this.RaisePropertyChanged(nameof(ShowGenericEmptyState));
   }
   ```
4. In the `WhenActivated` block, subscribe to triggers:
   - `this.WhenAnyValue(x => x.StartYear, x => x.EndYear)` → `RefreshFilteredEntries()`
   - `Entries.CollectionChanged` → resubscribe to item `Date` observables + `RefreshFilteredEntries()`
   - Per-item `WhenAnyValue(x => x.Date)` merged → `RefreshFilteredEntries()`
   - Apply `Throttle(TimeSpan.FromMilliseconds(50))` to coalesce rapid changes
5. Call `RefreshFilteredEntries()` at the end of `LoadAsync()` after populating `Entries`.

### Step 3: Update the View

In `HolidaySettingsView.axaml`:

1. Add helper text TextBlock below the year-range StackPanel:
   ```xml
   <TextBlock DockPanel.Dock="Top"
              Text="{x:Static res:Strings.Holidays_YearRange_HelperText}"
              Opacity="0.6" Margin="8,2,8,4" FontSize="12" />
   ```

2. Add Separator:
   ```xml
   <Separator DockPanel.Dock="Top" Margin="8,4" />
   ```

3. Change DataGrid binding:
   ```xml
   <DataGrid ItemsSource="{Binding FilteredEntries}" ... IsVisible="{Binding HasFilteredItems}" />
   ```

4. Replace the existing empty-state TextBlock with two conditional ones:
   ```xml
   <!-- Generic empty state: no holidays at all -->
   <TextBlock DockPanel.Dock="Top"
              Text="No holidays configured. Click Add or Import to get started."
              IsVisible="{Binding ShowGenericEmptyState}"
              HorizontalAlignment="Center" Opacity="0.6" Margin="8,16" />

   <!-- Filter empty state: holidays exist but none match range -->
   <TextBlock DockPanel.Dock="Top"
              Text="{x:Static res:Strings.Holidays_FilterEmpty_Message}"
              IsVisible="{Binding ShowFilterEmptyState}"
              HorizontalAlignment="Center" Opacity="0.6" Margin="8,16" />
   ```

### Step 4: Add Unit Tests

In `HolidaySettingsViewModelTests.cs`, add tests for:

1. `FilteredEntries_WhenRangeCoversAllEntries_ShowsAll`
2. `FilteredEntries_WhenRangeExcludesSomeEntries_ShowsOnlyMatching`
3. `FilteredEntries_WhenStartYearChanges_UpdatesImmediately`
4. `FilteredEntries_WhenEndYearChanges_UpdatesImmediately`
5. `FilteredEntries_WhenEntryDateEdited_OutsideRange_DisappearsFromFiltered`
6. `FilteredEntries_WhenEntryAdded_WithinRange_AppearsInFiltered`
7. `FilteredEntries_WhenEntryAdded_OutsideRange_DoesNotAppearInFiltered`
8. `FilteredEntries_WhenStartYearGreaterThanEndYear_ReturnsEmpty`
9. `ShowFilterEmptyState_WhenEntriesExistButNoneMatchRange_ReturnsTrue`
10. `ShowFilterEmptyState_WhenEntriesEmpty_ReturnsFalse`
11. `ShowGenericEmptyState_WhenEntriesEmpty_ReturnsTrue`
12. `ShowGenericEmptyState_WhenEntriesExist_ReturnsFalse`
13. `SaveCommand_PersistsAllEntries_NotJustFiltered`

## Key Design Decisions

- **`FilteredEntries` is a separate `ObservableCollection`**, not a wrapper around `Entries`. This keeps the save workflow unaffected and makes the reactive pipeline simple.
- **No DynamicData dependency** — manual rebuild is sufficient for the small collection size.
- **50ms throttle** on the merged observable prevents churn during bulk operations.
- **Two distinct empty-state properties** rather than a single enum — simpler for AXAML binding.
- **Save always reads from `Entries`** (unfiltered) — the filter is purely a display concern.

## Build & Test

```bash
# Build
dotnet build src/Rentier.Desktop/Rentier.Desktop.csproj

# Run tests
dotnet test tests/Rentier.Desktop.Tests/Rentier.Desktop.Tests.csproj --filter "HolidaySettings"

# Run all tests
dotnet test
```
