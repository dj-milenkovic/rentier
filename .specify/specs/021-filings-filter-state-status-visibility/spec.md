# Feature Specification: Filings Page Filter State & Status Visibility

**Feature Branch**: `001-filings-filter-status`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Fix the Filings page filtering and status visibility. Problems reported by QA: (1) Clicking All/Unpaid filter buttons produces no visible feedback — active filter is not highlighted and list does not clearly update. (2) Users cannot tell the current status of a filing row at a glance; they can only change it."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Active Filter Indication (Priority: P1)

As a user viewing the Filings page, I want to clearly see which filter ("All" or "Unpaid") is currently active so that I understand what subset of filings I am looking at.

Currently, clicking the "All" or "Unpaid" toggle buttons produces no visible change in the buttons themselves. Users cannot tell which filter is applied, leading to confusion about whether the list is showing all filings or only unpaid ones.

**Why this priority**: This is the most critical usability gap. Without knowing which filter is active, every other interaction on the page is undermined — users may act on incomplete data (e.g., thinking they see all filings when only unpaid ones are shown). This directly blocks trust in the filing list contents.

**Independent Test**: Can be fully tested by clicking each filter button on the Filings page and verifying that the selected button is visually distinguished from the unselected one, delivering confidence in which data subset is displayed.

**Acceptance Scenarios**:

1. **Given** the Filings page loads with the default "Unpaid" filter, **When** the user observes the filter buttons, **Then** the "Unpaid" button is visually highlighted (e.g., accent background or distinct border) and the "All" button appears in its default/unselected style.
2. **Given** the "Unpaid" filter is currently active, **When** the user clicks the "All" button, **Then** the "All" button becomes visually highlighted and the "Unpaid" button returns to its default/unselected style.
3. **Given** the "All" filter is currently active, **When** the user clicks the "Unpaid" button, **Then** the "Unpaid" button becomes highlighted and the "All" button returns to its default style.
4. **Given** either filter is active, **When** the user clicks the already-active filter button, **Then** the visual state does not change (the active button remains highlighted).

---

### User Story 2 - Read-Only Status Badge on Filing Rows (Priority: P1)

As a user scanning the filings list, I want to see a colour-coded status badge on each filing row showing the current status (Init, Filed, or Paid) so that I can assess the status of my filings at a glance without needing to interact with the status control.

Currently, the filing status is only visible through an editable dropdown. Users must inspect or interact with the dropdown to determine the status, which is slow and error-prone when scanning multiple rows.

**Why this priority**: Equal to P1 because this is the other half of the QA-reported problem. A status badge provides instant visual scanning capability — users can quickly triage which filings need attention by colour alone. This is essential for the primary workflow of managing tax filings.

**Independent Test**: Can be fully tested by loading the Filings page with filings in different statuses and verifying that each row displays a clearly visible, colour-coded, read-only badge with the correct human-readable label. Delivers immediate at-a-glance status awareness.

**Acceptance Scenarios**:

1. **Given** a filing row with status "Init", **When** the row is displayed, **Then** the row shows a read-only badge labelled "Init" (localised) with an amber/yellow colour scheme.
2. **Given** a filing row with status "Filed", **When** the row is displayed, **Then** the row shows a read-only badge labelled "Filed" (localised) with a blue colour scheme.
3. **Given** a filing row with status "Paid", **When** the row is displayed, **Then** the row shows a read-only badge labelled "Paid" (localised) with a green colour scheme.
4. **Given** a filing row with any status, **When** the user observes the badge, **Then** the badge is read-only and cannot be clicked, edited, or interacted with — it is purely informational.
5. **Given** the filing list contains rows in mixed statuses, **When** the user views the list, **Then** each row's badge colour is independently correct and visually distinct enough to differentiate at a glance.

---

### User Story 3 - Visible List Refresh on Filter Change (Priority: P2)

As a user switching between "All" and "Unpaid" filters, I want the filing list to visibly update so that I have confidence the displayed data matches my selected filter.

The underlying data loading already works correctly when the filter changes, but the visual transition is not obvious enough for users to notice the list has been refreshed, especially when the result set is similar.

**Why this priority**: While the data is already correct, the lack of visual feedback during filter transitions compounds the confusion from the missing active-filter indication (User Story 1). Once the filter highlight is in place (P1), this story adds a secondary layer of confidence.

**Independent Test**: Can be fully tested by toggling between filters and observing a visible change in the list (e.g., a brief loading indicator, a list fade/refresh animation, or at minimum the row count visibly changing).

**Acceptance Scenarios**:

1. **Given** the "Unpaid" filter is active showing a subset of filings, **When** the user switches to "All", **Then** the list visibly refreshes and additional rows (if any "Paid" filings exist) appear.
2. **Given** the "All" filter is active, **When** the user switches to "Unpaid", **Then** the list visibly refreshes and rows with "Paid" status are no longer displayed.
3. **Given** the user switches filters, **When** data is loading, **Then** a loading indicator is briefly visible to confirm the system is responding to the filter change.
4. **Given** a filter change results in zero matching filings, **When** the list finishes loading, **Then** an appropriate empty-state message is displayed (existing behaviour — confirm it still works).

---

### Edge Cases

- What happens when all filings are in "Paid" status and the user switches to "Unpaid"? The list should show the empty state with the appropriate message.
- What happens when there is only one filing? The badge should still render correctly with proper colour and label.
- What happens when the filing list is empty (no filings at all)? The filter buttons should still show the active state, and the empty-state message should be displayed regardless of which filter is selected.
- How does the status badge render with localised text that is longer or shorter than the English labels? The badge should accommodate varying text lengths without clipping or layout breakage.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST visually distinguish the currently active filter button ("All" or "Unpaid") from the inactive one using a clearly different visual treatment (e.g., accent background colour, distinct border, or contrasting text colour).
- **FR-002**: The system MUST update the active filter button's visual state immediately when the user clicks a different filter button.
- **FR-003**: Only one filter button MUST be visually active at any given time — selecting one deselects the other.
- **FR-004**: Each filing row MUST display a read-only status badge showing the current filing status in human-readable, localised text (using existing localisation strings).
- **FR-005**: The status badge MUST use a distinct colour for each status: amber/yellow for Init, blue for Filed, and green for Paid.
- **FR-006**: The status badge MUST be non-interactive (read-only) — users cannot click or modify the badge. Status changes remain available only through the existing dropdown control.
- **FR-007**: The status badge MUST be visually rendered as a pill or coloured tag (compact, rounded shape with coloured background and contrasting text).
- **FR-008**: The filing list MUST visibly refresh when the filter changes, providing clear feedback that the displayed data has updated (e.g., loading indicator during the transition, or a visible re-render of rows).
- **FR-009**: The filter button active-state, status badge colours, and status labels MUST be consistent across all pages of the filing list (pagination should not affect visual behaviour).
- **FR-010**: Status badge labels MUST use the existing localised resource strings (FilingStatus_Init, FilingStatus_Filed, FilingStatus_Paid) and must not introduce new hardcoded text.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Only the Desktop (presentation) layer is impacted. Changes are confined to Views, ViewModels, and Converters. No modifications to Domain, Application, or Infrastructure layers. Clean Architecture boundaries remain intact — no business logic is introduced in the presentation layer.
- **CA-002 (Money and Dates)**: No monetary or date fields are added or modified by this feature. Existing `TaxPayable` (decimal) and `FilingDeadline` (DateOnly) display properties remain unchanged.
- **CA-003 (Privacy and Security)**: No data storage changes. No new user data is collected, persisted, or transmitted. All changes are purely visual rendering of existing in-memory data.
- **CA-004 (Network Scope)**: No outbound network calls are added. The existing filing data loading mechanism is unchanged.
- **CA-005 (Async and UI)**: No new I/O operations are introduced. The filter toggle already triggers an async page reload. Badge rendering is synchronous property binding with no blocking operations.
- **CA-006 (Testing Impact)**: Desktop ViewModel tests should verify new computed display properties (status display text and colour mapping). No Domain, Application, or Infrastructure test changes are needed.

### Key Entities *(include if feature involves data)*

- **Filing**: Existing entity with a `FilingStatus` field (Init, Filed, Paid). No structural changes — the badge reads the existing status value.
- **FilingStatus**: Existing enumeration (Init=0, Filed=1, Paid=2). Used as the source for both the badge label (via localised strings) and badge colour mapping.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can identify which filter is active within 1 second of looking at the Filings page, without needing to click or hover.
- **SC-002**: Users can determine the status of any filing row within 1 second by scanning the status badge, without interacting with any control.
- **SC-003**: 100% of filing rows display the correct status badge with the appropriate colour and label matching their current filing status.
- **SC-004**: Switching between "All" and "Unpaid" filters produces a visible change in the page within 2 seconds (including loading feedback), confirming the filter was applied.
- **SC-005**: QA regression: the two originally reported issues (no filter active state, no at-a-glance status) are fully resolved and do not recur.

## Assumptions

- The existing `ShowAll` boolean property on FilingsViewModel correctly drives the filter logic and triggers data reload — no backend or query changes are needed.
- The existing `FilingStatusDisplayConverter` and `FilingStatusExtensions.ToDisplayString()` provide the correct localised labels and can be reused or extended for the badge.
- The Avalonia FluentTheme provides sufficient built-in styling capabilities (accent colours, control templates) to achieve the active-state toggle styling without introducing a custom theme.
- Badge colour values (amber for Init, blue for Filed, green for Paid) are chosen for adequate contrast against both light and dark theme backgrounds. Exact colour values will be determined during implementation to meet accessibility contrast ratios.
- The existing editable status dropdown (ComboBox) on each filing row remains as-is — the new badge supplements it, it does not replace it.
- No database schema, API, or domain logic changes are needed — this feature is entirely within the presentation layer.
