# Feature Specification: Reports — Icon-Only Action Column

**Feature Branch**: `031-reports-icon-only-action-column`  
**Created**: 2025-07-16  
**Status**: Draft  
**Input**: User description: "Converts the Reports DataGrid action buttons ('View Filings' and 'Delete') to icon-only buttons with ToolTip.Tip labels, matching the style introduced for Filings in feature 030. No behavioural changes."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Recognise Report Actions by Icon (Priority: P1)

A user opens the Reports page and sees the familiar action column at the far right of the DataGrid. Instead of text-labelled "View Filings" and "Delete" buttons, each action is represented by a compact icon. The user can immediately identify the purpose of each button because the icons follow widely understood conventions — a list/arrow icon for viewing related items and a trash icon for deletion. The page feels less cluttered, and the narrower action column leaves more horizontal space for data columns such as the report name and importer.

**Why this priority**: This is the core deliverable — replacing text buttons with icon buttons is the entire scope of the feature. Everything else depends on this change.

**Independent Test**: Can be fully tested by navigating to the Reports page with at least one report present and visually confirming that the action column contains two icon-only buttons (no visible text labels) rendered at a compact width.

**Acceptance Scenarios**:

1. **Given** the user is on the Reports page with one or more reports loaded, **When** the page renders, **Then** each report row displays exactly two icon buttons in the action column: a list/arrow icon and a trash icon — with no visible text labels.
2. **Given** the user is on the Reports page, **When** the user looks at the action column header area, **Then** the column width auto-sizes to fit the icon buttons only, leaving no excess whitespace that a text-labelled button would have occupied.

---

### User Story 2 — Discover Action Purpose via Tooltip (Priority: P1)

A user sees the icon buttons but is unsure of their exact meaning (particularly the "View Filings" icon). The user hovers over an icon button, and a tooltip appears describing the action: "View linked filings" for the list/arrow icon and "Delete report" for the trash icon. The user gains confidence in which button to press without trial-and-error.

**Why this priority**: Tooltips are the primary discoverability mechanism replacing the removed text labels. Without them, the icons become opaque — making this equal priority with the icon change itself.

**Independent Test**: Can be tested by hovering over each icon button and verifying the correct tooltip text appears within the standard system tooltip delay.

**Acceptance Scenarios**:

1. **Given** the Reports page displays a report row with icon action buttons, **When** the user hovers over the "View Filings" icon button, **Then** a tooltip reading "View linked filings" appears.
2. **Given** the Reports page displays a report row with icon action buttons, **When** the user hovers over the "Delete" icon button, **Then** a tooltip reading "Delete report" appears.

---

### User Story 3 — Distinguish Destructive Action by Visual Style (Priority: P2)

A user scanning the action column can immediately differentiate the safe "View Filings" action from the destructive "Delete" action. The delete icon button uses a red foreground to signal danger, consistent with the destructive-action styling used elsewhere in the application (e.g. the bulk-delete button on the same page). This visual cue helps prevent accidental deletions.

**Why this priority**: Important for user safety but secondary to the core icon/tooltip changes — the destructive style is an enhancement over the icons-alone change.

**Independent Test**: Can be tested by inspecting the delete icon button's foreground colour on a report row and confirming it renders in red while the "View Filings" button uses the default (non-red) foreground.

**Acceptance Scenarios**:

1. **Given** the Reports page displays a report row, **When** the user examines the delete icon button, **Then** the icon is rendered with a red foreground colour.
2. **Given** the Reports page displays a report row, **When** the user examines the "View Filings" icon button, **Then** the icon uses the standard (non-destructive) foreground colour.

---

### User Story 4 — Consistent Action Style Across Pages (Priority: P3)

A user who has used the Filings page (feature 030) navigates to the Reports page. The action column looks and behaves in the same pattern — compact icons with tooltips, destructive actions styled in red — creating a consistent cross-page experience. The user does not need to re-learn action patterns per page.

**Why this priority**: Consistency is a long-term UX quality goal. The pattern is established in feature 030; this feature simply applies it, so it is naturally satisfied when Stories 1–3 are complete.

**Independent Test**: Can be tested by navigating between the Filings page and the Reports page and comparing icon-button size, tooltip behaviour, and destructive-action styling for visual consistency.

**Acceptance Scenarios**:

1. **Given** the user has seen the Filings page's icon-only action buttons (from feature 030), **When** the user navigates to the Reports page, **Then** the action column uses the same icon-only button pattern (compact size, tooltip on hover, red destructive styling) so both pages feel visually unified.

---

### Edge Cases

- What happens when the Reports page has no reports (empty state)? The action column is simply not visible because no rows are rendered; the empty-state message displays as normal. No icon-related changes apply.
- What happens when a report row's "View Filings" or "Delete" command is disabled? The icon button renders in a disabled/dimmed state and does not respond to clicks, matching default button disabled behaviour. Tooltips should still appear on hover for discoverability.
- What happens when the user uses keyboard navigation to reach the action buttons? Icon buttons remain focusable via Tab and activatable via Enter/Space. Tooltips are not shown on keyboard focus (consistent with platform tooltip behaviour).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The "View Filings" text button in each Reports DataGrid row MUST be replaced with an icon-only button displaying a list/arrow icon.
- **FR-002**: The "Delete" text button in each Reports DataGrid row MUST be replaced with an icon-only button displaying a trash icon.
- **FR-003**: The "View Filings" icon button MUST display a tooltip with the text "View linked filings" on hover.
- **FR-004**: The "Delete" icon button MUST display a tooltip with the text "Delete report" on hover.
- **FR-005**: The "Delete" icon button MUST use a red foreground colour to indicate a destructive action.
- **FR-006**: The "View Filings" icon button MUST use the default (non-destructive) foreground colour.
- **FR-007**: The Actions column in the Reports DataGrid MUST auto-size its width to fit the icon buttons only, with no excess whitespace from the previously wider text-labelled buttons.
- **FR-008**: The "View Filings" icon button MUST retain the existing command binding and command parameter (report ID) — no change to navigation behaviour.
- **FR-009**: The "Delete" icon button MUST retain the existing command binding and command parameter (report ID) — no change to deletion behaviour.
- **FR-010**: Tooltip text for both actions MUST be defined as localised resource strings, consistent with the application's string-management approach.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Only the Desktop layer (view markup and resource strings) is impacted. No changes to Domain, Application, or Infrastructure layers. Clean Architecture boundaries remain intact — this is a purely cosmetic UI change.
- **CA-002 (Money and Dates)**: No monetary or date fields are affected. Not applicable.
- **CA-003 (Privacy and Security)**: No data storage or credential handling changes. Not applicable.
- **CA-004 (Network Scope)**: No outbound network calls introduced or modified. Not applicable.
- **CA-005 (Async and UI)**: No new I/O operations introduced. Icon buttons use the same synchronous command bindings as the existing text buttons. No UI-thread blocking risk.
- **CA-006 (Testing Impact)**: Desktop ViewModel tests are not impacted because command bindings remain unchanged. UI rendering tests (if present) for the Reports page should be updated to verify icon-only buttons render correctly and tooltips are bound.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of report rows display two icon-only action buttons (no visible text labels) in the action column.
- **SC-002**: Tooltip text appears within the standard system tooltip delay when hovering over each icon button, and matches the specified wording exactly ("View linked filings" and "Delete report").
- **SC-003**: The action column width is visibly narrower than before the change, leaving more horizontal space for data columns.
- **SC-004**: The delete icon button is visually distinguishable from the view icon button by its red foreground colour, identifiable without hovering.
- **SC-005**: All existing report-action functionality (navigate to filings, delete report) continues to work identically after the change — zero behavioural regressions.
- **SC-006**: The Reports page icon-button pattern is visually consistent with the Filings page icon-button pattern established in feature 030.

## Assumptions

- Feature 030 (Filings — Action Column Consolidation & Icon-Only Buttons) is completed before this feature begins, providing the icon resources (e.g. TrashIcon) and the established visual pattern to follow.
- The list/arrow icon for "View Filings" will reuse or closely match an existing icon resource (e.g. a navigation or list icon from the sidebar) rather than requiring new custom artwork.
- Icon size follows the convention established in feature 030 (16×16 logical pixels) for visual consistency.
- The existing `ViewFilingsCommand` and `DeleteCommand` on the Reports ViewModel do not change — only the view-layer button presentation is modified.
- The action column already uses `Width="Auto"` in the current markup; the narrower icon buttons will naturally cause it to shrink without explicit width changes.
- No right-to-left (RTL) layout considerations are needed for this change; the icon order (view then delete, left to right) matches the existing button order.
