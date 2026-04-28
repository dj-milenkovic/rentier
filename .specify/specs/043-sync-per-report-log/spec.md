# Feature Specification: Sync Per-Report Progress Log

**Feature Branch**: `043-sync-per-report-log`  
**Created**: 2025-07-17  
**Status**: Draft  
**Input**: User description: "During a sync operation, the log previously showed only aggregate progress. Now one log line is emitted per report processed, with severity colour-coding (Info / Warning / Error)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Per-Report Log Lines During Sync (Priority: P1)

As a user running a sync operation, I want to see one log line per report processed so that I can immediately identify which reports succeeded, partially failed, or completely failed — without having to dig into aggregate totals or open individual reports afterward.

**Why this priority**: This is the core value of the feature. Without per-report log lines, the user has no granular visibility into sync results and must manually inspect reports to find problems.

**Independent Test**: Can be fully tested by triggering a sync with a mix of reports (some fully successful, some partially failing, some fully failing) and verifying that each report produces its own log line with the correct filing counts.

**Acceptance Scenarios**:

1. **Given** a sync operation processes 5 reports with all filings created successfully, **When** the sync completes, **Then** 5 individual log lines appear, each reading "Report '{filename}': N filing(s) created, 0 failed."
2. **Given** a sync operation processes a report where 3 filings are created and 1 fails, **When** that report finishes processing, **Then** a log line reads "Report '{filename}': 3 filing(s) created, 1 failed."
3. **Given** a sync operation processes a report where all filings fail, **When** that report finishes processing, **Then** a log line reads "Report '{filename}': 0 filing(s) created, N failed."

---

### User Story 2 - Severity Colour-Coding of Log Lines (Priority: P1)

As a user reviewing sync results, I want log lines colour-coded by severity so that I can instantly scan for problems without reading every line in detail.

**Why this priority**: Colour-coding is essential for the feature's usability. Without it, per-report lines are harder to scan and the feature delivers significantly less value.

**Independent Test**: Can be tested by triggering syncs that produce each severity level and verifying the visual appearance of log lines matches the expected colour for each severity.

**Acceptance Scenarios**:

1. **Given** a report is processed with all filings created successfully (0 failed), **When** the log line appears, **Then** it is displayed with Info severity (normal/green styling).
2. **Given** a report is processed with some filings created and some failed (created > 0 AND failed > 0), **When** the log line appears, **Then** it is displayed with Warning severity (yellow/amber styling).
3. **Given** a report is processed where all filings failed (0 created, failed > 0), **When** the log line appears, **Then** it is displayed with Error severity (red styling).
4. **Given** a report encounters a processing error before any filings can be attempted, **When** the log line appears, **Then** it is displayed with Error severity (red styling).

---

### User Story 3 - Aggregate Progress Remains Alongside Per-Report Lines (Priority: P2)

As a user syncing many reports, I want the existing aggregate progress line to remain visible alongside the new per-report lines so that I still have a quick summary of overall sync progress.

**Why this priority**: The aggregate line provides a high-level overview that complements the per-report detail. Keeping it preserves backward compatibility and avoids disrupting existing workflows.

**Independent Test**: Can be tested by running a sync and verifying that both the aggregate progress message and individual per-report lines appear in the log.

**Acceptance Scenarios**:

1. **Given** a sync operation processes multiple reports, **When** the sync is running, **Then** both the aggregate progress line (e.g., "Processed N report(s)…") and individual per-report log lines appear in the log output.
2. **Given** a sync operation completes, **When** the user reviews the log, **Then** per-report lines appear in the order reports were processed, with the aggregate summary reflecting the total.

---

### Edge Cases

- What happens when a report file contains zero filings (empty report)? The log line should read "Report '{filename}': 0 filing(s) created, 0 failed." with Info severity.
- What happens when a report file cannot be read or parsed at all? An Error-severity log line should appear, e.g., "Report '{filename}': processing error." with 0 created and 0 filed counts.
- What happens when the sync is cancelled mid-operation? Reports already processed should have their log lines visible; reports not yet processed should have no log line.
- What happens when a report filename contains special characters (e.g., apostrophes, Unicode)? The filename should be displayed as-is in the log line without truncation or encoding artifacts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST emit one log line per report processed during a sync operation.
- **FR-002**: Each per-report log line MUST follow the format: "Report '{filename}': N filing(s) created, M failed." where N is the count of successfully created filings and M is the count of failed filings.
- **FR-003**: System MUST assign Info severity to log lines where all filings were created successfully (failed = 0, created ≥ 0).
- **FR-004**: System MUST assign Warning severity to log lines where some filings were created and some failed (created > 0 AND failed > 0).
- **FR-005**: System MUST assign Error severity to log lines where all filings failed (created = 0 AND failed > 0) or where the report encountered a processing error.
- **FR-006**: System MUST display log lines with colour-coding corresponding to their severity: Info as normal/green, Warning as yellow/amber, Error as red.
- **FR-007**: System MUST continue to display the existing aggregate progress line alongside the new per-report log lines.
- **FR-008**: Per-report log lines MUST appear in the order that reports are processed.
- **FR-009**: Each per-report log line MUST appear as soon as that report finishes processing (not batched until sync completion).

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Impacted layers: Application (sync command handler emits per-report results), Desktop (UI log display and colour-coding). Domain and Infrastructure layers are not expected to change. Clean Architecture boundaries remain valid — the Application layer produces structured progress data and the Desktop layer renders it.
- **CA-002 (Money and Dates)**: No monetary or date fields are introduced by this feature. Filing counts are integer values.
- **CA-003 (Privacy and Security)**: No new data is stored. Log lines contain only report filenames and filing counts, which are already visible to the local user. No secrets or external credentials involved.
- **CA-004 (Network Scope)**: No new outbound network calls. This feature adds logging to existing sync processing.
- **CA-005 (Async and UI)**: Sync operations already run asynchronously. Per-report log line emissions must be non-blocking and delivered to the UI on the appropriate thread for display. The UI must not freeze while receiving log updates.
- **CA-006 (Testing Impact)**: Application layer — unit tests for the sync handler to verify per-report result emission with correct severity assignment. Desktop layer — UI tests to verify colour-coded log line rendering for each severity level.

### Key Entities

- **Report Processing Result**: Represents the outcome of processing a single report during sync. Key attributes: report filename, count of filings created, count of filings failed, severity level (Info/Warning/Error).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a sync operation, users can identify which specific reports had failures within 5 seconds by scanning colour-coded log lines, without opening individual reports.
- **SC-002**: 100% of processed reports produce exactly one corresponding log line in the sync log.
- **SC-003**: Severity colour-coding is correctly applied for all three severity levels (Info, Warning, Error) based on the filing success/failure counts.
- **SC-004**: Per-report log lines appear in real time as each report finishes processing, not delayed until sync completion.
- **SC-005**: The existing aggregate progress summary remains visible and accurate alongside the new per-report lines.

## Assumptions

- The existing sync operation already processes reports individually and has access to per-report filing success/failure counts at processing time.
- The existing log/progress display in the UI supports displaying multiple lines and can be extended to support severity-based styling.
- Report filenames are unique within a single sync operation and serve as sufficient identifiers in log lines.
- The severity classification (Info/Warning/Error) is determined solely by the filing created/failed counts as specified — no additional severity logic is needed.
- Log lines are ephemeral (displayed during and after the sync session) and do not need to be persisted to disk or a database.
