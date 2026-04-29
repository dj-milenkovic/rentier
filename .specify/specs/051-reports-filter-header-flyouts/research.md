# Research: Reports Filter Header Flyouts

**Feature**: 051-reports-filter-header-flyouts
**Date**: 2025-07-15

## R-001: Avalonia Flyout in DataGrid Column Headers

**Decision**: Use `Flyout` attached to a `Button` inside `DataGridTemplateColumn.Header` template.

**Rationale**: Avalonia's `Flyout` control is the standard popup mechanism. It supports:
- Anchoring to a parent control (the filter icon button in the header)
- Light-dismiss behavior (clicking outside closes without applying)
- `ShowMode` for click-triggered display
- Content templating for arbitrary filter UI inside the flyout

`Popup` is lower-level and requires manual dismiss logic. `Flyout` handles dismiss automatically.

**Alternatives considered**:
- `Popup` control: More control but requires manual light-dismiss, placement, and lifecycle management. Unnecessary complexity.
- `ContextMenu`: Semantically wrong (not a right-click context action) and limited content templating.
- Custom overlay panel: Over-engineered for this use case.

## R-002: DataGridTemplateColumn Header with Custom Content

**Decision**: Convert all remaining `DataGridTextColumn` to `DataGridTemplateColumn` with custom `Header` templates containing both the column label and a filter icon `Button` with attached `Flyout`.

**Rationale**: `DataGridTextColumn.Header` accepts only a string/binding. To embed interactive content (button + flyout), `DataGridTemplateColumn` with a custom `HeaderTemplate` or inline header content is required. The Name, Selection, and Actions columns already use `DataGridTemplateColumn`.

**Alternatives considered**:
- `DataGridTextColumn` with `HeaderTemplate`: Avalonia DataGrid does not support `HeaderTemplate` on text columns the same way WPF does — template columns are the standard approach.
- Attached behaviors on headers: Fragile, hard to test, not idiomatic Avalonia.

## R-003: Flyout Filter Interaction Model (Apply vs Live)

**Decision**: Use explicit "Apply" button model. User opens flyout → edits filter → clicks Apply → flyout closes, filter triggers server query.

**Rationale**: The spec explicitly requires FR-010 (Apply closes flyout and triggers query) and FR-011 (dismiss without Apply does NOT apply changes). This means:
- The flyout content works with **local staging values**, not directly bound to ViewModel filter properties
- On "Apply", copy staged value → ViewModel filter property → triggers LoadPage
- On dismiss (light-dismiss), discard staged value

This approach prevents accidental filter changes and avoids unnecessary server calls.

**Alternatives considered**:
- Live filtering with debounce (as-you-type): Would violate FR-011 (dismiss should not apply) and creates complexity around partial state. Also, 300ms debounce per-keystroke means many server calls while user types in a flyout they might dismiss.

## R-004: Status Column Multi-Select (Checkbox List)

**Decision**: Replace the single-select `ComboBox` (current: `ReportStatus?`) with a multi-select checkbox list inside the flyout. The filter sends the **excluded** statuses or included statuses to the server.

**Rationale**: FR-005 requires checkboxes per enum value + Select All / Clear. The current `ReportColumnFilter.StatusFilter` is `ReportStatus?` (single value). This needs to change to `IReadOnlySet<ReportStatus>?` (set of selected statuses) to support multi-select.

**Implementation approach**:
- Add `IReadOnlySet<ReportStatus>? StatusFilters` (plural) to `ReportColumnFilter`
- Deprecate/remove `StatusFilter` (singular)  
- Repository changes: `WHERE Status IN (...)` instead of `WHERE Status == value`
- VM holds `ObservableCollection<StatusCheckboxItem>` with `IsChecked` per status
- On Apply: collect checked statuses → set on ViewModel → triggers query

**Alternatives considered**:
- Keep single-select with a "None" option: Spec explicitly requires multi-select checkboxes (FR-005).
- Client-side post-filter: Breaks server-side pagination requirement (FR-017).

## R-005: Date Column Text-Contains Filtering

**Decision**: Change date filtering from `DateOnly` exact/comparison to text-contains search on the formatted date string. The UI sends a raw text string; the repository formats dates as "yyyy-MM-dd" and applies `LIKE '%text%'`.

**Rationale**: FR-015 requires text-contains on the formatted date string. The current `ImportDateValue: DateOnly?` with `ImportDateOperator` approach is replaced by `ImportDateContains: string?`.

**Implementation**:
- Add `string? ImportDateContains` and `string? EmailDateContains` to `ReportColumnFilter`
- Remove `ImportDateOperator`, `ImportDateValue`, `EmailDateOperator`, `EmailDateValue`
- In `ReportRepository.GetPagedAsync`: use SQLite `strftime('%Y-%m-%d', ImportDate)` LIKE or EF Core string conversion
- Actually: since EF Core + SQLite stores DateOnly as text in "yyyy-MM-dd" format, a simple string Contains on the column should work directly

**Alternatives considered**:
- Parse user input to DateOnly and use exact match: Spec says text-contains (FR-015), partial matches like "2024-03" must work.
- Client-side date filtering: Breaks pagination (FR-017).

## R-006: Numeric Column (Filing Count) Exact Match

**Decision**: Filing Count filter uses a text input. If the text parses to an `int`, apply equals filter. If it doesn't parse, silently ignore (no filter applied).

**Rationale**: FR-007 and FR-014 specify this behavior. Filing count is a computed value (count of filings per report), currently post-filtered in-memory in the query handler. This approach stays the same — just removing the operator selector.

**Implementation**: Keep `int? FilingCountValue` in `ReportColumnFilter`, remove `FilingCountOperator`. Parse in ViewModel, pass null if invalid.

## R-007: Removing ComparisonOperator from UI

**Decision**: The UI will ONLY use Equals/Contains. Remove all operator ComboBoxes from the filter UI. The `ComparisonOperator` enum stays in the codebase for backward compatibility but the UI no longer exposes it.

**Rationale**: User context specifies "UI should ONLY use Equals operator — remove operator selectors from UI". The existing `ComparisonOperator` fields in `ReportColumnFilter` that are no longer needed (dates switch to text-contains, filing count always equals) can be removed or defaulted.

**Implementation**:
- Remove `ImportDateOperator`, `EmailDateOperator`, `FilingCountOperator` from `ReportColumnFilter` 
- Remove corresponding properties from `ReportsViewModel`
- Remove `ComparisonOperatorIndexConverter` usage from Reports view
- Keep `ComparisonOperator` enum in Application layer (may be used elsewhere)
