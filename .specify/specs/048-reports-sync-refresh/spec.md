# Feature Specification: Reports Table Refresh After Sync

**Feature Branch**: `045-reports-sync-refresh`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Bug fix: When the user presses 'Sync Mailbox' on the Reports page, the sync operation completes successfully but the newly synced report does NOT appear in the table immediately. The user has to navigate away and come back to see the new report."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - New Reports Appear Immediately After Sync (Priority: P1)

As a user on the Reports page, I press "Sync Mailbox" to retrieve new reports from my mailbox. After the sync completes successfully, I expect to see any newly added reports appear in the reports table immediately, without needing to navigate away and back.

**Why this priority**: This is the core bug — the entire feature exists to fix this broken user expectation. Without this, users cannot trust that sync worked and must perform a manual workaround (navigating away and back).

**Independent Test**: Can be fully tested by triggering a sync that adds at least one new report and verifying the table updates in-place.

**Acceptance Scenarios**:

1. **Given** I am on the Reports page with 5 reports displayed, **When** I press "Sync Mailbox" and the sync retrieves 1 new report, **Then** the reports table shows 6 reports without any page navigation.
2. **Given** I am on the Reports page, **When** I press "Sync Mailbox" and the sync retrieves 3 new reports, **Then** all 3 new reports appear in the table immediately after sync completes.
3. **Given** I am on the Reports page, **When** I press "Sync Mailbox" and no new reports are found, **Then** the table remains unchanged and no errors are shown.

---

### User Story 2 - Refresh Preserves Current Sort and Filter State (Priority: P2)

As a user who has sorted or filtered the reports table (e.g., sorted by date descending, or filtered to a specific account), after sync completes and the table refreshes, the new reports should appear in the correct position according to my current sort/filter settings.

**Why this priority**: Users who actively work with sorted/filtered views would find it disorienting if their view state was lost after sync. This preserves workflow continuity.

**Independent Test**: Can be tested by applying a sort order, syncing, and verifying new reports slot into the correct sorted position.

**Acceptance Scenarios**:

1. **Given** the reports table is sorted by date descending, **When** sync adds a new report with today's date, **Then** the new report appears at or near the top of the table (per date order).
2. **Given** the reports table is filtered to show only a specific account, **When** sync adds reports for multiple accounts, **Then** only the reports matching the active filter appear in the visible table.
3. **Given** the reports table has a sort and filter applied, **When** sync completes, **Then** the sort and filter settings remain unchanged and active.

---

### User Story 3 - No Disruptive Full-Page Reload (Priority: P3)

As a user on the Reports page, when sync completes and the table refreshes, the experience should be seamless — just the data updates, not a full page reload or visible flicker.

**Why this priority**: A full page reload would be a poor user experience (scroll position lost, momentary blank screen). A data-only refresh is smoother and expected behavior.

**Independent Test**: Can be tested by observing that after sync, only the table data updates; the page layout, scroll context, and other UI elements remain stable.

**Acceptance Scenarios**:

1. **Given** I am on the Reports page, **When** sync completes and the table refreshes, **Then** no full-page reload occurs — only the table data is updated.
2. **Given** I have scrolled down in the reports table, **When** sync completes, **Then** my approximate scroll position is preserved (the page does not jump to the top).

---

### Edge Cases

- What happens if sync completes but the database query to refresh the list fails? The table should retain its previous data and an appropriate error should be shown.
- What happens if the user triggers a second sync while the first is still running? The existing behavior (command disablement during execution) should prevent this, and the refresh should occur only after the active sync completes.
- What happens if sync returns a report that is a duplicate of one already displayed? The refresh re-queries from the database, so deduplication is handled at the data layer — no duplicate rows should appear.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST re-query the reports list from the database after a successful sync operation completes.
- **FR-002**: System MUST update the displayed reports table with the re-queried data so that newly synced reports are visible immediately.
- **FR-003**: System MUST preserve the current sort order when refreshing the reports table after sync.
- **FR-004**: System MUST preserve the current filter state when refreshing the reports table after sync.
- **FR-005**: System MUST NOT perform a full page reload or navigation — only the data binding for the reports table should be refreshed.
- **FR-006**: System MUST handle the case where sync succeeds but the subsequent data refresh fails, by retaining the previously displayed data and showing an error indication.
- **FR-007**: System MUST display all newly synced reports (not just the first one) when multiple reports are added in a single sync operation.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: The fix impacts the Desktop layer (ViewModel) and potentially the Application layer (query handler). The sync command (Application layer) remains unchanged. The ViewModel must call the existing query to re-fetch reports after the sync command succeeds. Clean Architecture boundaries are preserved — no new cross-layer dependencies are introduced.
- **CA-002 (Money and Dates)**: No new monetary or date fields are introduced. Existing report date fields retain their current types.
- **CA-003 (Privacy and Security)**: No change to storage model. All data remains local-first. No secrets involved.
- **CA-004 (Network Scope)**: No new outbound calls. The sync operation already uses existing allowed endpoints (mailbox access). The refresh is a local database query only.
- **CA-005 (Async and UI)**: The re-query after sync must be async. The UI update must occur on the UI thread after the async query completes. No blocking operations should be introduced.
- **CA-006 (Testing Impact)**: Desktop (UI) tests needed for the ViewModel to verify that the reports collection is refreshed after the sync command completes. Unit tests may be needed if any Application-layer query logic changes. No Infrastructure or Domain changes expected.

### Key Entities

- **Report**: The primary entity displayed in the reports table. Key attributes include report date, associated account, and report content/type. The fix does not modify the entity — it ensures the displayed collection is refreshed after sync.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a successful sync that adds new reports, 100% of newly synced reports are visible in the table within 2 seconds of sync completion, without any user navigation.
- **SC-002**: Sort and filter settings are retained after refresh — the user's view state is identical before and after sync (except for the addition of new data).
- **SC-003**: No full-page reload or navigation event occurs during the refresh — the user remains on the Reports page with their scroll context preserved.
- **SC-004**: When sync adds zero new reports, the table remains unchanged with no visible flicker or disruption.

## Assumptions

- The existing sync command in the Application layer correctly persists newly synced reports to the database and returns a success result — the bug is only in the UI layer's failure to re-query after success.
- The existing query that populates the reports table on initial page load can be reused for the post-sync refresh — no new query is needed.
- The Reports page ViewModel already has access to the query mechanism (e.g., a mediator or query handler) used during initial load.
- The sync command's ReactiveCommand already disables itself during execution, preventing duplicate concurrent syncs.
- Sort and filter state is managed within the ViewModel or the DataGrid control and can be preserved across a data refresh.
