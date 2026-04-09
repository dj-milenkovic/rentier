# Application Contracts: Holidays Settings — Year-Range Filter & UX Improvements

**Feature**: 024-holidays-settings-year-range-filter-ux  
**Date**: 2025-07-15

## Overview

This feature is purely a Desktop-layer enhancement. No new Application or Domain interfaces are introduced. No existing contracts are modified. This document defines the **ViewModel ↔ View contract** — the public API surface that the View (AXAML) binds to.

## ViewModel → View Contract: HolidaySettingsViewModel

### Existing Bindings (unchanged)

| Binding Target | Property | Type | Direction | Notes |
|---------------|----------|------|-----------|-------|
| Toolbar buttons | `AddRowCommand` | `ReactiveCommand` | OneWay | Add blank entry |
| Toolbar buttons | `DeleteRowCommand` | `ReactiveCommand<HolidayEntryViewModel>` | OneWay | Remove selected |
| Toolbar buttons | `SaveCommand` | `ReactiveCommand` | OneWay | Persist all entries |
| Import controls | `ImportCommand` | `ReactiveCommand<int>` | OneWay | Import from web |
| Import controls | `ImportYear` | `int` | TwoWay | Year for import |
| Year range | `StartYear` | `int` | TwoWay | Filter lower bound |
| Year range | `EndYear` | `int` | TwoWay | Filter upper bound |
| DataGrid | `SelectedEntry` | `HolidayEntryViewModel?` | TwoWay | Row selection |
| Status area | `IsLoading` | `bool` | OneWay | Loading indicator |
| Status area | `ErrorMessage` | `string?` | OneWay | Error display |
| Status area | `SuccessMessage` | `string?` | OneWay | Success display |

### New Bindings (added by this feature)

| Binding Target | Property | Type | Direction | Spec Requirement |
|---------------|----------|------|-----------|-----------------|
| DataGrid `ItemsSource` | `FilteredEntries` | `ObservableCollection<HolidayEntryViewModel>` | OneWay | FR-001, FR-002, FR-003, FR-004 |
| Generic empty state `IsVisible` | `ShowGenericEmptyState` | `bool` | OneWay | FR-006 |
| Filter empty state `IsVisible` | `ShowFilterEmptyState` | `bool` | OneWay | FR-005 |
| DataGrid `IsVisible` | `HasFilteredItems` | `bool` | OneWay | FR-001 |

### Binding Change: DataGrid ItemsSource

**Before** (current):
```xml
<DataGrid ItemsSource="{Binding Entries}" ... />
```

**After** (this feature):
```xml
<DataGrid ItemsSource="{Binding FilteredEntries}" ... />
```

This is the **only existing binding that changes**. All other existing bindings remain identical.

### View-Only Additions (no ViewModel binding)

| Element | Type | Source | Spec Requirement |
|---------|------|--------|-----------------|
| Helper text | `TextBlock` | `{x:Static res:Strings.Holidays_YearRange_HelperText}` | FR-007, FR-008 |
| Filter empty text | `TextBlock` | `{x:Static res:Strings.Holidays_FilterEmpty_Message}` | FR-005, FR-008 |
| Visual separator | `Separator` | Static AXAML element | FR-009 |

## Application Layer Contracts (unchanged)

No Application layer changes. For reference, the existing contracts consumed by this ViewModel:

| Contract | Type | Used For |
|----------|------|----------|
| `GetHolidayConfQuery` → `HolidayConfDto` | Query | Loading holidays + year range on activation |
| `SaveHolidayConfCommand` | Command | Persisting `Entries` (full unfiltered list) + year range |
| `ImportHolidaysFromWebCommand` → `IReadOnlyList<HolidayEntryDto>` | Command | Importing holidays for a year |

**Critical invariant**: The save workflow reads from `Entries` (unfiltered), NOT from `FilteredEntries`. This ensures all holidays are persisted regardless of the current filter state.

## Domain Contracts (unchanged)

No Domain layer changes. Existing constraints enforced on save:

| Constraint | Entity | Enforcement |
|-----------|--------|-------------|
| `StartYear >= 2020` | `HolidayYearRange` | Constructor validation |
| `EndYear >= StartYear` | `HolidayYearRange` | Constructor validation |
| `EndYear <= StartYear + 10` | `HolidayYearRange` | Constructor validation |
| Holiday `Name` not empty | `PublicHoliday` | Factory method validation |
| Holiday `Date` is `DateOnly` | `PublicHoliday` | Type system |
