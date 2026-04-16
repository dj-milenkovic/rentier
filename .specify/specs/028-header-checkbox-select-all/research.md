# Research: Header Checkbox for Select All / Clear All

**Feature**: 028-header-checkbox-select-all
**Date**: 2025-07-17

## Research Topics

### R-001: Avalonia DataGrid Header Template Binding

**Context**: The header checkbox in a `DataGridTemplateColumn` needs to bind to the parent ViewModel, not the row item.

**Decision**: Use `DataGridTemplateColumn.Header` with a `CheckBox` bound via `RelativeSource={RelativeSource AncestorType=DataGrid}` to reach the ViewModel.

**Rationale**: Avalonia DataGrid column headers have the column itself as DataContext, not the ViewModel. The existing codebase already uses this pattern for action buttons in `ReportsView.axaml` (line 116):
```xml
Command="{Binding DataContext.ViewFilingsCommand,
          RelativeSource={RelativeSource AncestorType=DataGrid}}"
```
This is a proven, working pattern in the project.

**Alternatives Considered**:
- Named element binding (`{Binding #DataGridName.DataContext.Property}`) — rejected because both DataGrids lack `x:Name` and this approach is less consistent with existing patterns.
- Attached property on column — rejected as over-engineered for this use case.

---

### R-002: Tri-State Checkbox in Avalonia

**Context**: The header checkbox must show three visual states: unchecked (none selected), indeterminate (some selected), fully checked (all selected).

**Decision**: Use native Avalonia `CheckBox` with `IsThreeState="True"` and bind `IsChecked` to a `bool?` property on the ViewModel.

**Rationale**: Avalonia's `CheckBox` natively supports `IsThreeState` and `IsChecked` as `bool?`:
- `null` → indeterminate (dash/filled-square indicator)
- `true` → all selected
- `false` → none selected

This matches the platform-native rendering assumption in the spec and requires no custom control template.

**Alternatives Considered**:
- Custom `ToggleButton` with manual visual states — rejected; unnecessary complexity when native support exists.
- Two-state checkbox with overlay icon for indeterminate — rejected; non-standard and brittle.

---

### R-003: IsAllSelected Property Design

**Context**: Need a single `bool?` property that both computes tri-state from row selection AND dispatches select/deselect actions when set.

**Decision**: Add an `IsAllSelected` property with:
- **Getter**: Computed from `SelectedCount` and `Rows.Count`:
  - `Rows.Count == 0` → `false` (disabled state)
  - `SelectedCount == 0` → `false`
  - `SelectedCount == Rows.Count` → `true`
  - Otherwise → `null` (indeterminate)
- **Setter**: When user clicks the header checkbox:
  - `true` or `null→true` transition → execute `SelectAllCommand`
  - `false` → execute `ClearSelectionCommand`
  - Ignore `null` from setter (Avalonia sends null during tri-state cycling; we skip it and let the reactive pipeline recompute)

**Rationale**: Reuses existing `SelectAllCommand` and `ClearSelectionCommand` as required by FR-014. The reactive subscription on `SelectedCount` already fires whenever any row changes, making the getter reactive automatically.

**Alternatives Considered**:
- Separate `HeaderCheckboxState` enum — rejected; `bool?` is the native Avalonia tri-state model and avoids an extra converter.
- Computing in the View via a multi-binding converter — rejected; violates MVVM and makes testing harder.

---

### R-004: Preventing Re-entrant Updates

**Context**: Setting `IsAllSelected` triggers `SelectAllCommand` which changes each row's `IsSelected`, which triggers `RebuildRowSubscriptions`' lambda to recalculate `SelectedCount`, which would re-raise `IsAllSelected` changed notification. This could cause a feedback loop.

**Decision**: Use a `_isUpdatingSelection` guard flag in the setter. When the setter executes commands, set the flag to `true`. In the reactive pipeline that recomputes `IsAllSelected`, skip notification if the flag is active. Clear the flag after command execution completes.

**Rationale**: This is a standard re-entrancy guard pattern used throughout ReactiveUI codebases. The alternative (throttling/debouncing) introduces timing dependencies that are harder to test.

**Alternatives Considered**:
- `Observable.Throttle()` on SelectedCount — rejected; introduces non-deterministic timing.
- Disconnecting subscriptions during bulk set — rejected; more complex and error-prone than a simple boolean guard.

---

### R-005: Empty State Handling

**Context**: FR-015 requires the header checkbox to be unchecked and non-interactive when no rows exist.

**Decision**: Bind `IsEnabled` on the header `CheckBox` to `HasItems` via the same `RelativeSource` pattern. When `HasItems` is `false`, the checkbox is disabled (unchecked, non-clickable).

**Rationale**: `HasItems` already exists on both ViewModels and is reactive (recomputed when `Rows` changes). This is the simplest binding approach.

**Alternatives Considered**:
- `IsVisible="False"` when empty — rejected; spec says "unchecked and non-interactive," not hidden. A disabled checkbox communicates the column's purpose even when empty.
- Separate `IsHeaderCheckboxEnabled` property — rejected; `HasItems` already serves this exact purpose.

---

### R-006: DataGrid Header DataContext in Avalonia

**Context**: Need to confirm the binding path for column headers in Avalonia DataGrid to ensure the tri-state checkbox can reach the ViewModel.

**Decision**: Column headers in Avalonia DataGrid inherit the DataGrid's DataContext when using `DataGridTemplateColumn.Header` with inline content (not a DataTemplate). Using `RelativeSource AncestorType=DataGrid` from within a header content template will correctly resolve to the ViewModel.

**Rationale**: Verified by the existing working pattern in `ReportsView.axaml` where cell templates use `RelativeSource={RelativeSource AncestorType=DataGrid}` to access ViewModel commands. The header uses the same visual tree ancestry.

**Alternatives Considered**: None — this is the established project pattern.

---

### R-007: Recalculation After Bulk Delete

**Context**: FR-016 requires recalculation when rows are added/removed (e.g., after bulk delete).

**Decision**: No new code needed. The existing `LoadPageAsync` / `LoadReportsAsync` methods already clear and repopulate `Rows`, then call `RebuildRowSubscriptions()` which resets `SelectedCount` to 0. This automatically triggers `IsAllSelected` to recompute via the reactive pipeline. The `RaisePropertyChanged(nameof(HasItems))` call that follows ensures the header checkbox enabled state updates too.

**Rationale**: The existing reload flow already handles this edge case. The only requirement is that `IsAllSelected` is included in the reactive property change chain, which happens naturally since it derives from `SelectedCount`.

**Alternatives Considered**: None — existing infrastructure is sufficient.
