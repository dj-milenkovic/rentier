# Feature Specification: Sync Replay Controls

**Feature Branch**: `feature/018-sync-replay-controls`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Improve sync UX and behavior around Start date and Start sync. Implement sync modes: Incremental sync (from cursor), Replay from date (user-provided DateOnly), Full replay for selected importer/mailbox. Consider whether Start Date field on mailbox settings should be removed."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Incremental Sync (Default Behavior) (Priority: P1)

As a user, I want to run an incremental sync that picks up only new emails since my last sync, so that I can quickly stay up to date without reprocessing old data.

**Why this priority**: This is the existing core sync behavior. It must remain the default and work reliably as the foundation for all other sync modes.

**Independent Test**: Can be fully tested by configuring a mailbox with importers, running sync twice, and confirming the second run only fetches messages delivered after the first sync's cursor position.

**Acceptance Scenarios**:

1. **Given** a mailbox with a cursor pointing to a previous sync position, **When** the user triggers sync without changing any mode, **Then** only emails newer than the cursor position are fetched and processed.
2. **Given** a mailbox that has never been synced, **When** the user triggers sync for the first time, **Then** emails are fetched from the mailbox's configured initial date onward and the cursor is set after completion.
3. **Given** a sync completes successfully, **When** the cursor is updated, **Then** the new cursor position (date and message identifier) is persisted and logged.
4. **Given** a sync encounters a connection failure mid-run, **When** the sync is interrupted, **Then** the cursor is NOT advanced and the user can retry without data loss.

---

### User Story 2 - Replay from Date (Priority: P2)

As a user, I want to replay sync from a specific date I choose, so that I can reprocess emails from a known point in time (e.g., after fixing an importer configuration or correcting a parsing bug).

**Why this priority**: This is the most common replay scenario — users need to go back to a specific date to pick up missed or incorrectly processed reports. It delivers the highest value of the new sync modes.

**Independent Test**: Can be tested by running incremental sync first, then choosing "Replay from date" with a past date and a duplicate handling strategy, confirming emails from that date onward are fetched and handled per the selected strategy.

**Acceptance Scenarios**:

1. **Given** a mailbox with an existing cursor, **When** the user selects "Replay from date" and provides a valid date, **Then** the system fetches all emails from that date onward, regardless of the current cursor position.
2. **Given** a user selects "Replay from date", **When** they choose a date, **Then** the system displays the expected impact (estimated email count range and affected importers) before execution.
3. **Given** a replay-from-date sync completes successfully, **When** the cursor is updated, **Then** the cursor is advanced to reflect the latest message processed (not reset to the replay date).
4. **Given** a user provides a replay date in the future, **When** validating the input, **Then** the system rejects the date with a clear error message.

---

### User Story 3 - Duplicate Handling Strategy Selection (Priority: P2)

As a user, I want to choose how duplicates are handled during replay, so that I can decide whether to skip already-processed reports, create new filing revisions, or safely reprocess them in place.

**Why this priority**: Without explicit duplicate handling, replay modes would either silently skip everything (defeating the purpose) or create unwanted duplicates. This is essential for replay modes to be useful.

**Independent Test**: Can be tested by running a normal sync, then replaying with each duplicate strategy and verifying the outcome: skipped reports show as skipped, new revisions are created alongside originals, and reprocess-in-place updates existing records.

**Acceptance Scenarios**:

1. **Given** a replay sync encounters a report that already exists, **When** the user has selected "Skip existing", **Then** the duplicate is skipped and logged as "already exists — skipped".
2. **Given** a replay sync encounters a report that already exists, **When** the user has selected "Create new revision", **Then** a new filing revision is created linked to the same report source, preserving the original.
3. **Given** a replay sync encounters a report that already exists, **When** the user has selected "Reprocess in place", **Then** the existing report and its filings are updated with the newly parsed data, only when doing so is safe (no downstream dependencies like exports that reference the original filing).
4. **Given** the user has not explicitly selected a duplicate strategy during replay, **When** the replay begins, **Then** the system defaults to "Skip existing" (safest option) and displays this choice prominently.
5. **Given** "Reprocess in place" is selected but a filing has already been exported, **When** the system encounters this conflict, **Then** it falls back to "Create new revision" for that specific filing and warns the user.

---

### User Story 4 - Full Replay for Selected Importer or Mailbox (Priority: P3)

As a user, I want to clear the cursor and replay everything from the beginning for a specific importer or an entire mailbox, so that I can fully rebuild my data after major configuration changes.

**Why this priority**: Full replay is a power-user operation needed less frequently, but critical when importer configuration has been fundamentally changed. It builds on the date replay and duplicate handling infrastructure.

**Independent Test**: Can be tested by syncing a mailbox, then selecting "Full replay" for a specific importer, confirming the cursor is temporarily overridden (not destroyed) and all historical emails are reprocessed according to the chosen duplicate strategy.

**Acceptance Scenarios**:

1. **Given** an importer with a linked mailbox, **When** the user selects "Full replay" for that importer, **Then** the system replays all emails matching that importer's filters from the beginning of time, without affecting other importers on the same mailbox.
2. **Given** a mailbox with multiple importers, **When** the user selects "Full replay" for the entire mailbox, **Then** all importers on that mailbox are replayed from the beginning.
3. **Given** a full replay is requested, **When** the system prepares to execute, **Then** it displays a confirmation dialog showing the mailbox/importer name, "replaying from: beginning", selected duplicate strategy, and a warning about potential processing time.
4. **Given** a full replay completes, **When** the cursor is updated, **Then** the cursor is advanced to the latest message processed (never left at the beginning).

---

### User Story 5 - Sync Mode Selection UI (Priority: P2)

As a user, I want a clear interface that shows me which sync mode is selected, what the expected impact will be, and any relevant warnings, so that I can make informed decisions before starting sync.

**Why this priority**: The UI is essential for users to access replay modes safely. Without clear mode selection, impact preview, and warnings, users risk unintended data reprocessing.

**Independent Test**: Can be tested by navigating to the sync screen and verifying that mode selection controls are visible, that changing modes updates the impact preview, and that warnings appear for destructive operations.

**Acceptance Scenarios**:

1. **Given** the user opens the sync screen, **When** the page loads, **Then** "Incremental sync" is selected by default with a description: "Sync new emails since last sync".
2. **Given** the user selects "Replay from date", **When** the mode changes, **Then** a date picker appears and a duplicate strategy selector becomes visible.
3. **Given** the user selects "Full replay", **When** the mode changes, **Then** a scope selector (specific importer or entire mailbox) appears, a duplicate strategy selector becomes visible, and a warning banner appears explaining this will replay all historical emails.
4. **Given** any non-incremental mode is selected, **When** the user reviews the impact panel, **Then** the panel shows: selected mode, target scope, duplicate strategy, and estimated processing scope.
5. **Given** the user triggers sync in any mode, **When** sync is running, **Then** the progress log shows the active mode, cursor transitions, and per-report duplicate handling decisions.

---

### User Story 6 - Remove Start Date from Mailbox Settings (Priority: P3)

As a user, I no longer need a "Start Date" (Initial Sync Date) field on mailbox settings because the replay-from-date mode provides the same capability more flexibly and without the confusion of a persistent start date that may conflict with cursor-based sync.

**Why this priority**: Removing this field simplifies the mailbox configuration UI, eliminates a source of user confusion (why does changing Start Date not affect sync after the first run?), and avoids conflicting semantics with the new replay modes. However, it requires a migration path for existing data.

**Independent Test**: Can be tested by verifying the mailbox settings form no longer shows a date field, that existing mailboxes continue to function (their initial sync date is migrated to the cursor if needed), and that first-time mailbox sync uses a sensible default.

**Acceptance Scenarios**:

1. **Given** the mailbox settings screen, **When** adding or editing a mailbox, **Then** there is no "Initial Sync Date" / "Start Date" field visible.
2. **Given** an existing mailbox that has a configured Initial Sync Date but has never been synced, **When** the system migrates, **Then** the Initial Sync Date value is preserved internally as the starting point for the first incremental sync.
3. **Given** a new mailbox is created without a start date, **When** the first sync runs, **Then** the system uses a sensible default (e.g., 90 days ago) as the starting point.
4. **Given** a user wants to sync from a specific date on a new mailbox, **When** they set up the mailbox and run the first sync, **Then** they can use "Replay from date" to override the default starting point.

---

### Edge Cases

- What happens when a replay-from-date sync is cancelled mid-run? The cursor must NOT be advanced past the last fully-processed message.
- What happens when a full replay encounters emails that no longer exist on the mail server (deleted/expunged)? The system processes only what is available; previously imported reports remain untouched.
- What happens when two importers on the same mailbox match the same email? Each importer processes independently; the cursor is shared at the mailbox level but duplicate detection is per-importer.
- What happens when "Reprocess in place" is selected for a report whose filing has downstream exports? The system falls back to "Create new revision" for that specific filing.
- What happens when the user changes duplicate strategy mid-sync? Strategy is locked at sync start and cannot be changed while sync is running.
- What happens when a mailbox has no importers? The sync skips that mailbox and logs an informational message.
- What happens when the replay date is before the oldest available email on the server? The system processes whatever is available from the server without error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support three sync modes: Incremental (default), Replay from Date, and Full Replay.
- **FR-002**: System MUST default to Incremental sync mode when the user opens the sync screen.
- **FR-003**: System MUST allow the user to select a DateOnly value when using Replay from Date mode.
- **FR-004**: System MUST validate that the replay date is not in the future.
- **FR-005**: System MUST allow Full Replay to be scoped to a specific importer or to an entire mailbox.
- **FR-006**: System MUST present three duplicate handling strategies: Skip Existing, Create New Revision, and Reprocess in Place.
- **FR-007**: System MUST default to "Skip Existing" as the duplicate handling strategy for all replay modes.
- **FR-008**: System MUST skip duplicate detection entirely for Incremental sync (cursor-based filtering already prevents duplicates).
- **FR-009**: System MUST NOT require any destructive database operations (deletes, truncates) to perform a replay.
- **FR-010**: System MUST update the cursor deterministically after sync completion — always advancing to the latest processed message, never regressing.
- **FR-011**: System MUST log every cursor transition with before/after values.
- **FR-012**: System MUST NOT advance the cursor if the sync is cancelled or fails mid-run.
- **FR-013**: System MUST display the selected sync mode, duplicate strategy, and expected impact before execution begins.
- **FR-014**: System MUST show a confirmation dialog for Full Replay operations with a warning about processing scope.
- **FR-015**: System MUST log duplicate handling decisions per report during replay (e.g., "Report X — skipped, already exists" or "Report X — new revision created").
- **FR-016**: System MUST fall back from "Reprocess in Place" to "Create New Revision" when a filing has been exported, and warn the user.
- **FR-017**: System MUST remove the "Initial Sync Date" field from the mailbox settings user interface.
- **FR-018**: System MUST preserve existing Initial Sync Date values for mailboxes that have not yet been synced, using them as the starting point for the first incremental sync.
- **FR-019**: System MUST use a default starting point (90 days before mailbox creation) for new mailboxes created without a start date.
- **FR-020**: System MUST allow the sync operation to be cancelled at any point, cleanly stopping after the current message.
- **FR-021**: System MUST support per-importer Full Replay without affecting the cursor of other importers sharing the same mailbox.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Sync modes and duplicate strategies are modeled as commands/queries in `Rentier.Application`. Sync pipeline logic remains in `Rentier.Infrastructure`. UI controls and ViewModel additions are in `Rentier.Desktop`. Domain entities (`MailboxCursor`, `Report`) may gain new value objects or status fields in `Rentier.Domain`. Clean Architecture boundaries are preserved — no infrastructure concerns leak into Domain or Application layers.
- **CA-002 (Money and Dates)**: All replay dates use `DateOnly`. No monetary values are directly affected. Cursor's `LastSyncDate` remains `DateOnly?`. No `DateTime` usage introduced.
- **CA-003 (Privacy and Security)**: No new sensitive data stored. Cursor values and sync logs are local-only. No credential changes. No PII involved in sync mode selection.
- **CA-004 (Network Scope)**: No new outbound calls. Sync continues to use existing IMAP connections to configured mail servers only.
- **CA-005 (Async and UI)**: All sync operations remain async. New UI controls (mode selector, date picker, strategy selector) follow the project's reactive command pattern. Progress reporting uses the existing progress reporting mechanism. UI follows standard ViewModel property patterns (`IsLoading`/`ErrorMessage`).
- **CA-006 (Testing Impact)**: Domain tests for new value objects (sync mode, duplicate strategy). Application tests for command handlers with mode/strategy parameters, cursor transition logic, and duplicate handling decisions. Infrastructure tests for cursor persistence and IMAP query building per sync mode. Desktop tests for ViewModel mode selection, validation, and UI state transitions.

### Key Entities

- **SyncMode**: Represents the three sync execution modes — Incremental, Replay from Date, and Full Replay. Each mode determines how the IMAP search query is constructed and whether duplicate handling applies.
- **DuplicateStrategy**: Represents the three duplicate handling behaviors — Skip Existing (idempotent), Create New Revision (additive), and Reprocess in Place (update). Determines what happens when a sync encounters an already-imported report.
- **MailboxCursor**: Existing value object tracking sync position (`LastSyncDate`, `LastUid`). Extended conceptually to support temporary override during replay (cursor is not mutated until sync completes successfully).
- **Report**: Existing entity. Gains awareness of revision lineage — a replayed report may reference an original report it supersedes.
- **FilingRevision**: Represents a new version of a filing created by the "Create New Revision" duplicate strategy. Links to the original filing and the new report data.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can complete a replay-from-date sync in the same number of steps as a normal sync (mode selection + start), with no more than one additional confirmation step.
- **SC-002**: 100% of cursor transitions are logged with before/after values, verifiable via the sync progress log.
- **SC-003**: Interrupted syncs (cancellation or failure) never advance the cursor, verified by cursor value comparison before and after interrupted operations.
- **SC-004**: Users can distinguish between sync modes at a glance — the selected mode, scope, and duplicate strategy are visible on the sync screen without scrolling.
- **SC-005**: Skip Existing replay produces identical database state to having done nothing (idempotent), verified by comparing filing counts and values before and after.
- **SC-006**: Create New Revision replay preserves all original filings and adds new revisions, verified by original filing count remaining unchanged.
- **SC-007**: Full replay for a single importer does not affect reports or filings from other importers on the same mailbox.
- **SC-008**: All sync mode, duplicate strategy, and cursor transition tests achieve 100% pass rate.
- **SC-009**: The mailbox settings form is simplified — one fewer field compared to the current form — and existing mailboxes continue to function without user intervention.

## Assumptions

- Users have a stable internet connection to their IMAP mail server during sync operations (as with existing sync).
- The IMAP server retains historical emails for replay — if emails have been deleted server-side, they cannot be replayed and the system processes only what is available.
- "Reprocess in Place" safety is determined by whether a filing has been exported (e.g., to XML). If no export has occurred, reprocessing in place is considered safe.
- The default starting point for new mailboxes (90 days before creation) is a reasonable default that balances completeness with performance. Users who need older data can use Replay from Date immediately after setup.
- Filing revision tracking is append-only — original filings are never modified or deleted by the replay system.
- The sync progress log is the primary mechanism for users to audit cursor transitions and duplicate handling decisions; no separate audit log is introduced.
- Mobile or remote access to the sync feature is out of scope — this is a desktop application feature only.
- Concurrent sync operations on the same mailbox are not supported — the existing single-sync-at-a-time model is maintained.
