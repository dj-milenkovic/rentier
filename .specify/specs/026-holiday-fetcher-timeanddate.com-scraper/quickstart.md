# Quick Implementation Guide: Holiday Fetcher — timeanddate.com Scraper

**Feature Branch**: `026-holiday-web-scraper`  
**Created**: 2025-07-18

## Prerequisites

- Existing code from specs 003 (Holiday Configuration) and 024 (Year-Range Filter UX) is in place
- AngleSharp NuGet package already referenced in `Rentier.Infrastructure`
- `TimeAndDateHolidayScraper`, `IHolidayImporter`, `ImportHolidaysFromWebCommand`, and handler already exist

## What Exists vs. What Needs Changing

### Already Implemented ✅

| Component | File | Status |
| --------- | ---- | ------ |
| IHolidayImporter interface | `Application/Interfaces/IHolidayImporter.cs` | Complete |
| ImportHolidaysFromWebCommand | `Application/Commands/ImportHolidaysFromWebCommand.cs` | Complete |
| ImportHolidaysFromWebCommandHandler | `Application/Handlers/ImportHolidaysFromWebCommandHandler.cs` | Complete |
| TimeAndDateHolidayScraper | `Infrastructure/Scraping/TimeAndDateHolidayScraper.cs` | Complete |
| DI registration | `Infrastructure/InfrastructureServiceExtensions.cs` | Complete |
| "Import from Web" button in AXAML | `Desktop/Views/HolidaySettingsView.axaml` | Exists (needs rename) |
| ImportCommand in ViewModel | `Desktop/ViewModels/HolidaySettingsViewModel.cs` | Exists (needs refactor) |

### Changes Required 🔧

| # | Layer | Change | File(s) |
| - | ----- | ------ | ------- |
| 1 | Desktop | **Merge logic**: Change ImportCommand from replace-all to merge-by-date | `HolidaySettingsViewModel.cs` |
| 2 | Desktop | **Multi-year fetch**: Loop through StartYear..EndYear instead of single year | `HolidaySettingsViewModel.cs` |
| 3 | Desktop | **Rename command**: `ImportCommand` → `FetchFromWebCommand` for consistency | `HolidaySettingsViewModel.cs` |
| 4 | Desktop | **Update AXAML binding**: Change `Command="{Binding ImportCommand}"` to `FetchFromWebCommand` | `HolidaySettingsView.axaml` |
| 5 | Desktop | **Success message**: Show count of holidays added per year | `HolidaySettingsViewModel.cs` |
| 6 | Desktop | **Partial failure reporting**: Aggregate success/failure for multi-year fetch | `HolidaySettingsViewModel.cs` |
| 7 | Desktop | **Remove ImportYear control**: Button uses StartYear–EndYear range, not separate ImportYear | `HolidaySettingsView.axaml`, `HolidaySettingsViewModel.cs` |
| 8 | Desktop | **Update localization**: Rename button text to "Fetch from web", update related strings | `Resources/Strings.resx` |

---

## Implementation Steps (Ordered)

### Step 1: Rename Command and Remove ImportYear

**ViewModel** (`HolidaySettingsViewModel.cs`):
- Remove the `_importYear` field, `ImportYear` property, and the unused `FetchFromWebCommand` declaration
- Rename the `ImportCommand` assignment to `FetchFromWebCommand`
- Change command signature from `ReactiveCommand<int, Unit>` to `ReactiveCommand<Unit, Unit>`

**View** (`HolidaySettingsView.axaml`):
- Remove the "Year:" label and `NumericUpDown` for `ImportYear`
- Change `Command="{Binding ImportCommand}"` to `Command="{Binding FetchFromWebCommand}"`
- Remove `CommandParameter="{Binding ImportYear}"`

### Step 2: Implement Merge-by-Date Logic

Replace the current replace-all pattern in FetchFromWebCommand:

**Current** (lines 128–130):
```csharp
Entries.Clear();
foreach (var dto in result.Value) 
    Entries.Add(HolidayEntryViewModel.FromDto(dto));
```

**New**:
```csharp
var existingDates = Entries.Select(e => e.Date).ToHashSet();
var added = 0;
foreach (var dto in result.Value)
{
    if (!existingDates.Contains(dto.Date))
    {
        Entries.Add(HolidayEntryViewModel.FromDto(dto));
        existingDates.Add(dto.Date); // prevent intra-batch dupes
        added++;
    }
}
```

### Step 3: Implement Multi-Year Fetch

Change the command body to loop through the year range:

```csharp
FetchFromWebCommand = ReactiveCommand.CreateFromTask(async (CancellationToken ct) =>
{
    IsLoading = true;
    ErrorMessage = null;
    SuccessMessage = null;
    try
    {
        var existingDates = Entries.Select(e => e.Date).ToHashSet();
        var totalAdded = 0;
        var successYears = new List<int>();
        var failedYears = new List<(int Year, string Reason)>();

        for (var year = StartYear; year <= EndYear; year++)
        {
            var cmd = new ImportHolidaysFromWebCommand(year);
            var result = await _importHandler.HandleAsync(cmd, ct);

            if (result.IsSuccess)
            {
                var added = 0;
                foreach (var dto in result.Value)
                {
                    if (!existingDates.Contains(dto.Date))
                    {
                        Entries.Add(HolidayEntryViewModel.FromDto(dto));
                        existingDates.Add(dto.Date);
                        added++;
                    }
                }
                totalAdded += added;
                successYears.Add(year);
            }
            else
            {
                failedYears.Add((year, result.Error.Message));
            }
        }

        HasUnsavedChanges = totalAdded > 0 || HasUnsavedChanges;

        // Build summary message
        if (failedYears.Count == 0)
            SuccessMessage = $"Fetched {totalAdded} holidays for {string.Join(", ", successYears)}.";
        else if (successYears.Count > 0)
        {
            SuccessMessage = $"Fetched {totalAdded} holidays for {string.Join(", ", successYears)}.";
            ErrorMessage = $"Failed: {string.Join("; ", failedYears.Select(f => $"{f.Year} — {f.Reason}"))}";
        }
        else
            ErrorMessage = $"Fetch failed: {string.Join("; ", failedYears.Select(f => $"{f.Year} — {f.Reason}"))}";
    }
    finally { IsLoading = false; }
}, notLoading);
```

### Step 4: Update Localization Strings

In `Resources/Strings.resx`:
- Rename `Holidays_Import_Button` → `Holidays_FetchFromWeb_Button` with value "Fetch from web"
- Add `Holidays_FetchSuccess` = "Fetched {0} holidays for {1}."
- Add `Holidays_FetchPartialFailure` = "Failed: {0}"
- Update `Holidays_ImportError_Prefix` → `Holidays_FetchError_Prefix`

### Step 5: Write Tests

**ViewModel tests** (`tests/Rentier.Desktop.Tests/`):
- Test merge-by-date: fetch with existing entries, verify no duplicates
- Test multi-year: mock handler for 3 years, verify all added
- Test partial failure: one year fails, others succeed, verify message
- Test loading state: FetchFromWebCommand disables during execution
- Test no data modified on full failure

**Infrastructure integration tests** (`tests/Rentier.Infrastructure.Tests/`):
- Test TimeAndDateHolidayScraper against a saved HTML fixture
- Test parse error on malformed HTML
- Test empty result (no "National Holiday" rows)

---

## Testing Strategy

### Unit Tests (ViewModel)

```
FetchFromWebCommand_SingleYear_MergesWithoutDuplicates
FetchFromWebCommand_MultiYear_FetchesAllYearsInRange
FetchFromWebCommand_PartialFailure_ReportsSuccessAndFailure
FetchFromWebCommand_AllFail_ShowsErrorPreservesData
FetchFromWebCommand_WhileLoading_ButtonDisabled
FetchFromWebCommand_Success_SetsHasUnsavedChanges
FetchFromWebCommand_NoNewHolidays_ReportsZeroAdded
```

### Integration Tests (Scraper)

```
ImportAsync_ValidHtml_ReturnsNationalHolidays
ImportAsync_NoHolidaysTable_ReturnsParseError
ImportAsync_NoNationalHolidays_ReturnsNotFound
ImportAsync_MalformedDate_SkipsInvalidEntries
ImportAsync_NetworkError_ReturnsImportFailed
```

---

## Files Changed (Summary)

```
Modified:
  src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs
  src/Rentier.Desktop/Views/HolidaySettingsView.axaml
  src/Rentier.Desktop/Resources/Strings.resx

New test files:
  tests/Rentier.Desktop.Tests/ViewModels/HolidaySettingsViewModelFetchTests.cs
  tests/Rentier.Infrastructure.Tests/Scraping/TimeAndDateHolidayScraperTests.cs
  tests/Rentier.Infrastructure.Tests/Scraping/Fixtures/holidays-2026.html
```
