# Feature Specification: Reports List & Manual Import

**Feature Branch**: `003-reports-manual-import`  
**Created**: 2025-07-14  
**Status**: Draft  
**Input**: User description: "Implement the Reports list and manual import UI for Rentier. Replace the ReportsView placeholder with a DataGrid showing all reports."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse the Reports List (Priority: P1)

A Rentier user opens the Reports pane and sees a DataGrid listing all previously imported reports. Each row shows the report name, the date it was imported, the importer it came from, its processing status, and how many filings have been generated from it. The user can see at a glance which reports are awaiting processing (Init), have been fully processed, or encountered an error.

**Why this priority**: Without a working reports list, no other report management action is usable. This is the foundation of the entire pane and already has an existing placeholder that needs to be replaced.

**Independent Test**: Launch the application with seeded report data, navigate to the Reports pane, and verify all rows appear with correct column values. This delivers immediate read-only value with no dependency on import or delete.

**Acceptance Scenarios**:

1. **Given** the database contains three reports with varying statuses, **When** the user navigates to the Reports pane, **Then** all three reports appear in the DataGrid with correct Report Name, Import Date, Importer Name, Status, and Filing Count values.
2. **Given** the Reports pane is open, **When** no reports exist in the database, **Then** the DataGrid shows an empty state (no rows, no error).
3. **Given** a report has Status = Init, **When** it is displayed in the list, **Then** the Status column shows "Init".
4. **Given** a report has three linked filings, **When** it is displayed in the list, **Then** the Filing Count column shows "3".
5. **Given** the Reports pane is open, **When** a report is imported or deleted, **Then** the list refreshes automatically to reflect the change.

---

### User Story 2 - Manually Import a CSV Report (Priority: P2)

A user wants to import a brokerage statement they received by email or downloaded from the IBKR portal. They click the **Import** button, choose the CSV file via a native file picker, select the matching importer from a dropdown, and confirm. The system saves the report record and immediately runs the processing pipeline. When finished, the new report appears in the list with status Processed (or Error if processing fails).

**Why this priority**: Manual import is the core new capability of this feature — it allows users to add reports outside of the automated IMAP sync flow.

**Independent Test**: With at least one importer configured, click Import, select a valid IBKR CSV, choose the importer, confirm, and verify the report appears in the list with status Processed. This is a complete, self-contained user task.

**Acceptance Scenarios**:

1. **Given** the user clicks "Import", **When** the file picker opens, **Then** only CSV files are selectable (other formats are filtered out).
2. **Given** a valid IBKR CSV is selected and an importer is chosen, **When** the user confirms, **Then** a Report record is created with status Init, the processing pipeline runs, and the report's status updates to Processed upon success.
3. **Given** a valid IBKR CSV is selected and an importer is chosen, **When** the processing pipeline encounters an error, **Then** the Report record's status is set to Error and a clear error message is shown to the user.
4. **Given** the user selects a file that is not a valid IBKR CSV format, **When** the import is attempted, **Then** no report record is persisted and a user-friendly error message is displayed.
5. **Given** a report with the same name already exists for the selected importer, **When** the user attempts to import it again, **Then** the import is rejected with an informative duplicate-detection message and no duplicate record is created.
6. **Given** the user opens the Import dialog, **When** only one importer is configured, **Then** that importer is pre-selected in the dropdown.
7. **Given** the user opens the Import dialog, **When** the user presses Cancel or closes the dialog without selecting a file, **Then** no import occurs and the report list is unchanged.

---

### User Story 3 - View Filings for a Report (Priority: P3)

A user wants to inspect the tax filings generated from a specific report. They click a "View Filings" action on a row in the Reports DataGrid. The application navigates to the Filings pane, which is pre-filtered to show only filings linked to that report.

**Why this priority**: This is a cross-pane navigation convenience. The Filings pane already exists; this story adds a targeted entry point that saves the user from manually filtering.

**Independent Test**: With a report that has linked filings, click "View Filings" on that row and verify the Filings pane opens filtered to show only matching filings. This is fully testable without any other new stories.

**Acceptance Scenarios**:

1. **Given** a report row is visible in the DataGrid, **When** the user clicks "View Filings", **Then** the Filings pane opens with results filtered to filings linked to that report's ID.
2. **Given** a report has zero linked filings, **When** the user clicks "View Filings", **Then** the Filings pane opens showing an empty filtered list.

---

### User Story 4 - Delete a Report (Priority: P4)

A user wants to remove an erroneously imported report. They click a "Delete" action on a report row, see a confirmation dialog that warns them that linked filings will also be permanently deleted, and confirm the deletion. The report and all its filings disappear from the system.

**Why this priority**: Data correction capability. Lower priority than viewing and importing, as the absence of delete does not block primary workflows.

**Independent Test**: With a report that has linked filings, click Delete, confirm, and verify the report and its filings no longer appear in any list. Fully independent from import and navigation stories.

**Acceptance Scenarios**:

1. **Given** the user clicks "Delete" on a report row, **When** the confirmation dialog appears, **Then** it clearly states that linked filings will also be permanently removed.
2. **Given** the confirmation dialog is shown, **When** the user confirms, **Then** the report and all linked filings are deleted and the report no longer appears in the DataGrid.
3. **Given** the confirmation dialog is shown, **When** the user cancels, **Then** no deletion occurs and the report remains in the list.
4. **Given** a report has no linked filings, **When** the user confirms deletion, **Then** only the report record is deleted; no filing deletion errors occur.

---

### Edge Cases

- **Empty importer list**: If no importers are configured when the user clicks Import, the dialog should inform the user that no importers are available and close without proceeding.
- **File picker cancelled**: Cancelling the native file picker at any stage returns the user to the Reports pane with no side effects.
- **Concurrent pipeline run**: If the IMAP sync command (SyncCommand) is already running, importing a report should still be allowed; the processing pipeline handles queued Init reports safely.
- **Very large CSV file**: Files significantly exceeding typical IBKR export sizes should not freeze the UI; the operation must remain async.
- **Database unavailable**: If persistence fails during import, the error must be surfaced and no partial record left in an inconsistent state.
- **Report deletion during refresh**: If a report is deleted while the list is refreshing, the operation should complete gracefully without displaying errors to the user.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST display all Report records in a DataGrid with the following columns: Report Name, Import Date (date only, no time), Importer Name (resolved from Importer.DisplayName), Status (Init / Processed / Error), and Filing Count (count of filings linked to that report).
- **FR-002**: The Reports list MUST refresh automatically each time the Reports pane becomes visible and after every import or delete operation.
- **FR-003**: The system MUST provide an "Import" button that opens a native OS file picker dialog, filtered to CSV files only.
- **FR-004**: The Import flow MUST present an importer selector (dropdown) populated from all available importers before the user confirms the import.
- **FR-005**: If exactly one importer is configured, it MUST be pre-selected in the dropdown; the user may change the selection.
- **FR-006**: If no importers are configured, the import flow MUST inform the user and abort without proceeding.
- **FR-007**: The system MUST validate the selected CSV file against the expected IBKR CSV format before creating any records; an invalid file MUST produce a descriptive error message and leave the database unchanged.
- **FR-008**: The system MUST check for an existing Report with the same name for the same importer (using `ExistsByImporterAndNameAsync`) before persisting; a duplicate MUST be rejected with an informative message.
- **FR-009**: On successful validation, the system MUST persist a new Report record with: ReportName set to the selected file's filename, ImportDate set to today's date, status Init, and AttachmentContent containing the raw CSV bytes.
- **FR-010**: Immediately after persisting the new Report, the system MUST trigger `ProcessReportsCommand` to run the filing generation pipeline and await its completion before returning.
- **FR-011**: After pipeline execution, the report's final status (Processed or Error) MUST be visible in the list; if the pipeline produced an error, a user-facing error message MUST be displayed.
- **FR-012**: Each row in the DataGrid MUST expose a "View Filings" action that navigates to the Filings pane pre-filtered by the selected report's ID.
- **FR-013**: Each row in the DataGrid MUST expose a "Delete" action that shows a confirmation dialog before deletion; the dialog MUST explicitly state that linked filings will also be permanently removed.
- **FR-014**: On confirmed deletion, the system MUST delete the Report and cascade-delete all linked Filings; the record MUST no longer appear in the list.
- **FR-015**: All user-visible strings (button labels, dialog titles, error messages, column headers, status labels) MUST be stored in `Strings.resx` and referenced via the resource key.
- **FR-016**: All file I/O, database reads/writes, and pipeline execution MUST be performed asynchronously; the UI MUST remain responsive during these operations.
- **FR-017**: The existing IMAP Sync functionality (`SyncCommand`) on `ReportsViewModel` MUST remain fully operational and unmodified in behavior.
- **FR-018**: New application-layer operations MUST be exposed as `GetReportsQuery` / Handler and `ImportReportCommand` / Handler, registered with `AddTransient` lifetime.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature spans the Desktop layer (ReportsView AXAML, ReportsViewModel), the Application layer (GetReportsQuery, ImportReportCommand and their handlers), and read access to Domain entities (Report, Importer, Filing). No domain logic is introduced in the Desktop or Application layers; all business rules (e.g., CSV parsing, pipeline execution) remain in Infrastructure and Domain. Clean Architecture boundaries are preserved.
- **CA-002 (Money and Dates)**: `ImportDate` is `DateOnly` throughout — in the domain entity, the Application query result DTO, and the ViewModel display binding. No monetary values are involved in this feature.
- **CA-003 (Privacy and Security)**: All data is stored locally in the application's SQLite database. The CSV file's raw bytes are stored in `Report.AttachmentContent` (local only). No credentials or personally identifiable information are transmitted externally.
- **CA-004 (Network Scope)**: This feature makes no outbound network calls. File access is limited to the user-selected CSV on the local file system. IMAP sync is an existing, separate feature.
- **CA-005 (Async and UI)**: The native file picker dialog is invoked asynchronously. All repository calls (`GetAll`, `Add`, `Delete`, `ExistsByImporterAndNameAsync`) and the `ProcessReportsCommand` pipeline are awaited. `ReactiveCommand.CreateFromObservable` / `CreateFromTask` is used for all ViewModel commands to prevent UI thread blocking.
- **CA-006 (Testing Impact)**:
  - **Application**: Unit tests for `GetReportsQueryHandler` (returns mapped DTOs), `ImportReportCommandHandler` (success path, invalid CSV, duplicate detection, pipeline trigger).
  - **Infrastructure**: Integration tests confirming `IbkrCsvParser` correctly rejects malformed files and parses valid ones (likely already covered; confirm coverage).
  - **Desktop**: ViewModel tests verifying that `ImportCommand`, `DeleteCommand`, and `ViewFilingsCommand` change observable state correctly using a fake/mock service.

### Key Entities

- **Report**: Represents a single imported brokerage statement. Key attributes: `Id` (unique identifier), `ReportName` (filename-derived, ≤ 500 characters), `ImportDate` (DateOnly — date the file was imported), `ImporterId` (reference to the Importer), `Status` (one of Init / Processed / Error), `AttachmentContent` (raw CSV bytes), `MailboxMessageId` (null for manually imported reports).
- **Importer**: Represents a named data-source configuration. Key attributes: `Id`, `DisplayName` (shown in the importer selector dropdown), `ReportType` (e.g., IbkrCsv — determines which parser to use).
- **Filing**: A generated tax filing record. Linked to a Report via `ReportId`. Used for the Filing Count column and the "View Filings" navigation. Cascade-deleted when its parent Report is deleted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can view the full list of reports with accurate metadata within 2 seconds of navigating to the Reports pane, for a database containing up to 500 reports.
- **SC-002**: A complete manual import — file selection, importer selection, confirmation, pipeline processing — completes and the updated status is visible in the list within 30 seconds for a typical IBKR monthly CSV export.
- **SC-003**: Invalid CSV files are rejected before any record is created; the user sees a clear, actionable error message within 3 seconds of confirming the import.
- **SC-004**: Duplicate report detection (same importer + same report name) is 100% reliable — no duplicate records are ever persisted regardless of how many times the same file is imported.
- **SC-005**: Deletion of a report and all its linked filings completes within 3 seconds of user confirmation, and the deleted report is no longer visible in the list.
- **SC-006**: The UI remains fully interactive (no freezes or blocking) during import, processing, and delete operations, even for files up to 5 MB in size.
- **SC-007**: All user-visible text is localizable — no hard-coded strings exist outside of `Strings.resx`.

## Assumptions

- The filename of the selected CSV file is used directly as the `ReportName` (trimmed, ≤ 500 characters). No additional name entry UI is required.
- `ImportDate` is set to the current local date at the moment of import; no date picker is provided.
- The `MailboxMessageId` field is left null for manually imported reports (it is only populated by IMAP sync).
- Pagination of the Reports DataGrid is out of scope for this version; all reports are loaded in a single query.
- The "View Filings" navigation uses the existing routing/navigation mechanism already present in the application (e.g., a shared navigation service or message-passing approach already used by other panes).
- The `ProcessReportsCommand` processes **all** reports with status Init, not just the newly added one. This is the existing behavior and is preserved.
- The importer dropdown in the Import dialog lists all importers returned by `IImporterRepository`; no filtering by `ReportType` is applied in the UI (the parser handles type validation during processing).
- Error messages from the `ProcessReportsCommand` pipeline are already captured in the Report's status transition to Error; surfacing the failure to the user (as a dialog or status indicator) is handled at the ViewModel level using the existing `Result<T, Error>` pattern.
- The existing `SyncCommand` and all associated IMAP sync logic in `ReportsViewModel` are out of scope for modification; they are preserved as-is.
- `AddTransient` DI lifetime applies to all new Application-layer handlers, consistent with the project's desktop DI policy.
