# Implementation Plan: Holiday Fetcher — timeanddate.com Scraper

**Feature**: 026-holiday-fetcher-timeanddate.com-scraper  
**Branch**: `026-holiday-web-scraper`

## Problem Statement

The Holidays settings page has an existing single-year `ImportCommand` that **replaces all entries** on every fetch. The declared `FetchFromWebCommand` is never initialized (NullReferenceException if invoked). There is no multi-year fetch capability and no merge-by-date logic.

## What Already Exists (Do NOT Recreate)

| Artifact | Status |
|---|---|
| `TimeAndDateHolidayScraper.cs` | ✅ Complete — scrapes correctly, filters `tr.showrow`, checks "National Holiday" type |
| `IHolidayImporter.cs` | ✅ Complete — single-year interface |
| `ImportHolidaysFromWebCommand.cs` | ✅ Complete — single-year command record |
| `ImportHolidaysFromWebCommandHandler.cs` | ✅ Complete — delegates to `IHolidayImporter` |
| `SaveHolidayConfCommandHandler.cs` | ✅ Complete |
| `GetHolidayConfQueryHandler.cs` | ✅ Complete |

## What Must Be Built

### Step 1 — Application: Multi-year command + handler

**New file**: `src/Rentier.Application/Commands/FetchHolidaysFromWebCommand.cs`
```csharp
public sealed record FetchHolidaysFromWebCommand(int StartYear, int EndYear);
```

**New file**: `src/Rentier.Application/Handlers/FetchHolidaysFromWebCommandHandler.cs`
- Loops `StartYear..EndYear` (inclusive)
- Calls `IHolidayImporter.ImportAsync(year, ct)` for each year
- Aggregates results, de-duplicates by `DateOnly`
- Partial failure: collect per-year errors, continue to next year
- Returns `Result<FetchHolidaysResult, Error>` where `FetchHolidaysResult` carries both the merged list and any per-year errors
- Or simpler: return `Result<IReadOnlyList<HolidayEntryDto>, Error>` and surface per-year warnings via a separate property

**Decision**: Return `Result<IReadOnlyList<HolidayEntryDto>, Error>` — success if at least one year fetched, failure only if ALL years fail. Per-year failure count exposed via a separate `WarningMessage` on the VM.

### Step 2 — String Resources

**File**: `src/Rentier.Desktop/Resources/Strings.resx` (and `.Designer.cs`)

Add:
- `Holidays_FetchFromWeb_Button` = "Fetch from web"
- `Holidays_FetchFromWeb_Success` = "Fetched {0} holidays for {1} year(s)"
- `Holidays_FetchFromWeb_PartialFailure` = "Fetched {0} holidays. {1} year(s) failed: {2}"

### Step 3 — ViewModel: Wire FetchFromWebCommand, fix merge

**File**: `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs`

Changes:
1. Remove `ImportYear` property (no longer needed — uses StartYear..EndYear range)
2. Remove `ImportCommand` field and its initialization
3. Wire `FetchFromWebCommand` as `ReactiveCommand<Unit, Unit>` (no parameter):
   - Calls `FetchHolidaysFromWebCommandHandler` with `(StartYear, EndYear)`
   - **Merge behavior**: union fetched dates with `Entries`, de-duplicate by `DateOnly`
   - Sets `HasUnsavedChanges = true` on success
   - Shows success message with count
   - Shows error/partial-failure message

**Merge logic**:
```csharp
var existingDates = Entries.Select(e => e.Date).ToHashSet();
foreach (var dto in result.Value)
{
    if (!existingDates.Contains(dto.Date))
        Entries.Add(HolidayEntryViewModel.FromDto(dto));
}
```

4. Inject `ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>` instead of the old import handler

### Step 4 — XAML: Replace import UI with "Fetch from web" button

**File**: `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`

Changes:
- Remove `NumericUpDown` for `ImportYear` and `Button` for `ImportCommand`
- Add `<Button Content="{x:Static res:Strings.Holidays_FetchFromWeb_Button}" Command="{Binding FetchFromWebCommand}" />`
- Button placed next to Save in the top toolbar

### Step 5 — DI Registration

**File**: `src/Rentier.Infrastructure/DependencyInjection.cs` (or equivalent)

Register `FetchHolidaysFromWebCommandHandler` as `ICommandHandler<FetchHolidaysFromWebCommand, ...>`.

## Architecture Compliance

- Domain: no changes (no schema changes needed)
- Application: new command + handler only (no infrastructure dependencies)
- Infrastructure: no changes (scraper already complete)
- Desktop: ViewModel + XAML only
- `IHolidayImporter` only called from explicit user action (CA-EXT-001 ✅)

## Test Strategy

**Unit tests** (xUnit + FluentAssertions + NSubstitute):
- `FetchHolidaysFromWebCommandHandler`: multi-year merge, partial failure, all-fail
- `HolidaySettingsViewModel`: FetchFromWebCommand success, merge deduplication, partial failure message, IsLoading cycle

**Integration tests** (optional, lower priority):
- `TimeAndDateHolidayScraper` against saved `holidays.txt` HTML fixture
