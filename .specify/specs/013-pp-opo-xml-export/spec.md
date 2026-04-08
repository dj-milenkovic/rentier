# Feature Specification: PP-OPO XML Export

**Feature Branch**: `002-pp-opo-xml-export`  
**Created**: 2026-04-06  
**Status**: Draft  
**Input**: User description: "Implement PP-OPO XML export for Rentier. The user clicks Export on a filing row and saves a .xml file via a native Save dialog."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Export a Filing as PP-OPO XML File (Priority: P1)

A taxpayer has one or more completed filings in Rentier. They select a filing row in the filings list and click "Export". A native OS save dialog appears pre-populated with a suggested filename. The taxpayer chooses a save location and confirms. A valid ePorezi PP-OPO XML file is written to disk, ready to be uploaded to the tax authority portal.

**Why this priority**: This is the entire purpose of the feature. Without the ability to produce a correctly structured XML file and save it to disk, the feature delivers no value. All other stories depend on this working correctly.

**Independent Test**: Can be fully tested end-to-end by loading a fixture filing with known field values, triggering the export action, and verifying that the saved file contains the expected PP-OPO XML structure with all fields correctly mapped and formatted.

**Acceptance Scenarios**:

1. **Given** a filing with IncomeType = Dividend exists in the system and the taxpayer profile is populated, **When** the user clicks "Export" on that filing row, **Then** a save dialog appears with a pre-populated filename in the format `PP-OPO_YYYY-MM_JMBG.xml`.
2. **Given** the save dialog is open, **When** the user confirms a save path, **Then** the file is written to that path without error and the UI returns to its normal state.
3. **Given** the saved file, **When** it is opened and validated, **Then** it contains a well-formed XML document with root element `PodaciOPrijavi` and all required child sections populated from the filing and taxpayer profile data.
4. **Given** a filing with IncomeType = Interest, **When** exported, **Then** the XML field `SifraVrstePrihoda` equals `111401000`.
5. **Given** a filing with IncomeType = Dividend, **When** exported, **Then** the XML field `SifraVrstePrihoda` equals `111402000`.
6. **Given** any monetary amount in the filing (e.g., GrossIncomeRsd = 12345.50), **When** exported, **Then** the corresponding XML element contains the value formatted as `"12345.50"` (exactly two decimal places, period as decimal separator).

---

### User Story 2 - Export Correctly Maps All XML Sections (Priority: P2)

A tax officer inspects the exported XML to verify it contains all mandatory ePorezi PP-OPO sections with data sourced correctly from the filing, the taxpayer profile, and the linked importer's payment notes.

**Why this priority**: Correctness of the XML structure is the core quality requirement. An XML file that saves but maps fields incorrectly is useless for tax submission. This story ensures each section and field is verified independently of the UI flow.

**Independent Test**: Can be fully tested by unit-testing the serialization component in isolation: construct a Filing + TaxpayerProfile + PaymentNotes in memory and assert that the resulting XML contains the exact expected element values for each section.

**Acceptance Scenarios**:

1. **Given** a filing with a known `IncomeDate` (DateOnly) and `FilingDeadline` (DateOnly), **When** serialized, **Then** `DatumOstvarivanjaPrihoda` equals the income date formatted as `YYYY-MM-DD` and `DatumDospelostiObaveze` equals the filing deadline formatted as `YYYY-MM-DD`.
2. **Given** a filing belonging to an accounting period (derived from IncomeDate), **When** serialized, **Then** `ObracunskiPeriod` equals `YYYY-MM` where YYYY and MM come from `IncomeDate`.
3. **Given** a taxpayer profile with JMBG, FullName, Address, OpstinaCode, Phone, and Email, **When** serialized, **Then** all six fields appear in `PodaciOPoreskomObvezniku`; FullName and Address are wrapped in CDATA sections.
4. **Given** an importer with non-empty `PaymentNotes`, **When** serialized, **Then** `PodaciONacinuOstvarivanjaPrihoda/Ostalo` contains those notes; `NacinIsplate` equals `3`.
5. **Given** known decimal values for GrossIncomeRsd, WhtPaidRsd, GrossTaxPayableRsd, and TaxPayableRsd, **When** serialized, **Then** `BrutoPrihod`, `OsnovicaZaPorez`, `ObracunatiPorez`, `PorezPlacenDrugojDrzavi`, and `PorezZaUplatu` each contain the correctly formatted decimal string.

---

### User Story 3 - Export Fails Gracefully When Data Is Incomplete (Priority: P3)

A taxpayer attempts to export a filing for which the taxpayer profile has not yet been configured, or the linked importer data is unavailable. Instead of silently producing an invalid XML file or crashing, the application informs the user with a clear, actionable error message.

**Why this priority**: Graceful failure prevents submission of malformed XML to the tax authority. The happy path (P1 + P2) is more critical, but error handling is necessary for a production-quality feature.

**Independent Test**: Can be fully tested by triggering the export command for a filing that lacks a taxpayer profile and asserting that the result is a descriptive failure (not an exception) and that no file is written to disk.

**Acceptance Scenarios**:

1. **Given** no taxpayer profile has been configured, **When** the user clicks "Export" on any filing, **Then** the export is rejected and the user sees a message such as "Taxpayer profile is required before exporting."
2. **Given** the export command returns a failure result, **When** the Desktop layer receives it, **Then** no save dialog is shown and a user-visible error notification is displayed.
3. **Given** the user cancels the save dialog without choosing a path, **When** the dialog is dismissed, **Then** no file is written and the application returns to normal state without displaying an error.

---

### Edge Cases

- What happens when `PaymentNotes` on the importer is null or empty? The `Ostalo` element is included with an empty value (the field is always present in the schema).
- What happens when `Filing.ReportId` is null (filing not yet linked to a Report/Importer)? `PaymentNotes` is treated as an empty string; the `Ostalo` element is written as an empty element and serialization continues. No error is raised for a missing importer link.
- What happens when a monetary amount is zero? The element is still written as `"0.00"`.
- What happens when `FilingDeadline` falls on a weekend or public holiday? The filing stores whatever deadline was calculated upstream; export uses it as-is without adjustment.
- What happens if the user chooses a path where they do not have write permission? The export command surfaces a failure and the Desktop layer shows an error notification; no partial file is left on disk.
- What happens if two filings share the same JMBG and accounting period? Each is exported independently; filename collisions at the save path are the user's responsibility to resolve via the save dialog.

## Clarifications

### Session 2026-04-07

- Q: What is the repository loading chain for ExportFilingCommand, and what happens when Filing.ReportId is null? → A: Filing → Report (via `Filing.ReportId`) → Importer (via `Report.ImporterId`); handler requires `IFilingRepository`, `IReportRepository`, `IImporterRepository`, and `ITaxpayerProfileRepository`. If `Filing.ReportId` is null, `PaymentNotes` is treated as an empty string and serialization continues normally.
- Q: What is the formal return type of ExportFilingCommand? → A: `Result<byte[], Error>` — the handler returns serialized XML bytes; the Desktop layer is solely responsible for opening the save dialog and writing bytes to disk.
- Q: Which Avalonia 11 API is used for the native save dialog? → A: `window.StorageProvider.SaveFilePickerAsync(...)` — the legacy `SaveFileDialog` from `Avalonia.Controls` is not used.
- Q: Where does the Export button live in the UI and how is the command typed? → A: A dedicated "Export" column in the FilingsView DataGrid; `FilingsViewModel` exposes a `ReactiveCommand` named `ExportCommand` that accepts the row's filing `Guid`.
- Q: Does this feature require a new EF Core migration? → A: No — no new database tables or columns are introduced; all required data is already persisted by existing migrations.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to trigger an export action via a dedicated "Export" button rendered as a new column in the FilingsView DataGrid; each row's button is bound to that row's filing `Id` (Guid) so each filing can be exported independently.
- **FR-002**: System MUST present a native OS file save dialog using Avalonia 11's `StorageProvider.SaveFilePickerAsync(...)` API (not the legacy `SaveFileDialog` from `Avalonia.Controls`) when the user triggers the export action, pre-populated with the suggested filename `PP-OPO_YYYY-MM_JMBG.xml` and filtered to `*.xml` files.
- **FR-003**: System MUST load the filing, the associated taxpayer profile, and the linked importer's payment notes before serialization using the following repository chain: `IFilingRepository` → `IReportRepository` (via `Filing.ReportId`) → `IImporterRepository` (via `Report.ImporterId`) → `ITaxpayerProfileRepository`. If `Filing.ReportId` is null, `PaymentNotes` is treated as an empty string and serialization continues (see Edge Cases).
- **FR-004**: System MUST reject the export and return a descriptive failure when the taxpayer profile is not configured.
- **FR-005**: System MUST produce a valid ePorezi PP-OPO XML document with the following top-level structure:
  - `PodaciOPrijavi` (root): `VrstaPrijave=1`, `ObracunskiPeriod` (YYYY-MM), `DatumOstvarivanjaPrihoda` (YYYY-MM-DD), `DatumDospelostiObaveze` (YYYY-MM-DD)
  - `PodaciOPoreskomObvezniku`: JMBG, FullName (CDATA), Address (CDATA), OpstinaCode, Phone, Email
  - `PodaciONacinuOstvarivanjaPrihoda`: `NacinIsplate=3`, `Ostalo` (payment notes)
  - `DeklarisaniPodaciOVrstamaPrihoda`: `SifraVrstePrihoda`, `BrutoPrihod`, `OsnovicaZaPorez`, `ObracunatiPorez`, `PorezPlacenDrugojDrzavi`, `PorezZaUplatu`
- **FR-006**: System MUST map `IncomeType.Interest` to `SifraVrstePrihoda = 111401000` and `IncomeType.Dividend` to `SifraVrstePrihoda = 111402000`.
- **FR-007**: System MUST format all monetary amounts as decimal strings with exactly two decimal places using period as the decimal separator (e.g., `"12345.50"`, `"0.00"`), regardless of system locale.
- **FR-008**: System MUST write the XML file to the user-selected path only after serialization succeeds; no partial file must be left on disk if an error occurs during write.
- **FR-009**: System MUST return control to normal UI state after the file is saved successfully or after the user cancels the save dialog.
- **FR-010**: System MUST expose the serialization capability through an interface defined in the Application layer, with the implementation residing in the Infrastructure layer.
- **FR-011**: The export command MUST be fully asynchronous; no blocking I/O is permitted on the UI thread.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: The feature spans four layers. Domain: no changes (existing entities reused). Application: new `IXmlFilingSerializer` interface and `ExportFilingCommand`/Handler (returns `Result<byte[], Error>`). Infrastructure: `PpOpoXmlSerializer` implementing the interface. Desktop: `FilingsViewModel.ExportCommand` (`ReactiveCommand<Guid, Unit>`) triggers the handler, opens the Avalonia 11 `StorageProvider.SaveFilePickerAsync(...)` dialog, and writes bytes to disk. Clean Architecture boundaries are preserved — the interface is defined in Application and only Infrastructure knows the XML serialization format.
- **CA-002 (Money and Dates)**: Monetary fields (`GrossIncomeRsd`, `WhtPaidRsd`, `GrossTaxPayableRsd`, `TaxPayableRsd`) are all `decimal`; dates (`IncomeDate`, `FilingDeadline`) are `DateOnly`. Both types are used as-is from the domain; serialization converts them to string representations only at the output boundary.
- **CA-003 (Privacy and Security)**: All data is read from the local database and written to a local file chosen by the user. No data leaves the device. JMBG and taxpayer personal details are written to disk only at the user's explicit request via the save dialog. No secrets or credentials are involved.
- **CA-004 (Network Scope)**: No outbound network calls. The feature is entirely local: read from DB, serialize to bytes, write to user-selected path.
- **CA-005 (Async and UI)**: The `ExportFilingCommand` handler is async end-to-end. The Desktop layer awaits the command result before opening the save dialog, and the file write is performed asynchronously. The UI thread is never blocked.
- **CA-006 (Testing Impact)**: Application layer: unit tests for `ExportFilingCommand` handler (verify correct entity loading and serializer invocation). Infrastructure layer: unit tests for `PpOpoXmlSerializer` validating XML structure and field values against known-good samples for both Dividend and Interest income types. Desktop layer: no automated tests required for the save dialog interaction (platform UI); manual verification is sufficient.

### Key Entities

- **Filing**: Represents a single tax filing event. Key fields used for export: `Id`, `IncomeType`, `IncomeDate`, `FilingDeadline`, `GrossIncomeRsd`, `WhtPaidRsd`, `GrossTaxPayableRsd`, `TaxPayableRsd`. Links to a Report which links to an Importer.
- **TaxpayerProfile**: Represents the user's personal tax identity. Key fields: `JMBG`, `FullName`, `Address`, `OpstinaCode`, `Phone`, `Email`. Required to be present before export can proceed.
- **Importer**: Represents the entity that paid income to the taxpayer. Key field used for export: `PaymentNotes` (maps to `Ostalo` in the XML).
- **ExportFilingCommand**: Application-layer command that takes a filing `Id` (`Guid`), loads all required entities via `IFilingRepository`, `IReportRepository`, `IImporterRepository`, and `ITaxpayerProfileRepository`, and returns `Result<byte[], Error>` containing the serialized XML bytes on success.
- **IXmlFilingSerializer**: Application-layer interface that abstracts the serialization of a filing + taxpayer profile + payment notes into an XML byte array.
- **FilingsViewModel (Desktop)**: Exposes an `ExportCommand` (`ReactiveCommand<Guid, Unit>`) that accepts the filing `Guid` from the DataGrid row; the command awaits the `ExportFilingCommand` result, opens the Avalonia 11 save dialog via `StorageProvider.SaveFilePickerAsync(...)`, writes the returned bytes to disk asynchronously, and surfaces failures via an in-app error notification without opening the save dialog.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The user can trigger an export, complete the save dialog, and have a valid PP-OPO XML file on disk in under 3 seconds from click to file saved.
- **SC-002**: 100% of exported XML files pass schema validation against the ePorezi PP-OPO format without manual correction.
- **SC-003**: All monetary values in exported XML files exactly match the stored `decimal` values when parsed back, with no rounding or precision loss.
- **SC-004**: The export feature works correctly for both Dividend and Interest income types, verified by at least one unit test per income type covering the full XML structure.
- **SC-005**: The application never crashes or leaves a corrupt file on disk when the export fails due to missing data or a write error.

## Assumptions

- The taxpayer profile is a singleton (one profile per Rentier installation); the export command always loads the single configured profile.
- `ObracunskiPeriod` is derived from `Filing.IncomeDate` (year + month), not from a separate field on the filing.
- `PorezPlacenDrugojDrzavi` maps to `WhtPaidRsd` (withholding tax paid to a foreign state at source).
- `OsnovicaZaPorez` (tax base) maps to `GrossTaxPayableRsd` as defined on the Filing entity.
- `PorezZaUplatu` (tax due for payment) maps to `TaxPayableRsd` on the Filing entity.
- `ObracunatiPorez` (calculated tax) maps to `GrossTaxPayableRsd`; if a more precise separation is required, that is a future refinement.
- The suggested export filename uses the taxpayer's JMBG and the accounting period derived from `IncomeDate`.
- The XML encoding is UTF-8 with an XML declaration (`<?xml version="1.0" encoding="utf-8"?>`).
- `VrstaPrijave` is always `1` for this feature (initial filing); amended filings are out of scope.
- `NacinIsplate` is always `3` for this feature; other payment methods are out of scope.
- If `PaymentNotes` is null or whitespace, `Ostalo` is written as an empty element.
- If `Filing.ReportId` is null (filing not yet linked to a Report), `PaymentNotes` is treated as an empty string; the `Ostalo` element is still written as an empty element (consistent with the null/whitespace edge case above).
- The Desktop layer uses `AddTransient` registration for all new services, consistent with existing project conventions.
- The save dialog default file extension filter is `*.xml`.
- The native save dialog is opened via Avalonia 11's `window.StorageProvider.SaveFilePickerAsync(...)` API; the legacy `Avalonia.Controls.SaveFileDialog` is not used anywhere in this feature.
- This feature introduces **no new EF Core database migrations**; all required data (`Filing`, `Report`, `Importer`, `TaxpayerProfile`) is already persisted by existing migrations.
