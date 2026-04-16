# Feature Specification: Header Checkbox for Select All / Clear All

**Feature Branch**: `028-header-checkbox-select-all`  
**Created**: 2025-07-17  
**Status**: Draft  
**Input**: User description: "Replaces the standalone 'Select All' and 'Clear Selection' toolbar buttons on the Filings and Reports pages with a single tri-state checkbox placed in the header cell of the selection column."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Select All Rows via Header Checkbox (Priority: P1)

A user viewing the Filings or Reports page wants to quickly select every row to perform a bulk action (e.g., delete). Instead of scanning the toolbar for a text button, the user clicks the checkbox in the selection column header. All rows become selected and the header checkbox shows a checked state.

**Why this priority**: This is the core interaction replacing the existing "Select All" button. Without it the feature delivers no value.

**Independent Test**: Can be fully tested by loading a page with multiple rows, clicking the header checkbox, and verifying all row checkboxes become checked and the header checkbox shows a fully-checked state.

**Acceptance Scenarios**:

1. **Given** the Filings page shows 5 rows and no rows are selected, **When** the user clicks the header checkbox, **Then** all 5 row checkboxes become checked and the header checkbox displays a checked state.
2. **Given** the Reports page shows 10 rows and no rows are selected, **When** the user clicks the header checkbox, **Then** all 10 row checkboxes become checked and the header checkbox displays a checked state.
3. **Given** the Filings page shows 3 rows and 1 row is already selected (indeterminate state), **When** the user clicks the header checkbox, **Then** all 3 row checkboxes become checked and the header checkbox transitions from indeterminate to checked.

---

### User Story 2 — Deselect All Rows via Header Checkbox (Priority: P1)

A user who has selected all rows decides they no longer want to perform a bulk action. They click the header checkbox (which is currently checked) to clear the entire selection in one click.

**Why this priority**: The deselect-all flow completes the toggle cycle and replaces the "Clear Selection" button. It is essential for the checkbox to function as a complete replacement for both toolbar buttons.

**Independent Test**: Can be fully tested by selecting all rows, clicking the header checkbox, and verifying all row checkboxes become unchecked and the header checkbox shows an unchecked state.

**Acceptance Scenarios**:

1. **Given** all rows on the Filings page are selected, **When** the user clicks the header checkbox, **Then** all row checkboxes become unchecked and the header checkbox displays an unchecked state.
2. **Given** all rows on the Reports page are selected, **When** the user clicks the header checkbox, **Then** all row checkboxes become unchecked and the header checkbox displays an unchecked state.

---

### User Story 3 — Visual Indication of Partial Selection (Priority: P2)

A user manually selects a few rows by clicking individual row checkboxes. The header checkbox automatically reflects the partial selection by showing an indeterminate (dash) state, giving the user a clear visual cue that some — but not all — rows are selected.

**Why this priority**: The indeterminate state provides essential visual feedback that distinguishes between "none selected" and "some selected." It is critical for usability but not strictly required for the select/deselect actions to work.

**Independent Test**: Can be fully tested by selecting one or more (but not all) individual row checkboxes and verifying the header checkbox shows an indeterminate indicator.

**Acceptance Scenarios**:

1. **Given** the Filings page shows 5 rows and no rows are selected, **When** the user checks 2 individual row checkboxes, **Then** the header checkbox displays an indeterminate state.
2. **Given** 3 of 5 rows are selected on the Reports page (indeterminate state), **When** the user unchecks one of the selected rows, **Then** the header checkbox remains in the indeterminate state (2 of 5 selected).
3. **Given** 4 of 5 rows are selected, **When** the user checks the last remaining unchecked row, **Then** the header checkbox transitions from indeterminate to fully checked.
4. **Given** 1 of 5 rows is selected (indeterminate state), **When** the user unchecks that row, **Then** the header checkbox transitions from indeterminate to unchecked.

---

### User Story 4 — Toolbar Cleanup (Priority: P2)

A user opens the Filings or Reports page and sees a cleaner, less cluttered toolbar. The standalone "Select All" and "Clear Selection" text buttons are no longer visible. The existing "Delete Selected (N)" bulk-action button remains available.

**Why this priority**: Removing the redundant toolbar buttons reduces visual noise and completes the UX migration. The header checkbox must be in place first (P1 stories) for removal to be safe.

**Independent Test**: Can be fully tested by opening the Filings and Reports pages and visually confirming that "Select All" and "Clear Selection" buttons are absent from the toolbar while the "Delete Selected (N)" button remains.

**Acceptance Scenarios**:

1. **Given** the user opens the Filings page, **When** the page finishes loading, **Then** the toolbar does not contain a "Select All" button.
2. **Given** the user opens the Filings page, **When** the page finishes loading, **Then** the toolbar does not contain a "Clear Selection" button.
3. **Given** the user opens the Reports page, **When** the page finishes loading, **Then** the toolbar does not contain "Select All" or "Clear Selection" buttons.
4. **Given** the user selects rows on the Filings page, **When** at least one row is selected, **Then** the "Delete Selected (N)" button is visible and functional in the toolbar.

---

### Edge Cases

- What happens when the page has zero rows (empty state)? The header checkbox should be unchecked and non-interactive (disabled or hidden), since there are no rows to select.
- What happens when the row list changes while rows are selected (e.g., a bulk delete removes some rows)? The header checkbox must recalculate its state based on the remaining rows.
- What happens when a single row is present? Selecting it should transition the header checkbox directly from unchecked to checked (no indeterminate state for one row fully selected).
- What happens when the user rapidly clicks the header checkbox? The system should handle each click as a discrete toggle without leaving the selection in an inconsistent state.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST display a tri-state checkbox in the header cell of the selection column on both the Filings page and the Reports page.
- **FR-002**: The header checkbox MUST display a checked state when all rows on the current page are selected.
- **FR-003**: The header checkbox MUST display an unchecked state when no rows are selected.
- **FR-004**: The header checkbox MUST display an indeterminate state when some (but not all) rows are selected.
- **FR-005**: Clicking the header checkbox when it is unchecked MUST select all rows.
- **FR-006**: Clicking the header checkbox when it is in the indeterminate state MUST select all rows.
- **FR-007**: Clicking the header checkbox when it is checked MUST deselect all rows.
- **FR-008**: The header checkbox state MUST update automatically when individual row checkboxes are toggled by the user.
- **FR-009**: The standalone "Select All" text button MUST be removed from the Filings page toolbar.
- **FR-010**: The standalone "Clear Selection" text button MUST be removed from the Filings page toolbar.
- **FR-011**: The standalone "Select All" text button MUST be removed from the Reports page toolbar.
- **FR-012**: The standalone "Clear Selection" text button MUST be removed from the Reports page toolbar.
- **FR-013**: The existing "Delete Selected (N)" bulk-action button MUST remain in the toolbar on both pages.
- **FR-014**: The SelectAllCommand and ClearSelectionCommand MUST be retained on both ViewModels to back the header checkbox logic.
- **FR-015**: When the page has no rows (empty state), the header checkbox MUST appear unchecked and MUST NOT respond to user clicks.
- **FR-016**: When rows are added or removed (e.g., after a bulk delete), the header checkbox state MUST recalculate based on the current row selection.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature impacts the Desktop layer only (Views and ViewModels). No Domain, Application, or Infrastructure changes are required. Clean Architecture boundaries remain valid — the header checkbox is a pure presentation concern.
- **CA-002 (Money and Dates)**: No monetary or date fields are introduced or affected by this feature.
- **CA-003 (Privacy and Security)**: No new data is stored. Selection state is transient (in-memory only). Local-first architecture is unaffected.
- **CA-004 (Network Scope)**: No outbound network calls are introduced. This is a purely local UI change.
- **CA-005 (Async and UI)**: No I/O operations are involved. All selection state changes are synchronous, in-memory property updates on ViewModels. UI thread safety must be maintained when updating the header checkbox state in response to row selection changes.
- **CA-006 (Testing Impact)**: Desktop ViewModel tests must be added or updated to cover: header checkbox state transitions (unchecked → checked → unchecked), indeterminate state detection, click behavior from each state, and empty-row edge case. No Domain, Application, or Infrastructure test changes required.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can select all rows on a page with a single click on the header checkbox, completing the action in under 1 second.
- **SC-002**: Users can deselect all rows on a page with a single click on the header checkbox, completing the action in under 1 second.
- **SC-003**: The header checkbox visually reflects the correct selection state (unchecked / indeterminate / checked) within 200 milliseconds of any row selection change.
- **SC-004**: The toolbar button count on both the Filings and Reports pages is reduced by 2 (removal of "Select All" and "Clear Selection"), resulting in a cleaner interface.
- **SC-005**: 100% of existing bulk-delete workflows continue to function without regression after the toolbar buttons are removed.
- **SC-006**: The header checkbox behaves consistently and identically on both the Filings and Reports pages.

## Assumptions

- The existing SelectAllCommand and ClearSelectionCommand logic on both ViewModels is correct and complete; the header checkbox will reuse these commands rather than introducing new selection logic.
- The selection column already exists in both data grids with per-row checkboxes; only the header cell needs modification.
- The header checkbox operates on the currently visible rows only (there is no server-side pagination or "select all across pages" requirement).
- The indeterminate visual indicator follows the platform-native checkbox rendering (a dash or filled square in the checkbox) without requiring a custom control template.
- The SelectedCount property already maintained on both ViewModels provides the data needed to compute the tri-state (compare SelectedCount to total row count).
