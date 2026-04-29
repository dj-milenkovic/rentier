# Research: Filings Visual Sorting

## R-001: Avalonia DataGrid Custom Sort Indicators in Column Headers

**Decision**: Use `DataGridTemplateColumn` with custom header templates containing a `TextBlock` for the column label and a `PathIcon` for the sort arrow, bound to ViewModel state via the DataGrid's `DataContext`.

**Rationale**: Avalonia's built-in DataGrid sort indicators only support client-side sorting (they set `DataGridColumn.SortDirection`). Since Rentier uses server-side (query-level) sorting via `ApplySortCommand`, the built-in indicators fight the custom sort logic. Custom header templates give full control over arrow visibility and direction without conflicting with Avalonia's internal sort state. The `Sorting` event already sets `e.Handled = true` to suppress built-in sort, so custom indicators are the natural complement.

**Alternatives considered**:
- **Built-in `SortDirection` property on columns**: Rejected — would require setting `SortDirection` from code-behind after each sort command execution, creating a fragile sync between ViewModel state and DataGrid column state. Also, built-in arrows use Avalonia's internal sort mechanism which conflicts with server-side sort.
- **Attached properties on DataGridColumn**: Rejected — DataGridColumns are not visual elements in Avalonia and don't support attached properties cleanly.
- **Value converter approach**: Rejected — column headers in `DataGridTextColumn` don't easily support complex templates; switching to `DataGridTemplateColumn` for sortable columns would lose the convenient `Binding` shorthand.

## R-002: Sort Cycle Behavior (Unsorted → Ascending → Descending → Unsorted)

**Decision**: Modify `ApplySortCommand` to implement a three-state cycle: unsorted → ascending → descending → unsorted. Introduce a nullable `FilingSortColumn?` for `SortColumn` (or a sentinel value) to represent the "unsorted" state.

**Rationale**: The spec (FR-002) requires a three-state cycle. The current implementation only toggles between ascending and descending for the same column. The unsorted state means the query uses default database ordering (no explicit ORDER BY), which aligns with "no active sort."

**Alternatives considered**:
- **Keep two-state toggle (asc/desc only)**: Rejected — violates FR-002 acceptance scenario 3.
- **Use `FilingSortColumn.None` enum value**: Considered but rejected — adding to the enum affects the Application layer unnecessarily. A nullable `FilingSortColumn?` in the ViewModel is Desktop-layer only.

## R-003: Best Approach for Sort Arrow Icons

**Decision**: Use inline `PathIcon` with `StreamGeometry` resources for up-arrow (▲/chevron-up) and down-arrow (▼/chevron-down), consistent with existing icon patterns in the codebase (see `AdvanceStatusIcon` and `ExportPpOpoXmlIcon` in FilingsView.axaml).

**Rationale**: The project already uses `StreamGeometry` for inline SVG-style icons (Lucide MIT). This avoids adding image assets and keeps icons resolution-independent and theme-aware.

**Alternatives considered**:
- **Unicode characters (↑/↓)**: Rejected — font-dependent rendering, inconsistent sizing across platforms.
- **Image assets (PNG/SVG files)**: Rejected — overkill for simple arrows, not consistent with existing icon approach.

## R-004: Removing Filter Toggles — Impact on ShowAll Property and Query Pipeline

**Decision**: Remove the `RadioButton` controls and `SortIndicatorDisplay` TextBlock from the AXAML. Default `_showAll` to `true` in the ViewModel. Keep the `ShowAll` property for now (it's used in `LoadPageAsync` to construct the `FilingFilterMode`), but it will always be `true` since no UI control sets it to `false`. The reactive `WhenAnyValue(x => x.ShowAll)` subscription can remain harmless.

**Rationale**: Minimal change approach — removing UI controls and changing the default achieves FR-007/FR-008 without restructuring the query pipeline. The `ShowAll` property and `FilingFilterMode` can be fully removed in a future cleanup task when inline column filters (feature 045) are complete.

**Alternatives considered**:
- **Remove ShowAll property entirely**: Rejected — it's referenced in tests and the reactive activation pipeline. Removing it is a larger refactor best done when inline filters replace it.
- **Remove FilingFilterMode from query**: Rejected — same reason; the Application layer query parameter remains, just always receives `All`.

## R-005: Sortable vs Non-Sortable Column Visual Distinction (P3)

**Decision**: Non-sortable columns already have `CanUserSort="False"` (checkbox, status badge, payment reference, actions). Sortable columns get custom header templates with arrow affordance. The visual distinction is inherent: sortable columns show arrows, non-sortable columns show plain text headers. Add a `cursor: Hand` style for sortable column headers on hover.

**Rationale**: The existing `CanUserSort="False"` already prevents the `Sorting` event from firing on those columns. The arrow presence/absence creates a natural visual distinction. Cursor change on hover reinforces discoverability.

**Alternatives considered**:
- **Dim or grey out non-sortable headers**: Rejected — reduces readability for no benefit.
- **Add tooltip "Click to sort" on sortable columns**: Could be added later as polish; not required by spec.
