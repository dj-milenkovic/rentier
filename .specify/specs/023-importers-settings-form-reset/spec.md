# Feature Specification: Importers Settings Form Reset on Save & Navigation

**Feature Branch**: `002-importer-form-reset`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Fix the Importers settings form so it is always consistent with the selected importer. Problem reported by QA: After saving changes or switching to a different importer in the list, some fields retain stale values from the previous edit. The form is not fully reset on save or on selection change."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Form Reflects Saved State After Save (Priority: P1)

A user edits an importer's settings (e.g., changes the display name, from-filter, and attachment regex), then clicks Save. After the save completes successfully, the form must show the exact values that were persisted — not stale values from before the edit, and not leftover values from a previously selected importer.

**Why this priority**: This is the core bug reported by QA. Stale form data after save is the highest-impact issue because users cannot trust whether their changes were actually saved, leading to confusion, duplicate saves, and potential data corruption.

**Independent Test**: Can be fully tested by editing any field on an importer, saving, and verifying every form field matches the persisted data. Delivers confidence that save works correctly.

**Acceptance Scenarios**:

1. **Given** an importer is selected and the user has modified DisplayName, FromFilter, and AttachmentRegex, **When** the user saves successfully, **Then** all form fields (DisplayName, ReportType, SelectedProfile, SelectedMailbox, FromFilter, SubjectFilter, AttachmentRegex, PaymentNotes) display the values from the freshly persisted record.
2. **Given** an importer is selected and the user has modified SubjectFilter and PaymentNotes, **When** the user saves successfully, **Then** the importer list is refreshed and the saved importer is automatically re-selected by its identifier.
3. **Given** an importer is selected and the user has modified only the ReportType, **When** the save succeeds, **Then** all other fields (not just ReportType) reflect the current persisted state — none retain stale or intermediate values.

---

### User Story 2 — Form Populates Correctly on Selection Change (Priority: P1)

A user selects a different importer from the list. The form must immediately display all fields from the newly selected importer, replacing any values from the previously selected importer — including any unsaved edits the user may have made.

**Why this priority**: This is the second half of the QA-reported bug and equally critical. Stale data on selection change means a user could unknowingly overwrite one importer's settings with another's.

**Independent Test**: Can be fully tested by selecting Importer A, editing fields, then selecting Importer B, and verifying every form field shows Importer B's values.

**Acceptance Scenarios**:

1. **Given** Importer A is selected and the user has modified some fields without saving, **When** the user selects Importer B, **Then** all form fields display Importer B's values and no values from Importer A remain.
2. **Given** Importer A has a non-empty AttachmentRegex and Importer B has an empty AttachmentRegex, **When** the user switches from A to B, **Then** AttachmentRegex displays as empty (not A's value).
3. **Given** Importer A uses Profile X and Mailbox Y, and Importer B uses Profile Z and Mailbox W, **When** the user switches from A to B, **Then** SelectedProfile shows Profile Z and SelectedMailbox shows Mailbox W.

---

### User Story 3 — Form Clears on Deselect (Priority: P2)

When no importer is selected (e.g., the selection becomes null after a delete, or the user deselects), the form must clear all fields to their empty or default state so that no orphaned data is displayed.

**Why this priority**: While less frequent than save and navigation, a non-empty form with no selected importer is misleading and could cause users to attempt edits that have no target.

**Independent Test**: Can be fully tested by selecting an importer, then triggering a deselect (set selection to null), and verifying every field is empty/default.

**Acceptance Scenarios**:

1. **Given** an importer is selected with all fields populated, **When** the selection becomes null, **Then** all form fields (DisplayName, ReportType, SelectedProfile, SelectedMailbox, FromFilter, SubjectFilter, AttachmentRegex, PaymentNotes) are cleared to empty or default values.
2. **Given** the user had unsaved edits on a selected importer, **When** the selection becomes null, **Then** all edits are discarded and the form is fully cleared.

---

### User Story 4 — All Editable Fields Are Covered by Reset Logic (Priority: P2)

Every editable field bound on the importer settings form must be included in the populate-from-data and clear-form logic. No field may be accidentally excluded, ensuring the form is always fully consistent.

**Why this priority**: The root cause of the QA bug is that some fields were excluded from the reset path. This story ensures completeness and prevents regressions when new fields are added in the future.

**Independent Test**: Can be verified by comparing the set of all editable/bound form fields against the set of fields handled in the populate and clear logic — they must be identical.

**Acceptance Scenarios**:

1. **Given** the importer form has editable fields: DisplayName, ReportType, SelectedProfile, SelectedMailbox, FromFilter, SubjectFilter, AttachmentRegex, PaymentNotes, **When** the populate logic runs, **Then** every one of these fields is set from the source data.
2. **Given** the importer form has editable fields as listed above, **When** the clear logic runs, **Then** every one of these fields is set to empty or default.

---

### User Story 5 — Automated Tests Verify Reset Behavior (Priority: P2)

Automated ViewModel-level tests must verify that save-then-re-select and select-different-item scenarios correctly update all form fields. These tests protect against future regressions.

**Why this priority**: Without automated coverage the bug could easily recur when new fields are added or logic is refactored.

**Independent Test**: Can be verified by running the test suite and confirming all new tests pass.

**Acceptance Scenarios**:

1. **Given** a ViewModel test that simulates saving an importer, **When** the save completes, **Then** all bound properties on the ViewModel reflect the refreshed data.
2. **Given** a ViewModel test that simulates switching from Importer A to Importer B, **When** the selection changes, **Then** all bound properties reflect Importer B's data.
3. **Given** a ViewModel test that simulates deselecting (selection becomes null), **When** the deselect occurs, **Then** all bound properties are cleared to empty/default.

### Edge Cases

- What happens when the save operation fails (e.g., network error or validation error)? The form should retain the user's current edits so they can retry — it must NOT reset to stale data on failure.
- What happens when the importer list refresh after save returns a list that no longer contains the saved item (e.g., it was deleted by another process)? The form should deselect and clear.
- What happens when the user rapidly switches between importers before the previous selection's data fully loads? The form must display data for the most recently selected importer, not an intermediate one.
- What happens when a field value is null in the source data vs. an empty string? The form should treat both as "empty" and display consistently (e.g., empty text field, no selection for dropdowns).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: After a successful save, the system MUST reload the importer list and re-select the saved importer by its unique identifier.
- **FR-002**: After a successful save and re-selection, the system MUST repopulate every editable form field (DisplayName, ReportType, SelectedProfile, SelectedMailbox, FromFilter, SubjectFilter, AttachmentRegex, PaymentNotes) from the refreshed data.
- **FR-003**: When the user selects a different importer, the system MUST populate all editable form fields from the newly selected importer's data, discarding any unsaved edits.
- **FR-004**: When the selected importer becomes null (deselect), the system MUST clear all editable form fields to empty or default values.
- **FR-005**: The set of fields handled by the populate and clear logic MUST exactly match the set of all editable/bound form fields — no field may be excluded.
- **FR-006**: On save failure, the system MUST NOT reset or clear form fields — the user's current edits must be preserved for retry.
- **FR-007**: If the importer list refresh after save does not contain the saved item, the system MUST deselect and clear the form.
- **FR-008**: Automated ViewModel tests MUST cover: (a) save → re-select verifies all fields, (b) select-different-item verifies all fields, and (c) deselect verifies all fields are cleared.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This change is confined to the Desktop/Presentation layer (ViewModel). It does not alter Domain, Application, or Infrastructure layers. Clean Architecture boundaries remain intact — the ViewModel consumes existing DTOs and commands without modification.
- **CA-003 (Privacy and Security)**: No change to data storage. All importer data remains local-first. No credentials or secrets are affected by form reset logic.
- **CA-005 (Async and UI)**: The save operation is already asynchronous. The post-save refresh and re-selection must also be non-blocking — the UI must remain responsive during list reload.
- **CA-006 (Testing Impact)**: New ViewModel unit tests are required in the Desktop test project. No Domain, Application, or Infrastructure test changes needed.

### Key Entities

- **Importer**: Represents a configured email importer with attributes: unique identifier, display name, report type, associated taxpayer profile, associated mailbox, from-filter, subject-filter, attachment regex pattern, and payment notes.
- **Importer List**: The collection of all configured importers displayed in the settings navigation list. After save, this list is refreshed to reflect persisted state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After saving any combination of field edits on an importer, 100% of form fields display the persisted values — zero stale values remain.
- **SC-002**: After switching from one importer to another, 100% of form fields display the newly selected importer's values — zero fields retain the previous importer's data.
- **SC-003**: After deselecting (selection becomes null), 100% of form fields are in their empty/default state.
- **SC-004**: All new ViewModel unit tests pass, covering save-then-re-select, select-different-item, and deselect scenarios across all eight editable fields.
- **SC-005**: The QA-reported bug (stale fields after save or switch) is no longer reproducible in manual testing.

## Assumptions

- The existing save command and importer list loading logic work correctly — this feature only addresses the form state management after those operations complete.
- The set of editable fields is currently: DisplayName, ReportType, SelectedProfile, SelectedMailbox, FromFilter, SubjectFilter, AttachmentRegex, PaymentNotes. If new fields are added in the future, they must be included in the populate and clear logic.
- The importer's unique identifier is stable across save-and-reload cycles (i.e., saving does not change the importer's ID).
- Dropdown/selection fields (ReportType, SelectedProfile, SelectedMailbox) have a well-defined default or null state when cleared.
- No confirmation dialog is needed when switching importers with unsaved edits — edits are silently discarded. (An "unsaved changes" prompt is out of scope for this fix.)
