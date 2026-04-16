# Feature Specification: Filings — Action Column Consolidation & Icon-Only Buttons

**Feature Branch**: `030-filings-action-column-consolidation`  
**Created**: 2025-07-17  
**Status**: Draft  
**Input**: User description: "Simplifies the Filings DataGrid by removing the dedicated Change Status ComboBox column and merging all row-level actions (Advance Status, Export XML, Delete) into a single Actions column. All three action buttons become icon-only (no visible text label) and each carries a tooltip so the action is still discoverable."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Advance Filing Status from the Actions Column (Priority: P1)

A tax preparer viewing the Filings list needs to advance a filing's status (e.g., from "Init" to "Filed", or from "Filed" to "Paid"). Instead of selecting from a dropdown in a dedicated status column, they click a single icon button in the consolidated Actions column. The button is only clickable when a valid next status exists for that filing.

**Why this priority**: Status advancement is the most frequent row-level action in the filings workflow and is the core interaction being redesigned. This story validates the replacement of the ComboBox with a command button — the riskiest UX change in the feature.

**Independent Test**: Can be fully tested by opening the Filings page with filings in various statuses and verifying the advance-status button is enabled/disabled correctly and transitions the filing to the expected next status.

**Acceptance Scenarios**:

1. **Given** a filing with status "Init" (one valid next status: "Filed"), **When** the user hovers over the advance-status icon button, **Then** a tooltip reading "Mark as Filed" is displayed.
2. **Given** a filing with status "Init", **When** the user clicks the advance-status icon button, **Then** the filing status transitions to "Filed" and the row refreshes to reflect the new status badge and updated button state.
3. **Given** a filing with status "Filed" (one valid next status: "Paid"), **When** the user hovers over the advance-status icon button, **Then** a tooltip reading "Mark as Paid" is displayed.
4. **Given** a filing with status "Paid" (no valid next statuses), **When** the user views the Actions column, **Then** the advance-status icon button is visually disabled and not clickable.
5. **Given** a filing with status "Init", **When** the user clicks the advance-status button and the operation fails, **Then** an error message is displayed and the filing status remains "Init".

---

### User Story 2 - Consolidated Actions Column Layout (Priority: P1)

A tax preparer viewing the Filings DataGrid sees a single Actions column at the far right containing three icon buttons (Advance Status, Export XML, Delete) arranged horizontally. The previously separate "Change Status", "Export", and "Delete" columns no longer exist. The read-only status badge column remains unchanged.

**Why this priority**: This story defines the core visual restructuring — collapsing three columns into one. It is co-equal with Story 1 because all other stories depend on this layout change being correct.

**Independent Test**: Can be fully tested by loading the Filings page and visually inspecting the DataGrid column layout — verifying the Actions column appears at the far right with three icon buttons, and that no separate status ComboBox, Export, or Delete columns exist.

**Acceptance Scenarios**:

1. **Given** the Filings page is loaded with one or more filings, **When** the user inspects the DataGrid columns, **Then** a single "Actions" column appears as the rightmost column containing three horizontally-arranged icon buttons per row.
2. **Given** the Filings page is loaded, **When** the user scans the column headers, **Then** there is no separate "Change Status" column, no separate "Export" column, and no separate "Delete" column.
3. **Given** the Filings page is loaded, **When** the user looks at the status area, **Then** the read-only coloured status badge (pill) column remains in its original position and is visually unchanged.

---

### User Story 3 - Export XML from the Actions Column (Priority: P2)

A tax preparer needs to export a filing as PP-OPO XML. They click the Export icon button in the consolidated Actions column. The button is always enabled regardless of filing status.

**Why this priority**: Export is a frequently used action but is simpler to migrate than status advancement because it has no conditional enable/disable logic. It is important for workflow completeness.

**Independent Test**: Can be fully tested by clicking the export icon button for any filing and verifying the XML export flow is triggered, the file save dialog appears, and the correct file is produced.

**Acceptance Scenarios**:

1. **Given** a filing in any status, **When** the user hovers over the export icon button, **Then** a tooltip reading "Export PP-OPO XML" is displayed.
2. **Given** a filing in any status, **When** the user clicks the export icon button, **Then** the PP-OPO XML export flow is triggered and the file save dialog appears.
3. **Given** a filing in any status, **When** the export operation fails, **Then** an error message is displayed to the user.

---

### User Story 4 - Delete Filing from the Actions Column (Priority: P2)

A tax preparer needs to remove a filing. They click the Delete icon button in the consolidated Actions column. The button is always enabled, uses a destructive visual style, and triggers a confirmation dialog before deletion.

**Why this priority**: Delete is always available regardless of status and uses the existing confirmation flow, making it a straightforward migration. It completes the trio of consolidated actions.

**Independent Test**: Can be fully tested by clicking the delete icon button, confirming in the dialog, and verifying the filing is removed from the list.

**Acceptance Scenarios**:

1. **Given** a filing in any status, **When** the user hovers over the delete icon button, **Then** a tooltip reading "Delete filing" is displayed.
2. **Given** a filing in any status, **When** the user clicks the delete icon button, **Then** a confirmation dialog appears asking the user to confirm deletion.
3. **Given** the confirmation dialog is shown, **When** the user confirms, **Then** the filing is deleted and the DataGrid refreshes without the deleted row.
4. **Given** the confirmation dialog is shown, **When** the user cancels, **Then** the filing remains in the list and no changes are made.
5. **Given** a filing in any status, **When** the user views the delete icon button, **Then** it is visually styled as a destructive action (distinct from the other two buttons).

---

### User Story 5 - Icon-Only Buttons with Tooltip Discoverability (Priority: P2)

All three action buttons in the consolidated column display only an icon (no text label) to save horizontal space, but each provides a tooltip on hover so the user can discover the action without guessing.

**Why this priority**: Discoverability via tooltips is essential for icon-only buttons. Without tooltips, users unfamiliar with the icons would not know what each button does. This story is lower priority than the functional stories because the actions still work without tooltips — they are a usability enhancement.

**Independent Test**: Can be fully tested by hovering over each icon button in the Actions column and verifying the correct tooltip text appears.

**Acceptance Scenarios**:

1. **Given** a filing row in the DataGrid, **When** the user hovers over the advance-status button for a filing with status "Init", **Then** a tooltip reading "Mark as Filed" appears.
2. **Given** a filing row in the DataGrid, **When** the user hovers over the export button, **Then** a tooltip reading "Export PP-OPO XML" appears.
3. **Given** a filing row in the DataGrid, **When** the user hovers over the delete button, **Then** a tooltip reading "Delete filing" appears.
4. **Given** a filing with status "Paid" (advance-status button disabled), **When** the user hovers over the disabled advance-status button, **Then** no tooltip is displayed (or the tooltip indicates no further status transitions are available).

---

### Edge Cases

- What happens when a filing's status changes between page load and clicking the advance-status button (stale state)? The command should handle this gracefully by reloading the page and displaying an error if the transition is no longer valid.
- What happens when the advance-status button is clicked rapidly multiple times? Only the first click should be processed; subsequent clicks should be ignored while the command is in flight.
- What happens when all three buttons are rendered for a filing at the terminal status ("Paid")? The advance-status button should be disabled; export and delete buttons should remain enabled.
- How does the Actions column behave when the DataGrid is resized to a very narrow width? The three icon buttons should remain visible and not overflow or collapse into an unusable state.
- What happens when the user has filings selected via checkboxes and clicks a row-level delete button (not bulk delete)? Only the specific filing for that row should be deleted, not the selected set.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST remove the dedicated "Change Status" ComboBox column from the Filings DataGrid.
- **FR-002**: The system MUST remove the dedicated "Export" button column from the Filings DataGrid.
- **FR-003**: The system MUST remove the dedicated "Delete" button column from the Filings DataGrid.
- **FR-004**: The system MUST add a single consolidated "Actions" column as the rightmost column of the Filings DataGrid.
- **FR-005**: The Actions column MUST contain three icon-only buttons arranged horizontally: Advance Status, Export XML, and Delete.
- **FR-006**: The Advance Status button MUST be enabled only when the filing has at least one valid next status (i.e., the count of available next statuses is greater than zero).
- **FR-007**: The Advance Status button MUST invoke the advance-status command with the first available next status as the command parameter when clicked.
- **FR-008**: The Export XML button MUST be enabled for all filings regardless of status.
- **FR-009**: The Export XML button MUST invoke the existing export command with the filing's identifier when clicked.
- **FR-010**: The Delete button MUST be enabled for all filings regardless of status.
- **FR-011**: The Delete button MUST invoke the existing delete command with the filing's identifier when clicked.
- **FR-012**: The Delete button MUST use a destructive visual style that distinguishes it from the other action buttons.
- **FR-013**: Each icon button MUST display a tooltip on hover: the Advance Status button shows the next valid status label (e.g., "Mark as Filed", "Mark as Paid"); the Export button shows "Export PP-OPO XML"; the Delete button shows "Delete filing".
- **FR-014**: When a filing has no valid next statuses (terminal state), the Advance Status button MUST appear visually disabled and MUST NOT be interactive.
- **FR-015**: The read-only status badge column (coloured pill showing current status) MUST remain unchanged in position and appearance.
- **FR-016**: The code-behind event handler for the status ComboBox selection change MUST be removed from the view's code-behind file.
- **FR-017**: The Advance Status button MUST use a direct command binding instead of a code-behind event handler.
- **FR-018**: All three buttons MUST display only an icon — no visible text label.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature impacts the Desktop (presentation) layer only — specifically the Filings view and its associated view model bindings. No changes to Domain, Application, or Infrastructure layers. Clean Architecture boundaries remain valid because only the UI layout and binding mechanism change; no business logic is added or modified.
- **CA-002 (Money and Dates)**: No monetary or date fields are added, removed, or modified. Existing `decimal` (TaxPayable) and `DateOnly` (FilingDeadline) fields on the row view model remain untouched.
- **CA-003 (Privacy and Security)**: No change to data storage or secrets. All data remains local-first. No new personal data is exposed.
- **CA-004 (Network Scope)**: No new outbound network calls. The export flow uses the existing local file-save mechanism.
- **CA-005 (Async and UI)**: All commands (advance status, export, delete) already execute asynchronously via reactive command bindings. No blocking I/O is introduced. The button-click-to-command path is non-blocking.
- **CA-006 (Testing Impact)**: Desktop layer UI tests must be updated to verify the new consolidated Actions column layout, button enabled/disabled states, tooltip content, and command bindings. Existing ViewModel unit tests for AdvanceStatusCommand, ExportCommand, and DeleteCommand remain valid with no changes needed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The Filings DataGrid displays exactly one Actions column instead of the previous three separate action columns (Change Status, Export, Delete), reducing total column count by two.
- **SC-002**: Users can advance a filing's status in a single click (one click on the icon button) compared to the previous two-step interaction (open ComboBox, select status).
- **SC-003**: All three action buttons display the correct tooltip text on hover within 100% of filings in any status.
- **SC-004**: The advance-status button is disabled for 100% of filings in terminal status ("Paid") and enabled for 100% of filings with available transitions.
- **SC-005**: The existing filing workflows (advance status, export XML, delete) continue to function identically in outcome — no regressions in success rate or error handling.
- **SC-006**: The consolidated Actions column occupies less horizontal space than the three separate columns combined, improving the data density of the Filings DataGrid.

## Assumptions

- The filing status state machine remains unchanged: Init → Filed → Paid, with no new statuses introduced by this feature.
- Each filing has at most one valid next status at any time (the `AvailableNextStatuses` list contains zero or one entry), so the advance-status button always advances to a deterministic next state.
- Icon assets (or icon font glyphs) for the three actions are either already available in the project's icon set or will be selected from the existing icon library during implementation — no custom icon design is needed.
- The tooltip text for the advance-status button is derived from the status display name (e.g., "Mark as Filed") and follows the existing localization pattern.
- Bulk operations (bulk delete, select all, clear selection) are unaffected by this change and continue to operate via the toolbar, not the per-row Actions column.
- The payment reference inline-edit column is unaffected and remains as a separate column with its existing lost-focus save behaviour.
- Keyboard accessibility for the new icon buttons follows the platform default — buttons are focusable via Tab and activatable via Enter/Space.
