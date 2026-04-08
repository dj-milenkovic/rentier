# Feature Specification: One-Click Sync Workflow

**Feature Branch**: `015-one-click-sync-workflow`
**Created**: 2025-07-14
**Status**: Draft
**Input**: User description: "Implement the one-click sync workflow for Rentier. Add a dedicated Sync pane accessible from the sidebar (between Reports and Settings). When the sync button is clicked: runs mailbox sync for all configured mailboxes/importers, then processes all new Init reports, showing real-time progress with cancel support and error summary."

## Clarifications

### Session 2025-07-14

- Q: What are the exact C# record definitions for `SyncAllCommand` and the new `SyncAllResult` DTO? → A: `public sealed record SyncAllCommand();` (no parameters — targets all mailboxes); `public sealed record SyncAllResult(int MailboxesSynced, int AttachmentsDownloaded, int ReportsProcessed, int FilingsCreated, IReadOnlyList<string> Errors);`
- Q: What is the exact shape of the new progress DTO, and how does it differ from the existing `SyncProgress`? → A: New `public sealed record SyncProgressEntry(DateTimeOffset Timestamp, string Message, SyncProgressSeverity Severity);` with `public enum SyncProgressSeverity { Info, Warning, Error }` placed in `src/Rentier.Application/DTOs/`; the existing `SyncProgress(int Total, int Processed, string? CurrentFile, bool IsComplete)` is **unchanged**
- Q: What interface does `SyncAllCommandHandler` implement, and how does it pass progress into the existing `SyncMailboxCommandHandler`? → A: Dedicated `ISyncAllCommandHandler` with signature `Task<Result<SyncAllResult, Error>> HandleAsync(SyncAllCommand command, IProgress<SyncProgressEntry> progress, CancellationToken ct)`; internally creates `new Progress<SyncProgress>(p => /* convert → SyncProgressEntry */)` and injects it via `new SyncMailboxCommand(internalProgress)` — neither existing handler is modified; does NOT implement `ICommandHandler<TCmd, TResult>`
- Q: How is `SyncViewModel` wired in `MainWindowViewModel`, and what delegate does it receive for auto-navigation? → A: Created via `ActivatorUtilities.CreateInstance<SyncViewModel>(provider, navigateToFilings)` where `navigateToFilings` is `Action` (no Guid — no report-ID filter needed); the delegate sets `SelectedEntry` to the Filings `NavigationEntry`; Sync entry is inserted between Reports and Settings in the `NavigationEntries` list
- Q: Does the feature require a new EF Core migration? → A: No — `SyncAllCommandHandler` is orchestration-only; all DB writes are delegated to the existing `SyncMailboxCommandHandler` and `ProcessReportsCommandHandler`; no new schema or migration

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Run Full Sync (Priority: P1)

A Rentier user opens the Sync pane from the sidebar and clicks the "Sync" button. The system first synchronises all configured mailboxes (downloading new emails and attachments), then processes all newly imported Init-status reports to create filings. A real-time progress log shows each step as it happens: connecting to mailboxes, discovering emails, parsing reports, and creating filings. When the sync completes successfully and at least one filing was created, the application automatically navigates the user to the Filings pane.

**Why this priority**: This is the core value proposition — consolidating a multi-step manual workflow (sync mailboxes, then process reports) into a single action. Without this, the feature has no purpose.

**Independent Test**: Can be fully tested by configuring at least one mailbox/importer, clicking Sync, and verifying that emails are downloaded, reports are processed, filings are created, progress entries appear in the log, and the app navigates to the Filings pane.

**Acceptance Scenarios**:

1. **Given** at least one mailbox and importer are configured, **When** the user clicks Sync, **Then** the system downloads new emails from all configured mailboxes, processes all Init-status reports, displays progress entries in real time, and shows a completion summary with counts of reports synced, reports processed, and filings created.
2. **Given** the sync completes successfully with at least one filing created and no errors, **When** the completion summary is displayed, **Then** the application automatically navigates to the Filings pane.
3. **Given** the sync completes successfully but zero filings were created, **When** the completion summary is displayed, **Then** the application stays on the Sync pane (no auto-navigation).
4. **Given** no mailboxes or importers are configured, **When** the user clicks Sync, **Then** the progress log shows an informational message indicating no mailboxes are configured and the sync completes immediately.

---

### User Story 2 — View Real-Time Progress (Priority: P1)

While the sync is running, the user sees a scrolling progress log displaying structured entries. Each entry includes a timestamp, a descriptive message, and a severity-based visual indicator (icon and colour). Progress entries cover the full lifecycle: mailbox connections, email discovery counts, report parsing steps, filing creation, warnings, and errors.

**Why this priority**: Real-time feedback is essential for users to understand what the system is doing during a potentially long-running operation. Without progress visibility, users cannot distinguish between "working" and "stuck."

**Independent Test**: Can be tested by initiating a sync and verifying that progress entries appear incrementally (not all at once), each entry has a timestamp, message, and severity icon, and the log auto-scrolls to show the latest entry.

**Acceptance Scenarios**:

1. **Given** a sync is in progress, **When** the mailbox sync step begins, **Then** the progress log displays entries such as "Connecting to mailbox…", "Found N emails", and "Downloading attachment X".
2. **Given** a sync is in progress and the report processing step begins, **When** each report is processed, **Then** the progress log displays entries such as "Parsing report X…" and "Created N filings".
3. **Given** a sync encounters a non-fatal issue (e.g., one mailbox unreachable but others succeed), **When** the issue occurs, **Then** a warning-severity entry appears in the progress log and sync continues with remaining mailboxes.
4. **Given** multiple progress entries exist, **When** new entries are added, **Then** the log automatically scrolls to display the most recent entry.

---

### User Story 3 — Cancel a Running Sync (Priority: P2)

While the sync is running, the user can click a Cancel button to abort the operation. Cancellation stops the sync at the earliest safe point, and the progress log shows a final entry indicating the sync was cancelled. Any work completed before cancellation (e.g., emails already downloaded, reports already processed) is retained.

**Why this priority**: Cancellation provides user control over long-running operations. It is important for usability but secondary to the core sync functionality.

**Independent Test**: Can be tested by starting a sync, clicking Cancel during execution, and verifying that the sync stops, the progress log shows a cancellation message, and previously completed work is preserved in the database.

**Acceptance Scenarios**:

1. **Given** a sync is in progress, **When** the user clicks Cancel, **Then** the sync operation stops at the earliest safe point and the progress log shows "Sync cancelled by user" as an informational entry.
2. **Given** a sync is in progress and the user has not clicked Cancel, **When** the Sync button area is observed, **Then** the Cancel button is visible and enabled, and the Sync button is disabled.
3. **Given** a sync has been cancelled, **When** the cancellation completes, **Then** any emails already downloaded and reports already processed before cancellation are retained (not rolled back), and the Sync button becomes enabled again.

---

### User Story 4 — Review Error Summary (Priority: P2)

When a sync completes (whether fully or partially), the user sees a summary at the end of the progress log showing what succeeded and what failed. If any errors occurred, an error summary section lists each error with enough detail for the user to understand what went wrong. The user can then decide to re-run the sync or investigate specific errors.

**Why this priority**: Error visibility is critical for a reliable workflow, but only relevant when errors actually occur — hence P2 behind the core flow.

**Independent Test**: Can be tested by simulating sync errors (e.g., invalid mailbox credentials, malformed report file), running sync, and verifying that the error summary displays at the end with specific error details.

**Acceptance Scenarios**:

1. **Given** a sync completes with errors, **When** the completion summary is displayed, **Then** the progress log includes an error summary section listing each error with a descriptive message.
2. **Given** a sync completes with both successes and errors (partial success), **When** the completion summary is displayed, **Then** it shows counts of successful operations alongside the error list, and the application does not auto-navigate to Filings.
3. **Given** a sync completes with no errors, **When** the completion summary is displayed, **Then** no error summary section is shown, only the success summary with counts.

---

### User Story 5 — Navigate to Sync Pane (Priority: P3)

The user sees a "Sync" entry in the sidebar navigation, positioned between "Reports" and "Settings". Clicking it navigates to the Sync pane, which displays the sync button, progress log area, and (if applicable) results from the last sync run in the current session.

**Why this priority**: Navigation is a prerequisite for accessing the feature, but it is a simple UI integration task that doesn't deliver user value on its own.

**Independent Test**: Can be tested by verifying the sidebar shows a "Sync" entry between "Reports" and "Settings", clicking it displays the Sync pane, and the pane shows the expected UI elements (sync button, progress log area).

**Acceptance Scenarios**:

1. **Given** the application is running, **When** the user views the sidebar, **Then** a "Sync" navigation entry appears between "Reports" and "Settings".
2. **Given** the user is on any other pane, **When** the user clicks the "Sync" sidebar entry, **Then** the Sync pane is displayed with the sync button and an empty progress log area.
3. **Given** a sync was previously completed in this session, **When** the user navigates away from and back to the Sync pane, **Then** the progress log from the last sync run is still visible (session-only persistence).

---

### Edge Cases

- What happens when the user clicks Sync while a sync is already running? The Sync button is disabled during execution, preventing concurrent syncs.
- What happens if the network connection drops during sync? The mailbox sync step reports a connection error in the progress log. If some mailboxes succeeded, their results are retained. Sync continues to the report processing step with whatever was downloaded.
- What happens if a single report fails to process but others succeed? The failing report generates an error-severity progress entry. Other reports continue processing normally. The error is included in the final error summary.
- What happens if the user closes the application during sync? The sync is cancelled via the cancellation token. Any work committed to the database before shutdown is retained.
- What happens if there are no Init-status reports to process after mailbox sync? The report processing step completes immediately with zero filings created. The progress log shows an informational entry: "No new reports to process."
- What happens if the progress log has many entries (e.g., hundreds)? The scrolling log continues to function with auto-scroll to the latest entry. Older entries remain accessible by scrolling up.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a dedicated Sync pane accessible from the sidebar, positioned between Reports and Settings.
- **FR-002**: System MUST provide a Sync button that initiates the full sync workflow when clicked.
- **FR-003**: System MUST execute the sync workflow in two sequential steps: first synchronise all configured mailboxes, then process all Init-status reports.
- **FR-004**: System MUST display real-time progress entries during sync, with each entry showing a timestamp, descriptive message, and severity level (Info, Warning, or Error).
- **FR-005**: System MUST display progress entries in a scrolling log that auto-scrolls to the most recent entry.
- **FR-006**: System MUST disable the Sync button while a sync is in progress and show a loading indicator.
- **FR-007**: System MUST provide a Cancel button during sync that aborts the operation at the earliest safe point.
- **FR-008**: System MUST retain all work completed before cancellation (no rollback of committed data).
- **FR-009**: System MUST display an error summary at the end of sync if any errors occurred, listing each error with a descriptive message.
- **FR-010**: System MUST automatically navigate to the Filings pane when sync completes successfully with at least one filing created and no errors.
- **FR-011**: System MUST NOT auto-navigate when sync completes with errors, with zero filings created, or after cancellation.
- **FR-012**: System MUST aggregate results from both sync steps into a single completion summary showing: reports synced, reports processed, filings created, and error count.
- **FR-013**: System MUST keep the progress log for the current session only (not persisted across application restarts).
- **FR-014**: System MUST orchestrate the sync workflow through a single command (`SyncAllCommand`) handled by `SyncAllCommandHandler`, which implements the dedicated `ISyncAllCommandHandler` interface (not `ICommandHandler<TCmd, TResult>` — the standard interface does not support `IProgress<SyncProgressEntry>` as a method argument). The handler reuses existing `SyncMailboxCommandHandler` and `ProcessReportsCommandHandler` without modifying them.
- **FR-015**: System MUST handle the case where no mailboxes or importers are configured by showing an informational message and completing immediately.
- **FR-016**: System MUST perform all sync operations asynchronously without blocking the user interface.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature touches three layers: **Application** (new `SyncAllCommand` / `SyncAllCommandHandler` / `ISyncAllCommandHandler`, new `SyncProgressEntry` and `SyncAllResult` DTOs), **Desktop** (new `SyncViewModel`, new `SyncView`, updated navigation in `MainWindowViewModel`). Domain layer is unaffected. Clean Architecture boundaries remain valid — the new command handler orchestrates existing handlers within the Application layer, and the Desktop layer consumes the new command via the dedicated `ISyncAllCommandHandler` interface (not the generic `ICommandHandler<TCmd, TResult>` — `SyncAllCommandHandler` requires an `IProgress<SyncProgressEntry>` parameter that the standard interface does not accommodate).
- **CA-002 (Money and Dates)**: No monetary fields are introduced. `SyncProgressEntry.Timestamp` uses `DateTimeOffset` (point-in-time for logging, not a calendar date — `DateOnly` does not apply here).
- **CA-003 (Privacy and Security)**: No new credentials are stored. Mailbox credentials are managed by the existing mailbox configuration. Progress log entries are ephemeral (session-only, in-memory) and contain no sensitive data beyond mailbox names and file names already visible in the application.
- **CA-004 (Network Scope)**: No new outbound calls are introduced. The sync workflow delegates to existing `SyncMailboxCommandHandler` which connects to configured IMAP mailboxes — all within previously approved network scope.
- **CA-005 (Async and UI)**: All sync operations are fully async. The Sync button uses `ReactiveCommand.CreateFromTask` with `IsExecuting` binding. Progress updates flow via `IProgress<T>` to the UI thread. Cancellation is supported via `CancellationToken` threaded through all async calls.
- **CA-006 (Testing Impact)**: **Application layer**: Unit tests for `SyncAllCommandHandler` orchestration logic (success, partial failure, cancellation, no-mailboxes edge case). **Desktop layer**: Unit tests for `SyncViewModel` (command state, progress collection updates, auto-navigation logic, cancellation). Existing `SyncMailboxCommandHandler` and `ProcessReportsCommandHandler` tests are unaffected.

### Key Entities *(include if feature involves data)*

- **SyncAllCommand**: Parameterless command record that triggers the full orchestration. Exact definition: `public sealed record SyncAllCommand();` Lives in `src/Rentier.Application/Commands/`.

- **SyncProgressEntry**: Represents a single progress log entry. Ephemeral — exists only in memory during the application session. Exact definition (in `src/Rentier.Application/DTOs/`):
  ```csharp
  public sealed record SyncProgressEntry(
      DateTimeOffset Timestamp,
      string Message,
      SyncProgressSeverity Severity);

  public enum SyncProgressSeverity { Info, Warning, Error }
  ```
  Distinct from the existing `SyncProgress(int Total, int Processed, string? CurrentFile, bool IsComplete)` DTO — the existing type is **not modified**.

- **SyncAllResult**: Aggregated outcome of the full sync workflow. Exact definition (in `src/Rentier.Application/DTOs/`):
  ```csharp
  public sealed record SyncAllResult(
      int MailboxesSynced,
      int AttachmentsDownloaded,
      int ReportsProcessed,
      int FilingsCreated,
      IReadOnlyList<string> Errors);
  ```

- **ISyncAllCommandHandler**: Dedicated handler interface (in `src/Rentier.Application/Interfaces/` or alongside `ICommandHandler`). Exact definition:
  ```csharp
  public interface ISyncAllCommandHandler
  {
      Task<Result<SyncAllResult, Error>> HandleAsync(
          SyncAllCommand command,
          IProgress<SyncProgressEntry> progress,
          CancellationToken ct = default);
  }
  ```
  Does **not** inherit from `ICommandHandler<TCmd, TResult>`.

- **SyncProgressSeverity**: Enumeration of progress entry severity levels — `Info`, `Warning`, `Error` — used to drive visual styling (icon and colour) in the progress log. Defined alongside `SyncProgressEntry` in `src/Rentier.Application/DTOs/`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can initiate a full sync (mailbox download + report processing) with a single click, completing both steps without manual intervention.
- **SC-002**: Users see the first progress entry within 2 seconds of clicking Sync (perceived responsiveness).
- **SC-003**: Progress entries update in real time during sync — no long gaps where the user sees no feedback while work is happening.
- **SC-004**: Users can cancel a running sync and see confirmation within 3 seconds.
- **SC-005**: After a successful sync that creates filings, the user is on the Filings pane within 1 second of completion — no manual navigation required.
- **SC-006**: When errors occur, users can identify which mailbox or report failed and the reason from the error summary alone, without needing to consult logs or other tools.
- **SC-007**: The Sync pane is discoverable — users can find and navigate to it from the sidebar without assistance.
- **SC-008**: The sync workflow does not block the user interface at any point — all UI elements remain responsive during sync execution.

## Assumptions

- At least one mailbox and one importer are configured before the user attempts to sync. The sync handles the zero-configuration case gracefully but the primary use case assumes prior setup.
- The existing `SyncMailboxCommandHandler` and `ProcessReportsCommandHandler` are stable and correct — this feature orchestrates them without modification.
- Sequential execution (mailbox sync → report processing) is the intended order. Parallel execution of the two steps is out of scope.
- Progress log entries are ephemeral and session-only. Users do not need to review sync history across application restarts.
- The sidebar navigation model supports inserting a new entry at a specific position (between Reports and Settings).
- Auto-navigation to the Filings pane on success is the desired default behaviour. There is no user preference to disable it.
- The `SyncProgress` DTO from the existing mailbox sync handler will be adapted into `SyncProgressEntry` entries by the orchestrating layer — the existing handler is not modified.
- Cancellation granularity is at the handler level — each handler checks the `CancellationToken` at natural checkpoints (e.g., between mailboxes, between reports). Sub-second cancellation is not guaranteed.
- **No new EF Core migration is required.** `SyncAllCommandHandler` is orchestration-only — all database writes are performed by the existing `SyncMailboxCommandHandler` and `ProcessReportsCommandHandler`. No new tables, columns, or schema changes are introduced.
- **`SyncMailboxCommandHandler` receives `IProgress<SyncProgress>?` via the `SyncMailboxCommand` constructor** (not as a method argument — see `SyncMailboxCommand(IProgress<SyncProgress>? Progress = null)`). `SyncAllCommandHandler` creates an internal `Progress<SyncProgress>` adapter that converts each `SyncProgress` report into a `SyncProgressEntry` forwarded to the outer `IProgress<SyncProgressEntry>`, then passes it as `new SyncMailboxCommand(internalProgress)`.
- **`SyncViewModel` is wired in `MainWindowViewModel`** using `ActivatorUtilities.CreateInstance<SyncViewModel>(provider, navigateToFilings)`, identical to how `ReportsViewModel` is wired. The `navigateToFilings` delegate is `Action` (no Guid argument — no report-ID filter needed for post-sync navigation). `MainWindowViewModel`'s constructor is updated to inject `SyncViewModel` and insert it between Reports and Settings in `NavigationEntries`.
