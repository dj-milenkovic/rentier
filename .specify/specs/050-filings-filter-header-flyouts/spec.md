# Feature Specification: Filings Filter Header Flyouts

**Feature Branch**: `050-filings-filter-flyouts`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Rework filings inline filter row (feature 045) — replace with Excel-style column header filter popups/flyouts"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Filter Filings by Status via Header Flyout (Priority: P1)

A user viewing the filings list wants to narrow results to only filings in specific statuses (e.g., only "Init" and "Filed"). The user clicks the funnel icon in the Status column header. A flyout appears with checkboxes for each status value (Init, Filed, Paid), plus "Select All" and "Clear" options. The user checks the desired statuses and clicks "Apply". The table reloads showing only matching filings.

**Why this priority**: Filtering by status is the most common filtering action — users routinely need to see only unfiled or unpaid filings. This story also establishes the core flyout interaction pattern that all other column filters will reuse.

**Independent Test**: Can be fully tested by opening the Status flyout, selecting one or more statuses, applying, and verifying the table shows only matching filings. Delivers immediate filtering value even without other column filters.

**Acceptance Scenarios**:

1. **Given** the filings list is displayed with filings in mixed statuses, **When** the user clicks the funnel icon in the Status column header, **Then** a flyout opens with a checkbox for each status value (Init, Filed, Paid), all checked by default, plus "Select All" and "Clear" options and an "Apply" button.
2. **Given** the Status flyout is open, **When** the user unchecks "Paid" and clicks "Apply", **Then** the flyout closes and the table reloads showing only filings with Init or Filed status.
3. **Given** no filter is active on Status, **When** the user looks at the Status column header, **Then** the funnel icon is shown in its default/inactive color.
4. **Given** a Status filter is active (not all values selected), **When** the user looks at the Status column header, **Then** the funnel icon is highlighted in the accent color to indicate an active filter.

---

### User Story 2 — Filter Filings by Text Columns via Header Flyout (Priority: P1)

A user wants to find filings from a specific payer or with a specific payment reference. The user clicks the funnel icon in the "Isplatilac" (Paying Entity) or "Poziv na broj" (Payment Reference) column header. A flyout opens with a text search box (placeholder "Pretraži..."). The user types a search term and clicks "Apply". The table reloads showing filings where that column contains the search text.

**Why this priority**: Text search on payer name and payment reference are essential for locating specific filings in a large list. Same priority as P1 because it uses a simpler flyout variant (text box only).

**Independent Test**: Can be tested by opening the PayingEntity flyout, typing a partial name, applying, and verifying the table shows only matching rows.

**Acceptance Scenarios**:

1. **Given** the filings list is displayed, **When** the user clicks the funnel icon in the Paying Entity column header, **Then** a flyout opens with a text input field showing placeholder "Pretraži..." and an "Apply" button.
2. **Given** the Paying Entity flyout is open, **When** the user types "Acme" and clicks "Apply", **Then** the flyout closes and the table reloads showing only filings where the paying entity contains "Acme" (case-insensitive).
3. **Given** a text filter is active on Paying Entity, **When** the user opens the flyout again, **Then** the text box shows the current filter value so the user can edit or clear it.
4. **Given** the Payment Reference flyout is open, **When** the user types a partial reference and applies, **Then** only filings with matching payment references are shown.

---

### User Story 3 — Filter Filings by Income Type via Header Flyout (Priority: P2)

A user wants to see only dividend filings or only interest filings. The user clicks the funnel icon in the Income Type column header. A flyout appears with checkboxes for each income type (Dividend, Interest), plus "Select All" and "Clear" options. The user selects the desired types and clicks "Apply".

**Why this priority**: Follows the same enum checkbox pattern as Status. Lower priority because income type filtering is less frequently needed than status filtering.

**Independent Test**: Can be tested by opening the Income Type flyout, selecting only "Dividend", applying, and verifying only dividend filings appear.

**Acceptance Scenarios**:

1. **Given** the filings list is displayed, **When** the user clicks the funnel icon in the Income Type column header, **Then** a flyout opens with checkboxes for Dividend and Interest, both checked by default, plus "Select All" / "Clear" and "Apply".
2. **Given** the user selects only "Dividend" and clicks "Apply", **Then** only filings with income type Dividend are shown.

---

### User Story 4 — Filter Filings by Deadline via Header Flyout (Priority: P2)

A user wants to find filings with a specific deadline date. The user clicks the funnel icon in the Deadline column header. A flyout opens with a text search box (placeholder "Pretraži..."). The user types a date string (e.g., "2025-03" or "15.07") and clicks "Apply". The table shows filings where the formatted deadline text contains the search string.

**Why this priority**: Date filtering is useful but less common than status or payer filtering. Simple text matching on formatted date strings keeps the interaction consistent and avoids complex date range UI.

**Independent Test**: Can be tested by opening the Deadline flyout, typing a partial date, applying, and verifying matching filings are shown.

**Acceptance Scenarios**:

1. **Given** the filings list is displayed, **When** the user clicks the funnel icon in the Deadline column header, **Then** a flyout opens with a text input and "Apply" button (no date picker, no operator selector).
2. **Given** the user types "2025-07" and applies, **Then** only filings whose formatted deadline text contains "2025-07" are shown.

---

### User Story 5 — Remove Filter Row and Recover Vertical Space (Priority: P1)

The existing inline filter row below the column headers (added in feature 045) is removed entirely. This eliminates the misaligned ComboBoxes, TextBoxes, and CalendarDatePicker that consume excessive vertical space. The DataGrid now starts immediately below the column headers.

**Why this priority**: Removing the filter row is a prerequisite for the new flyout approach and immediately improves the visual layout by reclaiming vertical space.

**Independent Test**: Can be tested by verifying the filter row Grid element is no longer rendered, and the DataGrid rows appear directly below the column headers.

**Acceptance Scenarios**:

1. **Given** the filings page is loaded, **When** the user looks at the area between column headers and data rows, **Then** there is no filter row — data rows start immediately.
2. **Given** the old filter row has been removed, **When** the user resizes columns, **Then** there are no misalignment issues because filters live inside column headers.

---

### User Story 6 — Clear All Filters (Priority: P2)

The existing "Clear All Filters" toolbar button continues to work. When clicked, it resets all column filters (status, income type, text searches, deadline) and all funnel icons return to their inactive/default color.

**Why this priority**: Provides a quick escape hatch to remove all filters at once. Reuses existing ClearFiltersCommand logic.

**Independent Test**: Can be tested by applying filters on multiple columns, clicking "Clear All Filters", and verifying all filters are removed and all funnel icons return to inactive state.

**Acceptance Scenarios**:

1. **Given** filters are active on Status and Paying Entity, **When** the user clicks "Clear All Filters" in the toolbar, **Then** all filters are cleared, the table reloads with unfiltered data, and all funnel icons return to default color.
2. **Given** no filters are active, **When** the user looks at the toolbar, **Then** the "Clear All Filters" button is hidden or disabled.

---

### User Story 7 — Active Filter Visual Indicator (Priority: P2)

When any column has an active filter, the funnel icon in that column header is visually distinct (accent color) so the user can see at a glance which columns are filtered.

**Why this priority**: Visual feedback is essential for usability — without it, users may not realize why they're seeing a subset of data.

**Independent Test**: Can be tested by applying a filter on one column and verifying only that column's funnel icon changes color.

**Acceptance Scenarios**:

1. **Given** no filters are active, **When** the user views column headers, **Then** all funnel icons are in default/muted color.
2. **Given** the user applies a filter on Paying Entity, **When** the user views column headers, **Then** only the Paying Entity funnel icon is in accent color; other funnel icons remain in default color.
3. **Given** the user clears the Paying Entity filter, **When** the user views the column header, **Then** the funnel icon returns to default color.

---

### Edge Cases

- What happens when the user opens a flyout, makes changes, but clicks outside (dismisses) without clicking "Apply"? The filter should NOT change — changes are discarded.
- What happens when the user selects no checkboxes in an enum flyout and clicks "Apply"? The filter should treat this as "show none" (empty result set) or prevent applying with nothing selected.
- What happens when the user navigates from Reports page with a ReportIdFilter active? Filters should be cleared as before (existing behavior preserved), and all flyout states reset.
- What happens when the user opens a flyout while data is still loading from a previous filter? The flyout should still be openable; the Apply action queues after the current load completes.
- What happens when a flyout is open and the user clicks a sort arrow on the same column header? The flyout should remain open; sorting and filtering are independent actions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST remove the entire inline filter row (Grid element with ComboBoxes, TextBoxes, and CalendarDatePicker) that sits between the column headers and the data rows.
- **FR-002**: System MUST display a funnel/filter icon in each filterable column header (Status, Income Type, Paying Entity, Deadline, Payment Reference), positioned next to the existing sort arrow.
- **FR-003**: System MUST open a flyout/popup anchored to the column header when the user clicks the funnel icon.
- **FR-004**: For enum columns (Status, Income Type), the flyout MUST contain a checkbox for each enum value, a "Select All" option, a "Clear" option, and an "Apply" button.
- **FR-005**: For text columns (Paying Entity, Payment Reference), the flyout MUST contain a text input with placeholder "Pretraži..." and an "Apply" button.
- **FR-006**: For the Deadline column, the flyout MUST contain a text input with placeholder "Pretraži..." and an "Apply" button (simple text search on formatted date strings, no date picker or operators).
- **FR-007**: Clicking "Apply" in a flyout MUST close the flyout and reload the filings list with the updated filter criteria.
- **FR-008**: Dismissing the flyout without clicking "Apply" (e.g., clicking outside) MUST discard any uncommitted changes to that column's filter.
- **FR-009**: The funnel icon MUST be visually highlighted (accent color) when the corresponding column has an active filter, and revert to default color when the filter is cleared.
- **FR-010**: The existing "Clear All Filters" toolbar button MUST continue to clear all column filters and reset all funnel icons to their default state.
- **FR-011**: The existing backend filtering logic (FilingColumnFilter, query handlers, repository WHERE clauses) MUST be reused without modification.
- **FR-012**: The existing ViewModel filter properties (FilterStatus, FilterIncomeType, FilterPayingEntity, FilterPaymentReference, FilterDeadline) and ClearFiltersCommand MUST be reused.
- **FR-013**: The ReportIdFilter interaction (clearing filters when navigating from Reports) MUST continue to work as before.
- **FR-014**: When opening a flyout for a column that already has an active filter, the flyout MUST display the current filter state (checked values for enums, current search text for text fields) so the user can modify it.
- **FR-015**: For enum flyouts, "Select All" MUST check all checkboxes and "Clear" MUST uncheck all checkboxes without closing the flyout or applying the filter.
- **FR-016**: The filter icon MUST be visually distinct from the sort arrow and both MUST be independently clickable without interfering with each other.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Only the Desktop (UI) layer is impacted. Application and Infrastructure layers remain unchanged. Clean Architecture boundaries are preserved — filter logic stays in the Application/Infrastructure layers; the UI layer only changes how filter values are collected from the user.
- **CA-002 (Money and Dates)**: Deadline filter uses text search on the formatted display string. The existing `DateOnly` usage in `FilingColumnFilter` is preserved. Note: the ViewModel's `FilterDeadline` property type may change from `DateTimeOffset?` to `string?` to support text-based deadline search instead of exact date match.
- **CA-003 (Privacy and Security)**: No impact. All data remains local. No credentials involved.
- **CA-004 (Network Scope)**: No outbound network calls. Purely UI changes.
- **CA-005 (Async and UI)**: Flyout interactions are synchronous UI events. Applying a filter triggers the existing async data reload. No blocking operations introduced.
- **CA-006 (Testing Impact)**: Desktop UI tests needed for flyout open/close behavior, filter icon state toggling, and ViewModel filter property binding. Existing Application/Infrastructure tests remain valid and unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The inline filter row is completely removed — zero additional vertical space consumed by filter controls below the column headers.
- **SC-002**: Users can apply a column filter in 3 clicks or fewer (click funnel → adjust filter → click Apply).
- **SC-003**: Users can visually identify which columns have active filters at a glance without opening any flyout.
- **SC-004**: All 5 filterable columns (Status, Income Type, Paying Entity, Deadline, Payment Reference) support filtering via header flyouts.
- **SC-005**: Existing filter functionality is fully preserved — all filter combinations that worked with the old filter row produce identical results with the new flyouts.
- **SC-006**: Clearing all filters returns the table to unfiltered state within 1 user action (single click on "Clear All Filters").

## Assumptions

- The existing sort arrow icons (feature 046) and their click behavior remain unchanged; filter icons are added alongside them without modifying sort functionality.
- The ViewModel's `FilterDeadline` property will be changed from `DateTimeOffset?` to `string?` to support text search on formatted date strings rather than exact date match. This requires a corresponding change in how the ViewModel builds the `FilingColumnFilter` — converting the text to a `DateOnly?` only if the text is an exact date, or using a new text-based filter field.
- Enum checkbox flyouts default to "all checked" when no filter is active, matching the behavior of "show all" in the previous ComboBox approach.
- The "Select All" / "Clear" options in enum flyouts are quick-toggle convenience buttons, not filter actions — only "Apply" commits the filter.
- The flyout visual style (background, border, shadow) follows the application's existing popup/flyout styling conventions.
- The `StatusFilterOptions` and `IncomeTypeFilterOptions` collections on the ViewModel will be reused to populate the enum flyout checkboxes, though the binding approach will change from ComboBox SelectedItem to a collection of checked/unchecked items.
- The `IsFilterRowEnabled` property (which disables filters when ReportIdFilter is active) will be repurposed or replaced to disable flyout triggers when ReportIdFilter is active.
