# Feature Specification: PP-OPO XML Schema Compliance Fix + Export Filename Convention

**Feature Branch**: `033-ppopo-xml-schema-compliance`  
**Created**: 2025-07-22  
**Status**: Draft  
**Input**: User description: "Corrects the PP-OPO XML output to fully comply with the ePorezi portal schema (http://pid.purs.gov.rs), fixes a data mapping bug in OsnovicaZaPorez, updates the suggested export filename to the convention {yyyy}-{MM}-{AssetName}.xml, and adds a Ticker field to Filing so the asset short-name can survive round-trips."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Upload Compliant XML to ePorezi Portal (Priority: P1)

A taxpayer exports a PP-OPO filing from Rentier and uploads the resulting XML file to the ePorezi portal. The portal accepts the file without validation errors because the XML now fully conforms to the ePorezi schema — correct root element with namespace, correct element names, required sections (Ukupno, Kamata, PodaciODodatnojKamati), correct taxpayer identification elements, and proper encoding declaration.

**Why this priority**: This is the core value of the entire feature. Without a valid XML that the portal accepts, the application fails its primary purpose — automating PP-OPO filing submission.

**Independent Test**: Can be fully tested by exporting a filing and verifying the XML document structure matches the ePorezi schema (namespace, element names, required sections). Delivers the ability to successfully upload filings.

**Acceptance Scenarios**:

1. **Given** a Filing with all required data and a valid TaxpayerProfile, **When** the user exports the filing as XML, **Then** the root element is `<ns1:PodaciPoreskeDeklaracije>` with `xmlns:ns1="http://pid.purs.gov.rs"` namespace.
2. **Given** a Filing is exported, **When** the XML is generated, **Then** every element uses the `ns1:` prefix.
3. **Given** a Filing is exported, **When** the XML is generated, **Then** the `<ns1:PodaciOPrijavi>` section includes `<ns1:Rok>1</ns1:Rok>`.
4. **Given** a TaxpayerProfile with JMBG, **When** the XML is generated, **Then** the taxpayer section contains `<ns1:PoreskiIdentifikacioniBroj>` with `<ns1:JMBGPodnosiocaPrijave>` (not bare `<JMBG>`).
5. **Given** a TaxpayerProfile with FullName, **When** the XML is generated, **Then** the name element is `<ns1:ImePrezimeObveznika>`.
6. **Given** a TaxpayerProfile with Address, **When** the XML is generated, **Then** the address element is `<ns1:UlicaBrojPoreskogObveznika>` containing street and number only.
7. **Given** a TaxpayerProfile with OpstinaCode, **When** the XML is generated, **Then** the municipality element is `<ns1:PrebivalisteOpstina>` with a 3-digit ePorezi municipality code.
8. **Given** a TaxpayerProfile with Phone/Email, **When** the XML is generated, **Then** they appear as `<ns1:TelefonKontaktOsobe>` and `<ns1:ElektronskaPosta>` respectively.
9. **Given** a Filing with income data, **When** the XML is generated, **Then** income rows are wrapped in `<ns1:PodaciOVrstamaPrihoda>` with `<ns1:RedniBroj>1</ns1:RedniBroj>`.
10. **Given** a Filing is exported, **When** the XML is generated, **Then** it includes `<ns1:Ukupno>` with aggregated monetary fields and zero contribution fields.
11. **Given** a Filing is exported, **When** the XML is generated, **Then** it includes `<ns1:Kamata>` with all-zero values.
12. **Given** a Filing is exported, **When** the XML is generated, **Then** it includes an empty `<ns1:PodaciODodatnojKamati/>` element.
13. **Given** a Filing is exported, **When** the XML is generated, **Then** the XML declaration uses uppercase `UTF-8` encoding.

---

### User Story 2 - Correct Tax Base (OsnovicaZaPorez) Value (Priority: P1)

A taxpayer exports a filing and the OsnovicaZaPorez field in the XML correctly reflects the gross income amount (the actual tax base), not the gross tax payable. This fixes a data accuracy bug that causes incorrect amounts on the tax form.

**Why this priority**: Equally critical — incorrect monetary values on a tax filing create legal and financial risk for the taxpayer. A structurally valid XML with wrong numbers is worse than a rejected upload.

**Independent Test**: Can be tested by creating a Filing where GrossIncomeRsd ≠ GrossTaxPayableRsd, exporting, and verifying the OsnovicaZaPorez value equals GrossIncomeRsd.

**Acceptance Scenarios**:

1. **Given** a Filing with GrossIncomeRsd = 100,000.00 and GrossTaxPayableRsd = 15,000.00, **When** the XML is generated, **Then** the `OsnovicaZaPorez` element value is `100000.00` (GrossIncomeRsd), not `15000.00`.
2. **Given** a Filing is exported, **When** the XML is generated, **Then** `ObracunatiPorez` reflects the correct computed tax amount (GrossTaxPayableRsd).

---

### User Story 3 - Human-Friendly Export Filename (Priority: P2)

When a taxpayer exports a filing, the suggested filename follows the convention `{yyyy}-{MM}-{AssetName}.xml` (e.g., `2025-03-BABA.xml`), making files easy to identify by tax period and asset at a glance in the file system.

**Why this priority**: Improves usability and file organization but is not a correctness issue. The old filename convention still produced functional files.

**Independent Test**: Can be tested by exporting a Filing that has a Ticker value and verifying the suggested filename matches the `{yyyy}-{MM}-{Ticker}.xml` pattern.

**Acceptance Scenarios**:

1. **Given** a Filing with IncomeDate 2025-03-15 and Ticker "BABA", **When** the user exports the filing, **Then** the suggested filename is `2025-03-BABA.xml`.
2. **Given** a Filing with no Ticker value (null or empty), **When** the user exports the filing, **Then** the system falls back to a reasonable default filename that still includes the year-month and some identifier (e.g., the paying entity name or JMBG).
3. **Given** a Ticker value containing characters unsafe for filenames, **When** the filename is generated, **Then** unsafe characters are sanitized or replaced.

---

### User Story 4 - Ticker Field on Filing Entity (Priority: P2)

The Filing entity includes a Ticker field (the asset short-name, e.g., "BABA", "AAPL") so that the asset's ticker symbol survives round-trips through creation, persistence, and export. This enables the filename convention and improves traceability of filings to specific assets.

**Why this priority**: Supports User Story 3 and adds general data completeness. The Ticker must be persisted to survive application restarts.

**Independent Test**: Can be tested by creating a Filing with a Ticker, persisting it, reloading it, and verifying the Ticker value is preserved.

**Acceptance Scenarios**:

1. **Given** a new Filing is created from income data that includes a ticker symbol, **When** the Filing is persisted and reloaded, **Then** the Ticker field retains its original value.
2. **Given** a Filing created from income data that has no ticker information, **When** the Filing is persisted, **Then** the Ticker field is null (the field is optional).
3. **Given** existing Filings in the database that were created before this feature, **When** the application starts after upgrade, **Then** the Ticker field is null for those existing records (backward compatible).

---

### Edge Cases

- What happens when the TaxpayerProfile has a 5-digit postal code in OpstinaCode instead of a 3-digit municipality code? The serializer emits whatever value is stored; correct data entry is a profile-setup concern.
- How does the system handle a Filing where all monetary values are zero? The XML must still include all required sections with zero values formatted to 2 decimal places.
- What happens if the Ticker field contains very long text (e.g., > 20 characters)? The domain should enforce a reasonable length constraint on the Ticker field.
- What happens when multiple Filings share the same year-month and Ticker? The filename convention still produces the same suggestion; the OS file-save dialog handles conflicts.
- What happens when optional TaxpayerProfile fields (PhoneNumber, Email) are null? The corresponding XML elements should be omitted or empty per ePorezi schema expectations.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST generate XML with root element `<ns1:PodaciPoreskeDeklaracije xmlns:ns1="http://pid.purs.gov.rs">` and namespace-prefix every child element with `ns1:`.
- **FR-002**: System MUST include `<ns1:Rok>1</ns1:Rok>` within the `<ns1:PodaciOPrijavi>` section.
- **FR-003**: System MUST emit taxpayer JMBG as `<ns1:PoreskiIdentifikacioniBroj>` containing `<ns1:JMBGPodnosiocaPrijave>`.
- **FR-004**: System MUST emit taxpayer name as `<ns1:ImePrezimeObveznika>`.
- **FR-005**: System MUST emit taxpayer address as `<ns1:UlicaBrojPoreskogObveznika>` (street and number only).
- **FR-006**: System MUST emit municipality as `<ns1:PrebivalisteOpstina>` with a 3-digit ePorezi municipality code.
- **FR-007**: System MUST emit telephone as `<ns1:TelefonKontaktOsobe>` and email as `<ns1:ElektronskaPosta>`.
- **FR-008**: System MUST wrap income rows in `<ns1:PodaciOVrstamaPrihoda>` with a sequential `<ns1:RedniBroj>` element.
- **FR-009**: System MUST map `OsnovicaZaPorez` to `GrossIncomeRsd` (not `GrossTaxPayableRsd`).
- **FR-010**: System MUST include a `<ns1:Ukupno>` section with aggregated monetary fields and zero contribution fields.
- **FR-011**: System MUST include a `<ns1:Kamata>` section with all-zero values.
- **FR-012**: System MUST include an empty `<ns1:PodaciODodatnojKamati/>` element.
- **FR-013**: System MUST use uppercase `UTF-8` in the XML encoding declaration.
- **FR-014**: System MUST suggest export filename in format `{yyyy}-{MM}-{Ticker}.xml` when Ticker is available.
- **FR-015**: System MUST provide a fallback filename pattern when Ticker is not available on the Filing.
- **FR-016**: System MUST add a `Ticker` field to the Filing domain entity that is optional (nullable), represents the asset short-name (e.g., "BABA"), and survives persistence round-trips.
- **FR-017**: System MUST ensure backward compatibility — existing Filings without a Ticker value MUST load with Ticker as null after the schema migration.
- **FR-018**: System MUST propagate the Ticker from the source income data (e.g., brokerage report) through Filing creation when available.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Impacted layers: Domain (Filing entity gains Ticker field), Infrastructure (PpOpoXmlSerializer rewrite, EF Core migration for Ticker column), Application (ExportFilingCommandHandler filename logic). Clean Architecture boundaries remain valid — no new cross-layer dependencies introduced.
- **CA-002 (Money and Dates)**: All monetary fields (OsnovicaZaPorez, Ukupno aggregates, Kamata zeros) remain `decimal`. Date fields (IncomeDate, TaxPeriod) remain `DateOnly`. No changes to type contracts.
- **CA-003 (Privacy and Security)**: No new outbound network calls. All data remains local in SQLite. JMBG handling unchanged (local-only).
- **CA-004 (Network Scope)**: No new outbound calls introduced. The ePorezi schema namespace URI is used only as an XML namespace identifier string, not as a network endpoint.
- **CA-005 (Async and UI)**: XML serialization is synchronous in-memory (byte array return) — no async change needed. Export command handler remains async for file I/O.
- **CA-006 (Testing Impact)**: Domain tests must cover Ticker field creation and validation. Infrastructure tests must cover the rewritten XML serializer (all element names, namespace, sections, encoding, OsnovicaZaPorez mapping). Application tests must cover the new filename convention logic. Existing snapshot tests must be updated with the new schema-compliant XML.

### Key Entities *(include if feature involves data)*

- **Filing**: Aggregate root representing a PP-OPO tax filing. Gains a new optional `Ticker` field (asset short-name, e.g., "BABA") to support the filename convention and asset traceability. All existing fields unchanged.
- **TaxpayerProfile**: Existing entity with JMBG, FullName, Address, OpstinaCode, PhoneNumber, Email. The Address and OpstinaCode field interpretations change in how the serializer uses them: Address maps to street-and-number only, OpstinaCode maps to 3-digit ePorezi municipality code. The entity schema itself is unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Exported PP-OPO XML files pass validation against the ePorezi portal schema with zero errors on every element, namespace, and required section.
- **SC-002**: OsnovicaZaPorez values in exported XML match the gross income amount for 100% of Filings (verified against source data).
- **SC-003**: Export filenames follow the `{yyyy}-{MM}-{Ticker}.xml` convention for all Filings with a Ticker value.
- **SC-004**: Existing Filings created before this feature load successfully with Ticker as null — zero data loss or migration errors.
- **SC-005**: All existing serializer tests are updated and passing, plus new tests cover every schema change enumerated in the current-vs-correct table.
- **SC-006**: Users can identify the correct filing file to upload by looking at the filename alone (year, month, and asset name visible) without opening the file.

## Assumptions

- The ePorezi portal schema at `http://pid.purs.gov.rs` is stable and the namespace URI will not change in the near term. If it changes, a single constant update will adapt the output.
- The TaxpayerProfile's `Address` field currently contains a full address including city. The serializer will use this field's value for `UlicaBrojPoreskogObveznika` (street + number). If the profile stores the full address, the user is responsible for entering only street and number in the Address field, or the UI/profile setup must guide them accordingly.
- The TaxpayerProfile's `OpstinaCode` field currently stores either a 5-digit postal code or a 3-digit municipality code. The serializer will emit whatever value is stored. If the profile contains a 5-digit code, the user or profile setup must be updated to store the 3-digit ePorezi municipality code.
- The `Rok` field value of `1` is a fixed constant for standard PP-OPO filings (representing the first filing period). This is hard-coded and not user-configurable.
- `NacinIsplate` remains `3` (hard-coded) for all filing types.
- The Ticker field is sourced from the brokerage report data (e.g., IBKR CSV `Symbol` column). The import pipeline already has access to this data.
- Contribution fields in `Ukupno` and all values in `Kamata` are zero because PP-OPO passive income filings do not have social contribution obligations or interest penalties at initial filing time.
- The `PodaciODodatnojKamati` element is always empty for standard filings.
