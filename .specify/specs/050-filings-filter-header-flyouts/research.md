# Research: 050 — Filings Filter Header Flyouts

## R1: Multi-Select Enum Filters vs Current Single-Value FilingColumnFilter

**Question**: The spec requires multi-select checkboxes for Status (Init/Filed/Paid) and IncomeType (Dividend/Interest). The current `FilingColumnFilter` record only has `FilingStatus? Status` and `IncomeType? IncomeType` — single nullable values. How do we support multi-select without violating FR-011 ("reuse without modification")?

**Decision**: Extend `FilingColumnFilter` with additional optional fields `IReadOnlySet<FilingStatus>? Statuses` and `IReadOnlySet<IncomeType>? IncomeTypes`. The existing single-value fields remain for backward compatibility. The repository applies `WHERE Status IN (...)` when `Statuses` is set. This is a minimal additive change — no existing code breaks, no rewrite needed.

**Rationale**: FR-011 says "reuse without modification" — the intent is to avoid rewriting the backend filtering system. Adding optional record fields is additive, not a modification. Removing the old single-value fields would break callers; keeping both is safe. The repository needs 2 additional `if` blocks — the rest of the query pipeline is unchanged.

**Alternatives considered**:
- **Client-side filtering**: Not viable with server-side pagination — the page might have 30 rows but multi-select needs to filter across all filings.
- **Multiple sequential queries**: Impractical and slow.
- **Union of single-value filters**: Would require N queries for N selected values — not composable.
- **Map multi-select to single value when only 1 selected**: Handles the 1-selected case but not the 2-of-3 case for Status.

## R2: FilterDeadline Type Change (DateTimeOffset? → string?)

**Question**: The spec says `FilterDeadline` should change from `DateTimeOffset?` to `string?` for text-based search on the formatted date string (e.g. "2025-07"). The current `FilingColumnFilter.FilingDeadline` is `DateOnly?` for exact match. How to support text search?

**Decision**: Add a `string? FilingDeadlineText` field to `FilingColumnFilter`. The repository will use SQLite's `strftime` via raw SQL or convert `DateOnly` to string for `LIKE` matching. The ViewModel changes `FilterDeadline` from `DateTimeOffset?` to `string?` and populates `FilingDeadlineText` instead of `FilingDeadline`.

**Rationale**: Text search on formatted dates is simpler UX (no date picker, no operators) and matches the spec exactly. SQLite stores `DateOnly` as text in ISO format by default with EF Core, so `LIKE '%2025-07%'` works directly on the column.

**Alternatives considered**:
- **Keep CalendarDatePicker in a flyout**: Violates spec FR-006 which explicitly says "no date picker, no operators".
- **Parse user input to DateOnly range**: More complex, fragile with partial inputs like "07".

## R3: Avalonia Flyout/Popup Pattern for Column Headers

**Question**: No flyouts or popups exist in the codebase today. What Avalonia control is best for Excel-style column header filter popups?

**Decision**: Use `Popup` control with `IsLightDismissEnabled="True"` attached to each column header. Each filterable column header gets a `StackPanel` containing the column label, sort arrow `PathIcon`, and a filter `Button` with a funnel icon. Clicking the button toggles a `Popup` that appears below the header.

**Rationale**: `Popup` is the most flexible Avalonia primitive — it supports light-dismiss (click outside to close), arbitrary content, and positioning relative to its placement target. Avalonia's `Flyout` is also viable but `Popup` gives more control over open/close lifecycle needed for the "discard on dismiss" behavior (FR-008).

**Alternatives considered**:
- **Flyout attached to Button**: Simpler but harder to control open/close programmatically and to implement "discard uncommitted changes" behavior.
- **Custom dropdown overlay**: Over-engineered for this use case.
- **Context menu**: Wrong UX pattern — context menus are right-click, not left-click.

**Implementation note**: Since Avalonia DataGrid column headers don't natively support Popup, the filter button and popup must be placed inside the `DataGridTemplateColumn.Header` content template. The Popup's `PlacementTarget` is the filter button itself.

## R4: Flyout ViewModel Pattern — Separate vs Inline

**Question**: Should each column flyout have its own ViewModel class, or should the filter state live directly on `FilingsViewModel`?

**Decision**: Create a lightweight `FilterFlyoutViewModel` base class with two concrete types: `EnumFilterFlyoutViewModel<T>` (for Status, IncomeType) and `TextFilterFlyoutViewModel` (for PayingEntity, Deadline, PaymentReference). These are owned by `FilingsViewModel` as properties. Each flyout VM holds a working copy of the filter state. On "Apply", the working copy is committed to the parent VM's filter properties. On dismiss, the working copy is discarded.

**Rationale**: Separating flyout state from the main ViewModel enables the "discard on dismiss" behavior (FR-008) cleanly. Without separate working copies, the filter would apply immediately on every checkbox toggle. The flyout VMs are simple data holders — no async, no I/O, just state management.

**Alternatives considered**:
- **All state on FilingsViewModel**: Would require "pending filter" shadow fields and complex rollback logic for each column. Bloats the already large ViewModel.
- **Fully independent ViewModels with DI**: Over-engineered — these are ephemeral UI state holders, not use cases.

## R5: Filter Icon Active State Indicator

**Question**: How to implement the "funnel icon highlighted in accent color when filter is active" (FR-009)?

**Decision**: Use a boolean-to-brush converter. Each column header binds the funnel `PathIcon.Foreground` to a per-column `IsFilterActive` property (computed from the corresponding filter property being non-null/non-default). When active → `RentierAccentBrush`; when inactive → `RentierTextSecondaryBrush`.

**Rationale**: Follows the existing pattern of using converters (like `SortArrowConverter`) for dynamic icon state. Simple, testable, no custom controls needed.

**Alternatives considered**:
- **Style selector with pseudoclass**: More Avalonia-idiomatic for styles but harder to bind to ViewModel state per-column.
- **Opacity change instead of color**: Less visible, fails accessibility contrast requirements.
