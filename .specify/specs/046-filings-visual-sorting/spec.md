# Feature Specification: Filings Visual Sorting

**Feature Branch**: `044-filings-visual-sorting`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Improve the sorting UX on the Filings page DataGrid: add visual sort indicators in column headers, remove redundant Unpaid/All filter toggles"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sort Filings by Column with Visual Feedback (Priority: P1)

A user viewing the filings list wants to sort the data by a specific column (e.g., Filing Deadline, Tax Payable) and immediately see which column is sorted and in which direction. They click a column header to cycle through unsorted → ascending → descending → unsorted, with a clear arrow indicator in the header showing the current state.

**Why this priority**: Sort indicators are the core value of this feature — without them, users cannot tell which column is actively sorted or in which direction, leading to confusion and repeated clicks.

**Independent Test**: Can be fully tested by clicking column headers and verifying arrow indicators appear/change, and that data reorders correctly.

**Acceptance Scenarios**:

1. **Given** the filings page is displayed with data and no column is sorted, **When** the user clicks the "Filing Deadline" column header, **Then** an ascending arrow (↑) appears in that column header and the rows are sorted by filing deadline in ascending order.
2. **Given** the "Filing Deadline" column is sorted ascending, **When** the user clicks the same column header again, **Then** the arrow changes to descending (↓) and rows reorder to descending filing deadline.
3. **Given** the "Filing Deadline" column is sorted descending, **When** the user clicks the same column header a third time, **Then** the sort indicator is removed and the column returns to its default (unsorted) order.
4. **Given** the "Filing Deadline" column is sorted ascending, **When** the user clicks a different sortable column header (e.g., "Tax Payable"), **Then** the Filing Deadline arrow disappears, a new ascending arrow appears on Tax Payable, and rows sort by Tax Payable ascending.

---

### User Story 2 - Remove Redundant Filter Toggles (Priority: P2)

A user navigating to the filings page no longer sees the "Unpaid" and "All" radio buttons in the top toolbar, since filtering will be handled by inline column filters (feature 045). The top area is cleaner with fewer controls.

**Why this priority**: Removing redundant controls simplifies the interface and avoids confusing users with two filter mechanisms. However, this depends on the inline column filters (feature 045) being available, making it slightly lower priority than the sort indicators which stand alone.

**Independent Test**: Can be tested by loading the filings page and confirming the Unpaid/All radio buttons are no longer present, while the rest of the toolbar (New Filing button, report filter chip, bulk actions) remains functional.

**Acceptance Scenarios**:

1. **Given** the filings page is loaded, **When** the user looks at the top toolbar area, **Then** the "Unpaid" and "All" radio buttons are not present.
2. **Given** the filter toggles are removed, **When** the user interacts with remaining toolbar elements (New Filing button, report filter chip, bulk selection), **Then** all remaining toolbar features continue to work correctly.
3. **Given** the filter toggles are removed, **When** the page loads, **Then** the filings list shows all filings by default (equivalent to "All" being selected).

---

### User Story 3 - Distinguish Sortable from Non-Sortable Columns (Priority: P3)

A user scanning the column headers can tell at a glance which columns support sorting and which do not. Non-sortable columns (e.g., selection checkbox, actions, payment reference) show no sort affordance, while sortable columns show a subtle neutral indicator or cursor change on hover.

**Why this priority**: Visual distinction between sortable and non-sortable columns improves discoverability but is a polish item — sorting still works without it.

**Independent Test**: Can be tested by hovering over sortable and non-sortable column headers and verifying the visual affordance differs.

**Acceptance Scenarios**:

1. **Given** the filings page is displayed, **When** the user hovers over a sortable column header (e.g., "Income Type"), **Then** the cursor or visual affordance indicates the column is clickable/sortable.
2. **Given** the filings page is displayed, **When** the user looks at a non-sortable column header (e.g., "Actions", checkbox column), **Then** no sort indicator or affordance is shown.

---

### Edge Cases

- What happens when the user sorts a column and then the dataset refreshes (e.g., after a status change or deletion)? The current sort state should be preserved and re-applied to the refreshed data.
- What happens when sorting a column that contains identical values for all visible rows? The sort indicator should still cycle correctly, even if row order doesn't visibly change.
- What happens when the filings list is empty? Sort indicators should still be clickable/visible in headers, but no data reordering occurs.
- What happens when the user removes the report filter chip while a column is sorted? The sort should remain applied to the updated (unfiltered) dataset.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sortable column headers MUST display an ascending arrow (↑) when sorted ascending and a descending arrow (↓) when sorted descending.
- **FR-002**: Clicking a sortable column header MUST cycle through: unsorted → ascending → descending → unsorted.
- **FR-003**: Only one column MUST be sorted at a time (single-column sorting). Activating sort on a new column MUST clear the sort indicator from the previously sorted column.
- **FR-004**: Sort arrows MUST be rendered directly within the column header area, visible without hovering.
- **FR-005**: Non-sortable columns (selection checkbox, status badge, payment reference, actions) MUST NOT show sort indicators or respond to header clicks for sorting.
- **FR-006**: Sorting MUST be performed client-side on the current in-memory dataset.
- **FR-007**: The "Unpaid" and "All" radio button controls MUST be removed from the top toolbar area of the filings page.
- **FR-008**: After removing the filter toggles, the filings page MUST default to showing all filings (no pre-filter applied).
- **FR-009**: The text-based sort indicator display currently shown in the toolbar (e.g., "↓ FilingDeadline") MUST be removed since the column header arrows replace this function.
- **FR-010**: The active sort state MUST be preserved when the dataset refreshes (e.g., after status updates, deletions, or navigation between pages).
- **FR-011**: All remaining toolbar elements (New Filing button, report filter chip, bulk selection toolbar) MUST continue to function correctly after the filter toggle removal.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature impacts only the Desktop (UI) layer — specifically the FilingsView (AXAML) and FilingsViewModel. No changes to Domain, Application, or Infrastructure layers. Clean Architecture boundaries remain intact.
- **CA-002 (Money and Dates)**: No changes to monetary or date data handling. Sort operations use existing field values.
- **CA-003 (Privacy and Security)**: No new data storage or external data access. All sorting is performed on locally-held data already displayed to the user.
- **CA-004 (Network Scope)**: No new outbound network calls. Sorting is entirely client-side.
- **CA-005 (Async and UI)**: Sorting is a synchronous in-memory operation on the current page's data. No I/O involved. UI remains responsive.
- **CA-006 (Testing Impact)**: Desktop ViewModel tests need updating to verify sort-cycle behavior and that the ShowAll property default changes. Headless UI tests should verify sort indicator rendering and absence of removed controls.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can identify the currently sorted column and its direction within 1 second of looking at the filings page header row.
- **SC-002**: Users can sort any sortable column in 1 click (ascending) or 2 clicks (descending) from an unsorted state.
- **SC-003**: 100% of sortable columns display a visible directional arrow when actively sorted.
- **SC-004**: The top toolbar contains zero filter toggle controls (Unpaid/All radio buttons) after this feature is complete.
- **SC-005**: All existing toolbar functionality (New Filing, report filter chip, bulk delete) continues to pass existing tests without modification.
- **SC-006**: Sort state persists correctly across page refreshes triggered by data mutations (status changes, deletions).

## Assumptions

- The existing `FilingSortColumn` enum and `SortDescending` property in the ViewModel already support the sort state model; this feature adds visual representation, not new sort logic.
- The default sort on page load will be FilingDeadline descending (matching current behavior), but the column header will show the corresponding arrow.
- Removing the filter toggles means the `ShowAll` property defaults to `true` and the associated radio buttons are simply removed from the view — the property may be retained for backward compatibility or removed entirely as an implementation detail.
- Feature 045 (inline column filters) will independently handle filtering; this feature does not need to provide any filtering replacement.
- The existing `SortIndicatorDisplay` text property in the toolbar becomes obsolete and should be removed along with its binding.
