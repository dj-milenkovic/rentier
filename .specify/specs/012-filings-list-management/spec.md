# Feature Specification: Filings List and Management UI

**Feature Branch**: `feature/012-filings-list-management`  
**Created**: 2025-01-08  
**Status**: Draft  

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — View All Filings (Priority: P1)

A user opens the Filings screen and immediately sees a paginated list of all tax filings sorted by
deadline (ascending). Each row shows the filing's status, income type, paying entity, deadline,
tax amount, and payment reference, giving a full snapshot of what is owed and when.

**Why this priority**: This is the entry point to the entire filings workflow. Without a working
list, no other interaction (status update, delete, filter) is possible.

**Independent Test**: Can be fully tested by navigating to the Filings screen with seeded data and
confirming rows appear in deadline-ascending order with correct column values and pagination
controls.

**Acceptance Scenarios**:

1. **Given** filings exist in the database, **When** the user navigates to the Filings screen,
   **Then** a paginated table loads with columns: Status, Income Type, Paying Entity, Filing
   Deadline (yyyy-MM-dd), Tax Payable (N,NNN.NN RSD), and Payment Reference — sorted by deadline
   ascending, 20 rows per page.
2. **Given** more than 20 filings exist, **When** the list loads, **Then** Previous/Next buttons
   and a "Page X of Y" indicator are visible; Previous is disabled on the first page.
3. **Given** the data is loading, **When** the async fetch is in progress, **Then** a loading
   indicator is visible and user interaction is suppressed.
4. **Given** the data load fails, **When** the fetch returns an error, **Then** an error message
   is displayed and no partial data is shown.

---

### User Story 2 — Filter Unpaid vs All (Priority: P2)

A user can toggle between seeing only unpaid filings (status Init or Filed) and seeing every
filing regardless of status, to focus their attention on actionable items without losing visibility
into the full history.

**Why this priority**: The default "Unpaid" view is the primary working view for a typical session;
"All" is needed for audit and historical review.

**Independent Test**: Can be fully tested by toggling the filter with a dataset that contains
filings of all three statuses (Init, Filed, Paid) and confirming the displayed rows change
accordingly.

**Acceptance Scenarios**:

1. **Given** filings with all three statuses exist, **When** the filter is set to "Unpaid",
   **Then** only Init and Filed filings are shown and the page resets to 1.
2. **Given** the "Unpaid" filter is active, **When** the user switches to "All", **Then** all
   filings including Paid ones are shown and the page resets to 1.
3. **Given** a filter is applied, **When** the user navigates pages, **Then** the filter remains
   active across page changes.

---

### User Story 3 — Advance Filing Status (Priority: P2)

A user can change a filing's status directly in the list via an inline dropdown, moving it from
Init → Filed or Filed → Paid. The system enforces these as the only valid transitions; an attempt
to make any other change is rejected with an error message.

**Why this priority**: Updating status is the core action in the post-generation workflow. It
directly tracks progress from filing preparation through to final tax payment.

**Independent Test**: Can be fully tested by selecting different status values in the dropdown for
Init and Filed rows, confirming valid transitions persist and invalid ones display an error.

**Acceptance Scenarios**:

1. **Given** a filing with status Init, **When** the user selects "Filed" in the status dropdown,
   **Then** the status updates in the database and the row reflects "Filed".
2. **Given** a filing with status Filed, **When** the user selects "Paid" in the status dropdown,
   **Then** the status updates in the database and the row reflects "Paid".
3. **Given** a filing with status Paid, **When** the user opens the status dropdown, **Then** no
   further status options are available (Paid is the terminal state).
4. **Given** a status update attempt that violates the state machine, **When** the command is
   processed, **Then** an error message is displayed and the original status is restored in the UI.

---

### User Story 4 — Enter Payment Reference (Priority: P3)

A user who has filed a tax return can enter the payment reference code (a string up to 200
characters) directly in the list row. The field is only editable when the filing has status Filed,
preventing accidental edits on unprocessed or already-paid filings.

**Why this priority**: Payment reference is needed to complete the payment step, but it is
downstream of status advancement. The field is a data-entry convenience, not gating.

**Independent Test**: Can be fully tested by confirming the Payment Reference cell is editable for
Filed rows only, and that entering text and committing persists the value.

**Acceptance Scenarios**:

1. **Given** a filing with status Filed, **When** the user clicks the Payment Reference cell and
   types a value, **Then** the value is saved (max 200 characters) upon commit.
2. **Given** a filing with status Init or Paid, **When** the user attempts to edit the Payment
   Reference cell, **Then** the field is read-only.
3. **Given** a payment reference longer than 200 characters is entered, **When** the user commits
   the value, **Then** the input is rejected with an appropriate error message.
4. **Given** a filing already has a payment reference, **When** the user clears the field,
   **Then** a null/empty value is saved (reference may be removed).

---

### User Story 5 — Delete a Filing (Priority: P3)

A user can delete a filing from the list. A confirmation dialog appears before the deletion is
executed, preventing accidental data loss.

**Why this priority**: Deletion is an infrequent, destructive action. A non-blocking confirmation
dialog is needed but this is a low-priority flow.

**Independent Test**: Can be fully tested by triggering the delete action on a row, confirming the
dialog appears, confirming deletion, and verifying the row disappears from the list.

**Acceptance Scenarios**:

1. **Given** a filing row, **When** the user activates the delete action, **Then** a
   ContentDialog appears asking for confirmation.
2. **Given** the confirmation dialog is open, **When** the user confirms, **Then** the filing is
   deleted from the database and removed from the list.
3. **Given** the confirmation dialog is open, **When** the user cancels, **Then** no deletion
   occurs and the list is unchanged.
4. **Given** a deletion fails (e.g., database error), **When** the delete command returns an
   error, **Then** an error message is displayed and the row remains.

---

### Edge Cases

- What happens when there are zero filings? An empty-state message is displayed instead of an
  empty table.
- What happens when the user navigates to page N and then applies a filter that reduces total
  pages below N? The page resets to 1.
- What happens when a status update succeeds but the currently active filter hides the new
  status? The row disappears from the filtered view immediately.
- What happens if a network/database error occurs during status update? The dropdown reverts to
  the previous value and an error message is shown.
- What happens when the user deletes the last item on a page beyond page 1? The page decrements
  by 1 after deletion.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST display a DataGrid with columns: Status, Income Type, Paying Entity,
  Filing Deadline (formatted `yyyy-MM-dd`), Tax Payable (formatted `N,NNN.NN RSD`), and Payment
  Reference.
- **FR-002**: The list MUST default to deadline-ascending sort order.
- **FR-003**: The system MUST paginate results at 20 items per page with Previous/Next navigation
  buttons and a "Page X of Y" indicator.
- **FR-004**: The system MUST provide a filter toggle with two states: "Unpaid" (shows Init and
  Filed filings only) and "All" (shows filings regardless of status); the default state is
  "Unpaid".
- **FR-005**: Changing the filter or page MUST NOT block the UI thread; all data fetches MUST be
  performed asynchronously.
- **FR-006**: The Status column MUST render as an inline dropdown; selecting a new value MUST
  invoke status advancement via the domain's `Filing.AdvanceStatus` logic.
- **FR-007**: The system MUST enforce valid status transitions only (Init → Filed → Paid). Any
  other requested transition MUST be rejected and an error message displayed.
- **FR-008**: The Payment Reference column MUST be an inline text field, editable ONLY when the
  row's status is Filed.
- **FR-009**: Payment Reference input MUST be validated to a maximum of 200 characters; inputs
  exceeding this limit MUST be rejected with an error message.
- **FR-010**: A delete action MUST be available per row; activation MUST open an asynchronous,
  non-blocking ContentDialog for user confirmation before performing any deletion.
- **FR-011**: A loading indicator (`IsLoading` state) MUST be shown during any async operation
  (fetch, status update, payment reference save, delete).
- **FR-012**: An error message area (`ErrorMessage` state) MUST be displayed when any async
  operation fails; it MUST be clearable.
- **FR-013**: All user-visible strings (column headers, button labels, dialog text, error messages,
  status labels, filter labels) MUST be stored in `Strings.resx`.
- **FR-014**: The application layer MUST expose:
  - `GetFilingsQuery` / `GetFilingsQueryHandler` — accepts filter mode (All/Unpaid), page number,
    and page size; returns a paged result set.
  - `UpdateFilingStatusCommand` / `UpdateFilingStatusCommandHandler` — accepts Filing ID and
    target status; validates via domain transition rules.
  - `UpdatePaymentReferenceCommand` / `UpdatePaymentReferenceCommandHandler` — accepts Filing ID
    and payment reference string (nullable, max 200 chars).
  - `DeleteFilingCommand` / `DeleteFilingCommandHandler` — accepts Filing ID; removes the record.
- **FR-015**: The `Filing` domain entity MUST be enriched with a `PaymentReference` (string?,
  max 200 chars) property and a `SetPaymentReference(string?)` mutating method.
- **FR-016**: A new EF Core migration (0009) MUST add a nullable `PaymentReference` TEXT column
  (max 200) to the Filings table created in migration 0008 (Feature 011).
- **FR-017**: `FilingsViewModel` MUST implement `IActivatableViewModel` and load data on
  activation using `ReactiveCommand.CreateFromTask`; NO blocking calls are permitted.
- **FR-018**: The existing `FilingsView` placeholder MUST be replaced by the new full DataGrid
  view.

---

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature spans all four layers.
  - **Domain**: `Filing` entity gains `PaymentReference` + `SetPaymentReference`.
  - **Application**: Four new CQRS handlers; a new `IFilingRepository` interface method for
    paged/filtered queries and deletion.
  - **Infrastructure**: EF migration 0009; `FilingRepository` implements the new interface methods.
  - **Desktop**: `FilingsViewModel` (ReactiveObject + IActivatableViewModel) + `FilingsView`
    (Avalonia DataGrid).
  Clean Architecture dependency rules remain intact — Desktop → Application → Domain; no upward
  references.
- **CA-002 (Money and Dates)**: `TaxPayable` is `decimal`; `FilingDeadline` is `DateOnly`. Both
  are displayed with explicit format strings (`N,NNN.NN RSD` and `yyyy-MM-dd` respectively). No
  `float`, `double`, or `DateTime` usage permitted.
- **CA-003 (Privacy and Security)**: All data is stored locally (SQLite). No credentials or
  sensitive identifiers are transmitted. No changes to security model.
- **CA-004 (Network Scope)**: This feature makes no outbound network calls. All operations are
  local database reads/writes.
- **CA-005 (Async and UI)**: All I/O (database reads, writes, deletes) MUST be performed via
  `async/await`. The UI thread is never blocked. `ReactiveCommand.CreateFromTask` is used for
  all VM commands. The delete confirmation dialog is awaited asynchronously.
- **CA-006 (Testing Impact)**:
  - **Domain**: Add unit tests for `SetPaymentReference` (valid, null, over-length) in
    `Rentier.Domain.Tests`.
  - **Application**: Unit tests for all four handlers covering happy path, invalid transitions,
    oversized reference, missing filing.
  - **Infrastructure**: Integration tests for the paged query, filtered query, and delete
    against an in-memory SQLite database.
  - **Desktop**: No automated UI tests required in this phase; manual acceptance testing is
    sufficient.

---

### Key Entities

- **Filing**: The aggregate root representing a single PP-OPO tax filing. Key attributes:
  `Id` (Guid), `TaxpayerProfileId` (Guid), `TaxPeriod` (DateOnly), `Status` (FilingStatus enum:
  Init/Filed/Paid), `PaymentReference` (string?, max 200). Owns the state-machine transition
  logic (`AdvanceStatus`) and reference mutation (`SetPaymentReference`).
- **FilingStatus**: An enumeration (Init = 0, Filed = 1, Paid = 2) representing the lifecycle
  stages of a filing. Only sequential forward transitions are valid.
- **FilingsPage**: A value object (or DTO) returned by `GetFilingsQueryHandler` containing the
  list of filing rows for the current page, total record count, and total page count.
- **FilingRow** (DTO): A read-model projection of a `Filing` used by the ViewModel, containing
  Id, Status, IncomeType, PayingEntity, FilingDeadline (DateOnly), TaxPayable (decimal), and
  PaymentReference (string?).

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can view the full filings list within 1 second of navigating to the Filings
  screen for datasets up to 500 filings.
- **SC-002**: Users can advance a filing's status (Init → Filed or Filed → Paid) in 2 interactions
  or fewer (open dropdown, select value) without leaving the list screen.
- **SC-003**: Users can enter a payment reference and have it persisted in 3 interactions or fewer
  (click cell, type value, commit) without leaving the list screen.
- **SC-004**: 100% of destructive actions (delete) are protected by a confirmation step; zero
  accidental deletions are possible without explicit user confirmation.
- **SC-005**: Invalid status transitions (e.g., Init → Paid, Paid → Init) are rejected 100% of
  the time with a human-readable error message; no corrupted filing state can be persisted.
- **SC-006**: Filtering switches the visible dataset in under 500 ms without a full screen
  reload.
- **SC-007**: The UI remains responsive (no freeze) during all async operations; a loading
  indicator is visible for any operation that takes more than 200 ms.
- **SC-008**: All user-visible strings appear in English with no hardcoded literals visible in
  the XAML or ViewModel code.

---

## Assumptions

- Feature 011 (Filing Generation Pipeline) has been completed and the Filings table exists in the
  database via migration 0008 before this feature is implemented.
- The `Filing` entity in Feature 011 includes `IncomeType` (Dividend/Interest) and `PayingEntity`
  (string) and `TaxPayable` (decimal) and `FilingDeadline` (DateOnly) columns; this feature reads
  but does not modify those fields.
- `IFilingRepository` either does not yet exist or has only a basic save method; this feature
  adds `GetPagedAsync(filter, page, pageSize)` and `DeleteAsync(id)` methods.
- The existing `FilingsView.axaml` contains a placeholder (e.g., "Coming soon" text) that can be
  safely replaced.
- No multi-user or concurrent-access scenarios apply; this is a single-user desktop application.
- Sorting is fixed (deadline ascending); user-adjustable column sort is out of scope for this
  feature.
- The "Income Type" column is read-only display; editing income type is out of scope.
- The "Paying Entity" column is read-only display; editing paying entity is out of scope.
- Performance target (SC-001) is defined for up to 500 filings; behaviour above that threshold
  is undefined and out of scope.
- The `ContentDialog` for delete confirmation uses the standard Avalonia/Semi.Avalonia dialog
  pattern consistent with the rest of the application.
