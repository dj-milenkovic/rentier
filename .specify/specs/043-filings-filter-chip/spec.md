# Feature Specification: Filings Per-Report Filter Chip

**Feature Branch**: `feature/043-filings-filter-chip`  
**Created**: 2025-07-22  
**Status**: Draft  
**Input**: User description: "When the user navigates to the Filings page from the Reports page (via 'View Filings'), a ReportIdFilter is applied. Previously this filter was invisible and irremovable. Now a dismissible chip ('Filtered by report ✕') appears in the filter bar whenever a report filter is active; clicking ✕ clears the filter and reloads all filings."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Visible Report Filter Indication (Priority: P1)

As a user who navigated to the Filings page from the Reports page via "View Filings", I want to see a clear visual indicator that the list is currently filtered to a specific report, so that I understand why only a subset of filings is displayed.

Previously, clicking "View Filings" on a report applied a hidden filter — the Filings page showed only that report's filings, but nothing on the page communicated this. Users could mistakenly believe they were seeing all filings, leading to confusion and potential errors (e.g., thinking filings were missing).

**Why this priority**: Without visibility of the active report filter, users cannot trust the data on screen. This is the foundational usability gap — every other interaction on the filtered Filings page is undermined if the user does not know a filter is applied.

**Independent Test**: Can be fully tested by navigating from the Reports page via "View Filings" and verifying that a dismissible chip appears in the filter bar, delivering immediate clarity about the active filter context.

**Acceptance Scenarios**:

1. **Given** the user is on the Reports page viewing a list of reports, **When** the user clicks "View Filings" on a specific report, **Then** the Filings page loads with a visible chip in the filter bar reading "Filtered by report ✕" (or a localized equivalent).
2. **Given** the Filings page is displayed with a report filter active, **When** the user observes the filter bar, **Then** the chip is visually distinct from surrounding controls (e.g., pill-shaped, contrasting background) and clearly communicates that a report-level filter is in effect.
3. **Given** the Filings page is displayed without any report filter (normal navigation from sidebar), **When** the user observes the filter bar, **Then** no report filter chip is visible.
4. **Given** the user navigates away from the Filings page and back (via sidebar), **When** the report filter was previously cleared, **Then** the chip does not reappear.

---

### User Story 2 - Dismiss Report Filter (Priority: P1)

As a user viewing report-filtered filings, I want to dismiss the report filter by clicking the ✕ on the chip, so that I can quickly return to viewing all filings without navigating away.

Currently, there is no way to remove the report filter from the Filings page once it has been applied. The user must navigate away and return via the sidebar to see all filings.

**Why this priority**: This is equally critical to Story 1 — showing a filter without the ability to remove it would create a dead end. Together, visibility and dismissal complete the user's control over the report filter.

**Independent Test**: Can be fully tested by navigating from Reports via "View Filings", then clicking the ✕ button on the chip, and verifying the full unfiltered filing list loads.

**Acceptance Scenarios**:

1. **Given** the Filings page displays a report filter chip, **When** the user clicks the ✕ (dismiss) button on the chip, **Then** the report filter is cleared, the chip disappears, and the page reloads showing all filings (respecting the current All/Unpaid toggle and sort order).
2. **Given** the user dismisses the report filter chip, **When** the page reloads, **Then** the pagination resets to page 1 and the total count reflects all filings (not just those from the previously filtered report).
3. **Given** the user dismisses the report filter chip, **When** the user subsequently navigates from Reports via "View Filings" on a different report, **Then** a new chip appears for the newly filtered report.

---

### Edge Cases

- What happens if the report that was used for filtering has been deleted? The chip should still appear (the filter is based on a report ID, not report existence), and dismissing it should still work. The filings list may show zero results with the empty-state message.
- What happens if the user toggles between All/Unpaid while a report filter is active? The report filter chip should remain visible; the All/Unpaid filter and report filter should compose together.
- What happens if the user changes the sort order while a report filter is active? The chip should remain visible; sorting and report filtering are independent.
- What happens if the user paginates while a report filter is active? The chip should remain visible across page changes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a dismissible chip element in the filter bar of the Filings page whenever a report-level filter is active.
- **FR-002**: The chip MUST contain descriptive text (e.g., "Filtered by report") and a visible dismiss (✕) control.
- **FR-003**: The chip MUST only be visible when a report filter is currently applied; it MUST NOT appear when no report filter is active.
- **FR-004**: Clicking the dismiss control on the chip MUST clear the report filter, remove the chip from the UI, and trigger a reload of the filings list showing all filings.
- **FR-005**: Dismissing the report filter MUST reset pagination to page 1.
- **FR-006**: The report filter chip MUST coexist with the existing All/Unpaid filter toggle — both filters compose together when both are active.
- **FR-007**: The report filter chip MUST persist across pagination, sort changes, and All/Unpaid toggle changes as long as the report filter remains active.
- **FR-008**: The chip text MUST be localizable (support the application's existing localization system).
- **FR-009**: The dismiss control MUST have an accessible name for screen readers and keyboard navigation support.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature impacts the Desktop layer (ViewModel + View). The `FilingsViewModel` already owns the `ReportIdFilter` property; a new reactive command and a computed visibility property will be added. No Application or Domain changes are required. Clean Architecture boundaries remain valid — no business logic moves into the Desktop layer.
- **CA-002 (Money and Dates)**: No monetary or date fields are introduced or modified.
- **CA-003 (Privacy and Security)**: No privacy or security concerns — the report ID filter is an in-memory UI state parameter, not persisted or transmitted externally.
- **CA-004 (Network Scope)**: No new outbound network calls. The existing `GetFilingsQuery` handler is reused with the filter parameter set to null on dismissal.
- **CA-005 (Async and UI)**: The dismiss action sets a ViewModel property which triggers an existing async load pipeline (`LoadPageCommand`). No blocking operations are introduced.
- **CA-006 (Testing Impact)**: Desktop ViewModel tests must be updated to cover chip visibility and dismiss behavior. Headless UI tests should verify chip rendering and dismiss interaction.

### Key Entities *(include if feature involves data)*

- **ReportIdFilter (UI state)**: An optional identifier (GUID) on the Filings page ViewModel that, when set, restricts the displayed filings to those belonging to a specific report. The chip visualizes and provides dismissal of this state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can identify that a report-level filter is active within 2 seconds of the Filings page loading after navigating from Reports.
- **SC-002**: Users can dismiss the report filter and return to the full filings list in a single click (no multi-step navigation required).
- **SC-003**: The chip is never visible when no report filter is active — zero false-positive appearances across all navigation paths to the Filings page (sidebar, dashboard, post-sync, manual filing creation).
- **SC-004**: All existing Filings page functionality (pagination, sorting, All/Unpaid toggle, bulk delete, export, status advancement) continues to work correctly both with and without the report filter chip visible.
- **SC-005**: Chip text is available in all supported application languages.

## Assumptions

- The existing `ReportIdFilter` property on `FilingsViewModel` and its reactive subscription pipeline (which triggers `LoadPageCommand` on change) will be reused. No new query or handler is needed.
- The chip will be placed in the existing filter toggle bar (the `DockPanel` containing the All/Unpaid radio buttons and sort indicator) to maintain visual grouping of filter controls.
- The chip text will be a short label ("Filtered by report") without displaying the report name or ID, since report names may be long and the user already knows which report they came from. If report name display is desired in the future, it can be added as an enhancement.
- The dismiss action is equivalent to setting `ReportIdFilter = null`, which the existing reactive pipeline already handles by reloading the page.
- Keyboard accessibility follows standard Avalonia Button behavior — the ✕ control will be a focusable Button element inheriting standard keyboard interaction (Tab to focus, Enter/Space to activate).
