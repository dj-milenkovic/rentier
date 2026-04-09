# Research: Holidays Settings — Year-Range Filter & UX Improvements

**Feature**: 024-holidays-settings-year-range-filter-ux  
**Date**: 2025-07-15

## Research Questions

### RQ-1: Reactive Filtering Pattern for ObservableCollection in ReactiveUI

**Context**: The ViewModel has an `ObservableCollection<HolidayEntryViewModel> Entries` (unfiltered source) and needs to derive a filtered display collection that reacts to changes in `StartYear`, `EndYear`, the collection itself (add/remove), and individual item `Date` property changes.

**Decision**: Use a manually-maintained `ObservableCollection<HolidayEntryViewModel>` named `FilteredEntries` that is rebuilt via a `RefreshFilteredEntries()` method triggered by reactive subscriptions.

**Rationale**:
- The collection is small (tens to low hundreds of items) — full rebuild on each trigger is negligible in cost and simpler to reason about than incremental updates.
- The existing codebase does not use DynamicData's `SourceList<T>` pattern anywhere. Introducing DynamicData would add a new dependency pattern for a trivial use case.
- ReactiveUI's `WhenAnyValue` on `StartYear` and `EndYear`, combined with `Entries.CollectionChanged`, provides all necessary triggers.
- Individual item `Date` changes require subscribing to each entry's `WhenAnyValue(x => x.Date)` — this is straightforward with a merged observable using `Entries.CollectionChanged` to resubscribe when items are added/removed.

**Alternatives considered**:
1. **DynamicData `SourceList<T>` + `.Filter()`**: More elegant for large or complex collections, but overkill here. Would require refactoring `Entries` from `ObservableCollection` to `SourceList`, impacting the existing save workflow and import logic. Rejected for unnecessary complexity.
2. **`ICollectionView` / `DataGridCollectionView`**: Avalonia does not provide a built-in `ICollectionView` equivalent like WPF. Rejected — not available in the tech stack.
3. **LINQ query in a getter (no caching)**: Would require the View to re-query on every property change notification, but `ObservableCollection` doesn't notify on content changes. Rejected — breaks reactivity for item edits.

### RQ-2: Reactive Subscription for Individual Item Property Changes

**Context**: When a user edits a holiday entry's date to move it outside the current filter range, the filtered view must update (FR-012). This requires observing `Date` property changes on individual `HolidayEntryViewModel` items.

**Decision**: Use a merged observable that combines:
1. `Entries.CollectionChanged` — triggers resubscribe to item observables + rebuild.
2. `this.WhenAnyValue(x => x.StartYear, x => x.EndYear)` — triggers rebuild on range change.
3. A per-item `WhenAnyValue(x => x.Date)` merged via `Observable.Merge` — triggers rebuild on individual date edits.

The merged observable is throttled with a short debounce (e.g., 50ms) to coalesce rapid changes (e.g., bulk import replacing many entries).

**Rationale**:
- Covers all three trigger sources: range changes, collection mutations, and item property edits.
- Throttle prevents unnecessary intermediate rebuilds during batch operations (import replaces all entries).
- Per-item subscriptions are disposed and recreated on `CollectionChanged` to avoid leaks.

**Alternatives considered**:
1. **No per-item observation**: Would miss FR-012 (date edits). Rejected — explicit spec requirement.
2. **Global timer-based polling**: Fragile, wastes CPU, doesn't guarantee immediate response. Rejected.

### RQ-3: Empty-State Display Logic — Two Distinct States

**Context**: The spec requires two different empty-state messages:
- Generic: "No holidays configured. Click Add or Import to get started." (when `Entries` is empty)
- Range-specific: "No holidays configured for this range." (when `Entries` is non-empty but `FilteredEntries` is empty)

**Decision**: Add two computed boolean properties to the ViewModel:
- `ShowGenericEmptyState` → `Entries.Count == 0`
- `ShowFilterEmptyState` → `Entries.Count > 0 && FilteredEntries.Count == 0`

The View binds each empty-state TextBlock's `IsVisible` to the corresponding property. The DataGrid is visible when `FilteredEntries.Count > 0`.

**Rationale**:
- Clear separation of concerns — the ViewModel exposes the display state; the View just binds.
- The existing `HasItems` property (`Entries.Count > 0`) is already used for the generic empty state. We keep it and add `ShowFilterEmptyState` alongside.
- Mutually exclusive by construction: if `Entries` is empty, `FilteredEntries` is also empty, so `ShowFilterEmptyState` is false.

**Alternatives considered**:
1. **Single enum `EmptyStateKind`**: Cleaner for >2 states, but overengineered for exactly two. Rejected.
2. **Multi-binding in AXAML**: Would put logic in the View layer, violating the constitution's coding standards ("no view logic in code-behind" / clean ViewModel separation). Rejected.

### RQ-4: Resource String Strategy for New UI Text

**Context**: FR-008 requires the helper text and range-specific empty-state message to be stored as localized resource strings in `Strings.resx`.

**Decision**: Add two new entries to `src/Rentier.Desktop/Resources/Strings.resx`:
- `Holidays_YearRange_HelperText` = `"Showing holidays for the selected year range. The range also determines which years are pre-seeded on first run."`
- `Holidays_FilterEmpty_Message` = `"No holidays configured for this range."`

Reference in AXAML via `{x:Static res:Strings.Holidays_YearRange_HelperText}` and `{x:Static res:Strings.Holidays_FilterEmpty_Message}`.

**Rationale**:
- Consistent with existing pattern (other strings like `Holidays_AddRow_Button` already in Strings.resx).
- `x:Static` binding is the standard Avalonia pattern used throughout the codebase.
- Helper text wording matches the agreed-upon text from spec assumptions.

**Alternatives considered**:
1. **Hardcoded strings in AXAML**: Violates constitution coding standard ("User-visible strings MUST be in Resources/Strings.resx"). Rejected.
2. **ViewModel string properties**: Unnecessary indirection — resource strings accessed via `x:Static` are the established pattern. Rejected.

### RQ-5: Visual Separator Implementation

**Context**: FR-009 requires a visual separator between the year-range controls area and the holidays data grid.

**Decision**: Use an Avalonia `Separator` control (or a styled `Border` with bottom border) placed between the year-range StackPanel and the DataGrid in the DockPanel layout.

**Rationale**:
- Avalonia's `Separator` control renders a horizontal line by default with FluentTheme styling.
- Consistent with FluentTheme's visual language.
- Minimal AXAML change — single element insertion.

**Alternatives considered**:
1. **Increased Margin/Padding only**: Provides spacing but not a visible boundary. QA specifically requested a visual separator. Rejected.
2. **`Border` wrapper around controls section**: Heavier-weight, would require restructuring the DockPanel children. Rejected for unnecessary complexity.

### RQ-6: Save Workflow Impact Analysis

**Context**: Must ensure the save workflow persists ALL entries (unfiltered), not just the filtered subset.

**Decision**: No change to the save workflow. The existing `SaveCommand` reads from `Entries` (the full unfiltered collection), not from the display binding. `FilteredEntries` is a read-only derived view used only for display. The save handler (`SaveHolidayConfCommand`) receives `Entries.Select(e => e.ToDto())` — this continues to work correctly.

**Rationale**: The filtering is purely a display concern. `Entries` remains the authoritative collection for all CRUD operations (add, delete, save, import). `FilteredEntries` is never written to, only read for display.

**Alternatives considered**: None — the current architecture naturally isolates the display filter from the persistence workflow.
