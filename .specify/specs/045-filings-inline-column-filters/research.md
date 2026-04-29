# Research: Filings Inline Column Filters

**Feature**: 045-filings-inline-column-filters  
**Date**: 2025-07-15

## R-001: Server-Side vs. Client-Side Filtering

**Decision**: Server-side (database) filtering via extended `GetFilingsQuery` parameters.

**Rationale**: The Filings page already uses server-side pagination (`GetPagedAsync` with skip/take). Applying filters client-side would only filter the current page (30 items), not the full dataset. The existing `FilingFilterMode` (Unpaid/All) already demonstrates server-side filtering in the repository. Extending this pattern with additional column-level filter parameters keeps behavior consistent and correct across paginated results.

**Alternatives considered**:
- **Client-side filtering**: Rejected — only filters visible page, breaks pagination contract, would require loading all filings into memory.
- **Hybrid (load all, filter in-memory)**: Rejected — violates pagination purpose, memory concerns with large filing sets.

## R-002: Avalonia DataGrid Filter Row Implementation

**Decision**: Custom `DataGridTemplateColumn` headers or a dedicated filter row panel positioned between the header row and data rows using the DataGrid's `ColumnHeaderTemplate` or a separate control outside the DataGrid.

**Rationale**: Avalonia's `DataGrid` does not have built-in column filter support like WPF third-party grids. The cleanest approach is to place a styled row of filter controls (ComboBoxes, TextBoxes, DatePicker) in a `Grid` panel directly below the DataGrid header. This avoids fighting DataGrid internals while maintaining visual alignment with columns. The filter controls bind directly to ViewModel properties.

**Alternatives considered**:
- **DataGrid ColumnHeaderTemplate override**: Rejected — overriding column headers to include filter controls creates complex templates and breaks standard header sorting behavior.
- **Third-party DataGrid with filtering**: Rejected — adds external dependency, constitution prefers minimal NuGet dependencies.
- **Frozen row inside DataGrid**: Rejected — Avalonia DataGrid does not support frozen/pinned non-data rows.

## R-003: Text Filter Debounce Pattern

**Decision**: Use `WhenAnyValue` with `.Throttle(TimeSpan.FromMilliseconds(300))` from `System.Reactive.Linq` on text filter properties before triggering page reload.

**Rationale**: ReactiveUI's `WhenAnyValue` combined with Rx `Throttle` is the idiomatic pattern for debounced input in ReactiveUI applications. 300ms provides responsive feel without excessive queries. No third-party library needed — `System.Reactive` is already a transitive dependency of ReactiveUI.

**Alternatives considered**:
- **Manual timer-based debounce**: Rejected — reinvents Rx functionality already available.
- **No debounce (immediate on keystroke)**: Rejected — spec FR-008 explicitly requires debounce; rapid keystrokes would cause excessive DB queries.

## R-004: Filter State and ReportIdFilter Coexistence

**Decision**: When `ReportIdFilter` is active (navigation from Reports page), inline column filters are cleared and disabled. When user clears `ReportIdFilter`, inline filters become available. The two filter mechanisms are mutually exclusive.

**Rationale**: `ReportIdFilter` bypasses pagination entirely (returns all filings for a report). Inline filters are designed for paginated browsing. Mixing them creates ambiguous UX — if a user navigates from Reports to see 3 filings but has a Status filter active, they might see 0 results. The spec FR-012 requires target filings to always be visible. Mutual exclusivity is the simplest guarantee.

**Alternatives considered**:
- **Clear only conflicting inline filters**: Rejected — complex logic to determine which filters conflict, and partial clearing is confusing UX.
- **Apply inline filters on top of ReportIdFilter**: Rejected — ReportIdFilter returns a small set; further filtering is unnecessary and could hide the target filing.

## R-005: DateOnly Filter for Filing Deadline

**Decision**: Use Avalonia `CalendarDatePicker` control (compact date picker) bound to a nullable `DateTimeOffset?` property, converted to `DateOnly?` for the query. Exact date match semantics.

**Rationale**: Avalonia's `CalendarDatePicker` provides a compact date selection control suitable for a filter row. It uses `DateTimeOffset?` as its binding type, requiring conversion at the ViewModel boundary to `DateOnly?` for the query layer (consistent with constitution Principle III). The spec explicitly states exact date match for initial implementation.

**Alternatives considered**:
- **TextBox with date parsing**: Rejected — poor UX, locale-sensitive parsing issues.
- **Date range filter**: Rejected — spec explicitly defers range filtering to future iteration.

## R-006: Empty State When Filters Produce Zero Results

**Decision**: Display an overlay message "No filings match the active filters" when the filtered result count is zero and at least one inline filter is active. Reuse the existing empty-state pattern from the Filings page.

**Rationale**: Spec FR-015 requires this. The message should differentiate "no filings exist" from "filters are hiding filings" to avoid user confusion.

**Alternatives considered**:
- **No special message**: Rejected — spec requires it, and users may not realize filters are active.
- **Auto-clear filters on empty result**: Rejected — surprising behavior, user may be intentionally narrowing search.
