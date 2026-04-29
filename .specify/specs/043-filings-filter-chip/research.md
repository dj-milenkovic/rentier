# Research: Filings Per-Report Filter Chip

**Feature**: 043-filings-filter-chip  
**Date**: 2025-07-22

## Research Tasks

### RT-1: Existing Reactive Pipeline for ReportIdFilter

**Decision**: Reuse the existing reactive subscription on `ReportIdFilter` that already triggers `LoadPageCommand`.

**Rationale**: `FilingsViewModel` (lines 393-397) already observes `ReportIdFilter` changes via `this.WhenAnyValue(x => x.ReportIdFilter)` and invokes `LoadPageCommand`. Setting `ReportIdFilter = null` already reloads all filings. No new command handler or query is needed.

**Alternatives considered**:
- Creating a dedicated `ClearReportFilterCommand` handler in Application layer → Rejected: overkill, the filter is purely UI state; setting it to `null` is sufficient and already wired.

### RT-2: Chip UI Pattern in Avalonia

**Decision**: Use a `Border` with `CornerRadius` containing a `TextBlock` + `Button` (✕), matching the existing status badge pattern.

**Rationale**: The codebase already uses `Border` with `CornerRadius="10"` and `Padding="8,2"` for pill-shaped status badges in `FilingsView.axaml` (lines 120-134). Reusing this pattern ensures visual consistency. The chip will add an interactive `Button` for dismissal.

**Alternatives considered**:
- Custom `TemplatedControl` for chips → Rejected: unnecessary abstraction for a single instance; a composed `Border` + `Button` is simpler and matches existing patterns.
- Third-party chip control library → Rejected: adds dependency; the pattern is trivial to implement with native Avalonia controls.

### RT-3: ViewModel Property Design for Chip Visibility

**Decision**: Add a read-only `HasReportFilter` property (computed from `ReportIdFilter != null`) and a `ClearReportFilterCommand` reactive command.

**Rationale**: `HasReportFilter` is a simple derived boolean, ideal for `IsVisible` binding. A dedicated `ReactiveCommand` for clearing the filter follows the existing pattern (all user actions route through commands) and is testable. The command body simply sets `ReportIdFilter = null`, which the existing pipeline handles.

**Alternatives considered**:
- Binding `IsVisible` directly to `ReportIdFilter` with a null-to-bool converter → Rejected: less explicit, harder to test, and doesn't follow the command pattern for the dismiss action.
- Using `[ObservableProperty]` source generator for `HasReportFilter` → Rejected: it's a derived property, not independent state; `ObservableAsPropertyHelper<bool>` from ReactiveUI is the correct pattern.

### RT-4: Localization Strategy

**Decision**: Add a `Filings_FilterChip_Report` entry to `Strings.resx` with value "Filtered by report".

**Rationale**: Follows the existing naming convention (`Filings_Filter_Unpaid`, `Filings_Filter_All`). The ✕ button uses a Unicode character or `PathIcon` and needs an `AutomationProperties.Name` entry (`Filings_FilterChip_Dismiss`) for accessibility.

**Alternatives considered**:
- Including the report name in the chip text → Rejected per spec assumption: report names can be long; the user already knows which report they navigated from.

### RT-5: Accessibility Requirements

**Decision**: The ✕ dismiss button will be a standard Avalonia `Button` with `AutomationProperties.Name` set to a localized "Remove report filter" string.

**Rationale**: Standard `Button` inherits keyboard focusability (Tab navigation) and activation (Enter/Space). This satisfies FR-009 with zero custom accessibility code.

**Alternatives considered**:
- Custom accessible control → Rejected: standard Button already provides full keyboard and screen reader support.
