# Feature Specification: Bulk Delete for Filings and Reports

**Feature Branch**: `025-bulk-delete-fillings-reports`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Implement bulk delete for both the Filings and Reports pages. UX requirements: 1. Add a checkbox column as the first column to both DataGrids (IsSelected TwoWay). 2. Toolbar: always show 'Select All' and 'Clear Selection' buttons when HasItems. Show 'Delete Selected (N)' button only when HasSelection = true; use a destructive style (red foreground). The count N updates reactively. 3. Confirmation dialog: summarise count; for reports warn that linked filings are also deleted. 4. After delete: clear selection, reload list. All async. All strings in Strings.resx. No blocking UI."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Bulk Delete Filings (Priority: P1)

A user has accumulated many outdated or erroneous filings and wants to remove them in a single operation rather than deleting them one at a time. The user navigates to the Filings page, selects multiple filings using checkboxes, reviews the selection count displayed in the toolbar, clicks "Delete Selected (N)", confirms the action in a dialog, and sees the filings removed with the list refreshed.

**Why this priority**: Bulk deletion of filings is the core workflow. Filings are the primary data entity and the most numerous records. Users managing tax filings across many periods need this efficiency gain the most.

**Independent Test**: Can be fully tested on the Filings page alone — select several filings, trigger bulk delete, verify they are removed and the list refreshes with selection cleared.

**Acceptance Scenarios**:

1. **Given** the Filings page shows 5 filings, **When** the user checks 3 filings and clicks "Delete Selected (3)", **Then** a confirmation dialog appears showing the count of 3 filings to be deleted.
2. **Given** the confirmation dialog is displayed, **When** the user confirms, **Then** all 3 selected filings are deleted, the selection is cleared, and the filings list reloads showing the remaining 2 filings.
3. **Given** the confirmation dialog is displayed, **When** the user cancels, **Then** no filings are deleted and the selection remains intact.
4. **Given** the Filings page shows 5 filings, **When** the user clicks "Select All", **Then** all 5 filings are checked and the toolbar shows "Delete Selected (5)".
5. **Given** all 5 filings are selected, **When** the user clicks "Clear Selection", **Then** all checkboxes are unchecked and the "Delete Selected" button is hidden.

---

### User Story 2 — Bulk Delete Reports with Cascade Warning (Priority: P1)

A user wants to remove several imported reports that are no longer needed. The user navigates to the Reports page, selects multiple reports, and clicks "Delete Selected (N)". Because reports may have linked filings, the confirmation dialog explicitly warns that associated filings will also be deleted. After confirmation, the reports and their linked filings are removed.

**Why this priority**: Equal to filings because the feature request explicitly requires both pages. Reports have a cascade-delete relationship with filings, making the warning dialog a critical safety mechanism.

**Independent Test**: Can be fully tested on the Reports page alone — select reports (some with linked filings, some without), trigger bulk delete, verify the warning mentions linked filings, confirm, and verify both reports and their linked filings are removed.

**Acceptance Scenarios**:

1. **Given** the Reports page shows 4 reports (2 of which have linked filings), **When** the user checks all 4 and clicks "Delete Selected (4)", **Then** a confirmation dialog appears that states 4 reports will be deleted and warns that all linked filings will also be removed.
2. **Given** the confirmation dialog is displayed, **When** the user confirms, **Then** all 4 reports and all their linked filings are deleted, the selection is cleared, and the reports list reloads.
3. **Given** the confirmation dialog is displayed, **When** the user cancels, **Then** no reports or filings are deleted and the selection remains intact.

---

### User Story 3 — Selection State and Toolbar Reactivity (Priority: P2)

A user interacts with the selection checkboxes and expects the toolbar to react instantly. The "Select All" and "Clear Selection" buttons are always visible when items exist in the list. The "Delete Selected (N)" button appears only when at least one item is selected and disappears when the selection is cleared. The count N updates in real time as the user checks or unchecks individual items.

**Why this priority**: While the selection mechanism is required for bulk delete to function, this story focuses specifically on the reactive UX polish that keeps the user informed and prevents accidental actions.

**Independent Test**: Can be tested by toggling checkboxes and observing toolbar state changes — no actual deletion required.

**Acceptance Scenarios**:

1. **Given** the Filings page has items loaded, **When** no items are selected, **Then** "Select All" and "Clear Selection" buttons are visible but "Delete Selected" is hidden.
2. **Given** no items are selected, **When** the user checks one filing, **Then** the toolbar immediately shows "Delete Selected (1)".
3. **Given** 3 filings are selected, **When** the user unchecks one, **Then** the toolbar updates to "Delete Selected (2)".
4. **Given** 3 filings are selected, **When** the user clicks "Clear Selection", **Then** all checkboxes are unchecked and "Delete Selected" is hidden.
5. **Given** the page is loading or has no items, **When** the data loads to show items, **Then** "Select All" and "Clear Selection" become visible.
6. **Given** the list has no items (empty state), **When** the page renders, **Then** "Select All", "Clear Selection", and "Delete Selected" are all hidden.

---

### User Story 4 — Non-Blocking Async Delete Operation (Priority: P2)

During a bulk delete, the user should never experience a frozen or unresponsive interface. The delete operation runs asynchronously. The UI remains responsive throughout — the user sees a loading state during deletion, and once complete, the list refreshes and the selection resets.

**Why this priority**: Non-blocking async behaviour is essential for good user experience but is an implementation quality concern rather than a functional workflow.

**Independent Test**: Can be tested by selecting a large number of items, triggering delete, and verifying the UI remains responsive (buttons clickable, no frozen frames) during the operation.

**Acceptance Scenarios**:

1. **Given** the user confirms bulk deletion of 10 filings, **When** the deletion is in progress, **Then** the UI remains responsive and shows a loading indicator.
2. **Given** the deletion completes successfully, **When** the list reloads, **Then** the selection is cleared and the deleted items no longer appear.
3. **Given** some deletions fail due to an error, **When** the operation completes, **Then** an error message is displayed, the list reloads to reflect the current state, and the selection is cleared.

---

### Edge Cases

- What happens when the user selects items, then navigates away and returns? Selection is cleared on page load — no stale selection persists.
- What happens when items are deleted by another process between selection and confirmation? The system handles missing items gracefully — already-deleted items are skipped without error.
- What happens when the user selects all items on a paginated filings page? Only items on the current page are selected — "Select All" applies to the visible page, not the entire dataset.
- What happens when a bulk delete partially fails (e.g., 3 of 5 succeed)? The system displays an error, reloads the list to reflect current state, and clears the selection.
- What happens when the user rapidly clicks "Delete Selected" twice? The button is disabled while a delete operation is in progress to prevent double-submission.

## Requirements *(mandatory)*

### Functional Requirements

#### Selection

- **FR-001**: System MUST display a checkbox column as the first column in both the Filings and Reports data grids.
- **FR-002**: Each checkbox MUST support two-way binding to the item's selected state, updating the selection model when toggled by the user and reflecting programmatic changes (Select All, Clear Selection).
- **FR-003**: Selection state MUST be local to the current page view and cleared when navigating away or when the list reloads after a delete operation.

#### Toolbar

- **FR-004**: The toolbar MUST display "Select All" and "Clear Selection" buttons whenever the list contains at least one item.
- **FR-005**: The toolbar MUST hide "Select All" and "Clear Selection" buttons when the list is empty (no items loaded).
- **FR-006**: The toolbar MUST display a "Delete Selected (N)" button only when at least one item is selected. N represents the current count of selected items.
- **FR-007**: The "Delete Selected (N)" button MUST use a destructive visual style (red foreground text) to signal the irreversible nature of the action.
- **FR-008**: The count N in "Delete Selected (N)" MUST update reactively as the user checks or unchecks individual items, without requiring any manual refresh.
- **FR-009**: "Select All" MUST select all items currently visible in the list (the current page for paginated views).
- **FR-010**: "Clear Selection" MUST deselect all currently selected items.

#### Confirmation Dialog

- **FR-011**: System MUST display a confirmation dialog before executing a bulk delete operation.
- **FR-012**: The Filings confirmation dialog MUST summarise the number of filings about to be deleted (e.g., "You are about to delete N filing(s). This action cannot be undone.").
- **FR-013**: The Reports confirmation dialog MUST summarise the number of reports about to be deleted AND warn that all filings linked to the selected reports will also be deleted.
- **FR-014**: The confirmation dialog MUST offer "Confirm" and "Cancel" actions. Selecting "Cancel" MUST leave all data and selection unchanged.

#### Deletion

- **FR-015**: Upon confirmation, the system MUST delete all selected filings asynchronously.
- **FR-016**: Upon confirmation, the system MUST delete all selected reports and their linked filings asynchronously, following the existing cascade-delete behaviour.
- **FR-017**: After a successful bulk delete, the system MUST clear the selection and reload the list to reflect the current state.
- **FR-018**: The "Delete Selected" button MUST be disabled while a delete operation is in progress to prevent duplicate submissions.
- **FR-019**: If any deletion fails, the system MUST display an error message, reload the list to reflect current state, and clear the selection.

#### Localisation

- **FR-020**: All user-facing strings introduced by this feature (button labels, dialog titles, dialog messages, error messages) MUST be defined in the localisation resource file and not hardcoded.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature impacts the Desktop layer (Views, ViewModels, Resources) and the Application layer (new bulk-delete commands and handlers). Domain entities are unchanged. Clean Architecture boundaries remain valid — the Desktop layer dispatches commands; the Application layer orchestrates deletion; the Infrastructure layer performs data access.
- **CA-002 (Money and Dates)**: No new monetary or date fields are introduced. Existing `decimal` and `DateOnly` usage in Filing and Report entities is unaffected.
- **CA-003 (Privacy and Security)**: All data operations remain local-first. No new secrets or external credentials are introduced. Deletion is a local database operation.
- **CA-004 (Network Scope)**: No new outbound network calls are introduced. Bulk delete operates entirely on the local database.
- **CA-005 (Async and UI)**: All deletion operations MUST be async. The UI MUST remain responsive during bulk delete — no blocking calls on the UI thread. Loading indicators MUST be shown during operations.
- **CA-006 (Testing Impact)**: New Application-layer tests are required for bulk-delete command handlers (Filings and Reports). New Desktop-layer tests are required for ViewModel selection logic, toolbar state reactivity, and confirmation dialog flow. Existing single-delete tests remain unaffected.

### Key Entities *(include if feature involves data)*

- **Filing**: A tax filing record that may optionally belong to a Report (via a report reference). Filings are the primary items on the Filings page. Bulk delete removes selected filings directly.
- **Report**: An imported report that can have zero or more linked Filings. Deleting a report cascades to remove all linked filings. This cascade behaviour must be clearly communicated to the user during bulk delete confirmation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can select and delete 20 filings in a single operation in under 15 seconds (selection through confirmation to refreshed list), compared to the current one-at-a-time workflow.
- **SC-002**: 100% of user-facing strings for this feature are externalised in the resource file — zero hardcoded display strings.
- **SC-003**: The UI remains responsive (no frozen frames) during bulk delete of up to 100 items.
- **SC-004**: The toolbar selection count updates within 200ms of a checkbox state change, providing real-time feedback.
- **SC-005**: The Reports bulk-delete confirmation dialog always displays the cascade warning about linked filings, ensuring users are fully informed before confirming.
- **SC-006**: After a bulk delete operation, the selection is cleared and the list reloads without requiring manual user intervention.

## Assumptions

- The existing single-item delete commands and handlers for both Filings and Reports remain unchanged. Bulk delete is a new, parallel capability.
- "Select All" on the Filings page applies only to the currently visible page (consistent with the existing pagination model of 20 items per page). There is no "select all across all pages" feature.
- The Reports page does not use pagination (loads all reports at once), so "Select All" selects all loaded reports.
- The existing confirmation dialog pattern (lightweight code-based dialog returning a boolean result) will be extended for bulk operations rather than replaced.
- The existing cascade-delete logic (delete linked filings before deleting the report) will be reused for bulk report deletion, applied per selected report.
- Bulk delete does not require undo/rollback capability — the confirmation dialog is the primary safeguard against accidental deletion.
- The destructive button style (red foreground) follows the application's existing styling conventions and does not require a new design system component.
