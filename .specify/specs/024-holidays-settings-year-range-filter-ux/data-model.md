# Data Model: Holidays Settings — Year-Range Filter & UX Improvements

**Feature**: 024-holidays-settings-year-range-filter-ux  
**Date**: 2025-07-15

## Overview

This feature introduces no new entities or database changes. All modifications are within the existing `HolidaySettingsViewModel` in the Desktop layer. The data model below documents the ViewModel property additions and their relationships to existing entities.

## Existing Entities (Unchanged)

### HolidayEntryViewModel

| Field | Type | Description |
|-------|------|-------------|
| `Date` | `DateOnly` | Holiday date (reactive, `RaiseAndSetIfChanged`) |
| `Name` | `string` | Holiday display name (reactive, `RaiseAndSetIfChanged`) |

**Location**: `src/Rentier.Desktop/ViewModels/HolidayEntryViewModel.cs`  
**Role**: Individual row in the holidays DataGrid. Already reactive — `Date` property changes trigger `PropertyChanged`.

### PublicHoliday (Domain Entity — unchanged)

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Unique identifier |
| `Date` | `DateOnly` | Holiday date |
| `Name` | `string` | Holiday display name |
| `Year` | `int` | Computed from `Date.Year` |

**Location**: `src/Rentier.Domain/Entities/PublicHoliday.cs`  
**Note**: Not modified by this feature.

### HolidayYearRange (Domain Entity — unchanged)

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` | Singleton (always 1) |
| `StartYear` | `int` | Range start (min: 2020) |
| `EndYear` | `int` | Range end (max: StartYear + 10) |

**Location**: `src/Rentier.Domain/Entities/HolidayYearRange.cs`  
**Note**: Not modified by this feature.

## Modified Entity: HolidaySettingsViewModel

**Location**: `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs`

### Existing Properties (unchanged)

| Property | Type | Description |
|----------|------|-------------|
| `StartYear` | `int` | Year range start bound (reactive) |
| `EndYear` | `int` | Year range end bound (reactive) |
| `Entries` | `ObservableCollection<HolidayEntryViewModel>` | Full unfiltered holiday list |
| `HasItems` | `bool` | Computed: `Entries.Count > 0` |
| `SelectedEntry` | `HolidayEntryViewModel?` | Currently selected row |
| `ImportYear` | `int` | Year for web import |
| `IsLoading` | `bool` | Async operation indicator |
| `ErrorMessage` | `string?` | Error display |
| `SuccessMessage` | `string?` | Success display |
| `HasUnsavedChanges` | `bool` | Dirty tracking |

### New Properties (added by this feature)

| Property | Type | Description | Triggers |
|----------|------|-------------|----------|
| `FilteredEntries` | `ObservableCollection<HolidayEntryViewModel>` | Filtered subset of `Entries` where `entry.Date.Year >= StartYear && entry.Date.Year <= EndYear` | Rebuilt when `StartYear`, `EndYear`, `Entries` collection, or any item's `Date` changes |
| `ShowFilterEmptyState` | `bool` | Computed: `Entries.Count > 0 && FilteredEntries.Count == 0` | Recalculated after each filter rebuild |
| `ShowGenericEmptyState` | `bool` | Computed: `Entries.Count == 0` | Recalculated after `Entries` collection changes |
| `HasFilteredItems` | `bool` | Computed: `FilteredEntries.Count > 0` | Recalculated after each filter rebuild |

### Reactive Pipeline

```text
┌─────────────────────────────────────────────────────────────────┐
│                    Trigger Sources                               │
├─────────────────────────────────────────────────────────────────┤
│ WhenAnyValue(StartYear, EndYear)                                │
│ Entries.CollectionChanged                                       │
│ Observable.Merge(entry.WhenAnyValue(x => x.Date) for each item) │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                    Throttle(50ms)
                           │
                           ▼
              ┌────────────────────────┐
              │  RefreshFilteredEntries │
              │                        │
              │  1. Clear FilteredEntries
              │  2. LINQ: Entries.Where(e =>
              │       e.Date.Year >= StartYear
              │       && e.Date.Year <= EndYear)
              │  3. AddRange to FilteredEntries
              │  4. Raise: HasFilteredItems
              │  5. Raise: ShowFilterEmptyState
              │  6. Raise: ShowGenericEmptyState
              └────────────────────────┘
```

### Filtering Rule

```
FilteredEntries = Entries.Where(e => e.Date.Year >= StartYear && e.Date.Year <= EndYear)
```

- Inclusive bounds on both sides: `[StartYear, EndYear]`
- When `StartYear > EndYear`: filter yields zero results → `ShowFilterEmptyState = true`
- When `Entries` is empty: `ShowGenericEmptyState = true`, `ShowFilterEmptyState = false`

## State Transition: Empty State Display

```text
                          ┌─────────────────────────────┐
                          │    Entries.Count == 0?       │
                          └──────┬──────────┬────────────┘
                            Yes  │          │  No
                                 ▼          ▼
                    ┌────────────────┐  ┌──────────────────────┐
                    │ ShowGeneric=T   │  │ FilteredEntries      │
                    │ ShowFilter=F    │  │ .Count == 0?         │
                    │ HasFiltered=F   │  └───┬──────────┬───────┘
                    │                 │   Yes │          │ No
                    │ Display:        │       ▼          ▼
                    │ "No holidays    │  ┌──────────┐  ┌──────────────┐
                    │  configured..." │  │ ShowG=F  │  │ ShowG=F      │
                    └────────────────┘  │ ShowF=T  │  │ ShowF=F      │
                                        │ HasFI=F  │  │ HasFI=T      │
                                        │          │  │              │
                                        │ Display: │  │ Display:     │
                                        │ "No hols │  │ DataGrid     │
                                        │  for     │  │ with rows    │
                                        │  range"  │  └──────────────┘
                                        └──────────┘
```

## New Resource Strings

| Key | Value | Used By |
|-----|-------|---------|
| `Holidays_YearRange_HelperText` | `"Showing holidays for the selected year range. The range also determines which years are pre-seeded on first run."` | Helper text below year selectors |
| `Holidays_FilterEmpty_Message` | `"No holidays configured for this range."` | Range-specific empty state |

**Location**: `src/Rentier.Desktop/Resources/Strings.resx`

## View Layout (Visual Hierarchy)

```text
┌─────────────────────────────────────────────────┐
│ Toolbar: [Add] [Delete Selected] [Save]         │
│          Year: [NumericUpDown] [Import from Web] │
├─────────────────────────────────────────────────┤
│ Year Range: Start Year [____] End Year [____]   │
│ Helper: "Showing holidays for the selected..."  │
├─────────────────── Separator ───────────────────┤
│                                                 │
│   DataGrid (bound to FilteredEntries)           │
│   — OR —                                        │
│   Empty state: generic or filter-specific       │
│                                                 │
└─────────────────────────────────────────────────┘
```

## Validation Rules

No new validation rules. Existing constraints apply:

| Rule | Enforced By | Location |
|------|------------|----------|
| `StartYear >= 2020` | NumericUpDown Min="2020" | View (AXAML) |
| `EndYear <= 2099` | NumericUpDown Max="2099" | View (AXAML) |
| `EndYear <= StartYear + 10` | `HolidayYearRange` domain entity | Domain (on save only) |
| `StartYear > EndYear` → empty view | `RefreshFilteredEntries` LINQ | ViewModel (filter returns empty set) |

**Note**: The View does NOT prevent `StartYear > EndYear` — it simply results in an empty filtered view with the filter-specific empty state message, per spec edge case definition.
