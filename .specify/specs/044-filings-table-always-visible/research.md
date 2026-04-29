# Research: 044-filings-table-always-visible

**Date**: 2025-07-15

## R-001: Avalonia DataGrid Empty State Pattern

**Decision**: Keep the DataGrid always visible by removing `IsVisible="{Binding HasItems}"` and adding a subtle overlay/below-table message for the empty state.

**Rationale**: Avalonia's `DataGrid` renders column headers independently of row data. Simply removing the `IsVisible` binding ensures headers display even with zero `ItemsSource` items. This is the simplest, most maintainable approach — no custom templates or control overrides needed.

**Alternatives considered**:
- Custom `DataGrid` control template with empty-state content presenter — rejected as over-engineered for this change.
- Using `DataGrid.IsVisible="True"` with a separate `ItemsControl` overlay — unnecessarily complex; the DataGrid already handles empty `ItemsSource` gracefully.

## R-002: Empty-State Message Placement

**Decision**: Place a `TextBlock` below the DataGrid (still inside the `DockPanel`) that is visible only when `IsEmpty` is true. Remove the existing full-page empty-state `TextBlock` that sits above the DataGrid and replaces it visually.

**Rationale**: Placing the message below (or as an overlay inside) the grid area ensures column headers remain visible and the message doesn't compete with the table structure. The existing `Filings_Empty` localization key can be reused with updated text to say "No filings yet" rather than replacing the table entirely.

**Alternatives considered**:
- Overlay panel centered inside the DataGrid — adds z-ordering complexity; a simple TextBlock below the grid is cleaner.
- No message at all — rejected; users need feedback that the empty state is intentional.

## R-003: ViewModel Property Impact

**Decision**: The `IsEmpty` property stays as-is for the empty-state message. The `HasItems` property remains for the select-all checkbox `IsEnabled` binding. Neither property is removed — only the AXAML bindings change (DataGrid no longer bound to `HasItems` for visibility).

**Rationale**: Minimal ViewModel changes reduce risk. The `HasItems` and `IsEmpty` properties are still meaningful for other UI behavior (select-all checkbox, pagination). Only the view layer binding changes.

**Alternatives considered**:
- Removing `HasItems`/`IsEmpty` entirely — rejected; they're used by other UI elements (checkbox `IsEnabled`, pagination logic).
