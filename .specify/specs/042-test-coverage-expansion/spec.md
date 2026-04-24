# Feature Specification: Test Coverage Expansion

**Feature Branch**: `042-test-coverage-expansion`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Expand test coverage to address gaps identified in the DevOps analysis. Three categories: A. FsCheck Property-Based Tests (6+ new tests), B. Verify Snapshot Tests (4+ new tests), C. Missing ViewModel Tests (3 ViewModels)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Property-Based Tests for Domain Invariants (Priority: P1)

As a developer, I need property-based tests that assert critical financial and scheduling invariants hold across thousands of randomized inputs, so that edge cases in exchange-rate conversions, filing-status transitions, deadline calculations, report aggregation, pagination, and payment references are caught before release.

**Why this priority**: Financial correctness and state-machine integrity are the highest-risk areas of the application. A rounding error in currency conversion or an illegal status transition could cause incorrect tax filings or data corruption. Property-based testing provides the strongest assurance across the broadest input space.

**Independent Test**: Can be fully verified by running the property-based test suite alone. Each test generates hundreds of random inputs and confirms the stated invariant holds for every one. If any single invariant fails, the test isolates a minimal counterexample.

**Acceptance Scenarios**:

1. **Given** two positive monetary amounts (foreign income and exchange rate), **When** the income is converted to local currency (RSD), **Then** the result is rounded to exactly two decimal places and equals the expected value within a half-cent tolerance.
2. **Given** a filing in any valid status (Init, Filed, Paid), **When** an invalid status transition is attempted (e.g., Init → Paid, Filed → Init, Paid → any), **Then** the transition is rejected and the filing retains its original status.
3. **Given** a filing in a valid status, **When** a valid forward transition is attempted (Init → Filed, Filed → Paid), **Then** the filing advances to the new status.
4. **Given** any income date and a set of holidays, **When** the filing deadline is calculated, **Then** the resulting deadline never falls on a Saturday, Sunday, or any date in the holiday set.
5. **Given** a collection of report amounts, **When** they are aggregated, **Then** the total equals the sum of all individual amounts (no rounding drift or missing items).
6. **Given** a dataset of N items and a page size, **When** the dataset is paginated and all pages are collected, **Then** every item appears exactly once (no loss and no duplication) and the total count matches N.
7. **Given** any valid payment reference string (within the allowed character set and length), **When** it is stored and retrieved, **Then** it round-trips without alteration; and any string exceeding the maximum length is rejected.

---

### User Story 2 — Snapshot Tests for Serialization Stability (Priority: P2)

As a developer, I need snapshot tests that lock down the exact output format of serialized data (XML exports and CSV exports), so that changes to serialization logic are immediately detected and intentional format changes are explicitly reviewed.

**Why this priority**: The application produces XML filings for government submission and CSV exports for user records. Any unintentional change to these formats could cause rejected tax filings or incompatible data imports. Snapshot tests provide a fast, deterministic safeguard against format regressions.

**Independent Test**: Can be fully verified by running the snapshot test suite. Each test serializes a representative object and compares the output byte-for-byte against an approved baseline. A mismatch fails the test immediately.

**Acceptance Scenarios**:

1. **Given** a filing record with an interest-income type, **When** it is serialized to XML, **Then** the output matches the approved snapshot (including element ordering, namespace declarations, and value formatting).
2. **Given** a filing record that includes payment reference data, **When** it is serialized to XML, **Then** the payment reference fields appear in the correct location within the XML structure and the output matches the approved snapshot.
3. **Given** a collection of report records, **When** they are exported to CSV format, **Then** the output matches the approved snapshot (including header row, delimiter, quoting rules, and date/number formatting).
4. **Given** a filing with fully populated detail fields, **When** it is exported as a filing-details document, **Then** the output matches the approved snapshot.

---

### User Story 3 — ViewModel Unit Tests for Untested View Models (Priority: P3)

As a developer, I need unit tests for the three ViewModels that currently lack any test coverage (HolidayEntryViewModel, SyncProgressEntryViewModel, ImporterItemViewModel), so that every ViewModel in the application has a baseline of verified behavior.

**Why this priority**: While these three ViewModels are simpler data-presentation models (no complex commands), they still participate in data binding and display logic. Untested ViewModels are blind spots that could silently regress during refactoring. Completing coverage for all ViewModels closes the last gap identified in the DevOps analysis.

**Independent Test**: Can be fully verified by running the ViewModel test suite. Each test constructs the ViewModel from representative input data and asserts that public properties are correctly populated and that round-trip conversions (to/from DTOs) preserve all values.

**Acceptance Scenarios**:

1. **Given** a holiday entry with a specific date and name, **When** the HolidayEntryViewModel is constructed from it, **Then** the Date and Name properties match the input; and converting back to a DTO produces identical values.
2. **Given** a sync progress log entry with a message, timestamp, and severity level, **When** the SyncProgressEntryViewModel is constructed, **Then** all display properties (Icon, Message, Timestamp, Severity) are correctly derived from the input.
3. **Given** an importer configuration with an identifier, display name, and report-type label, **When** the ImporterItemViewModel is constructed from it, **Then** the Id, DisplayName, and ReportTypeDisplay properties match the input.

---

### Edge Cases

- What happens when the exchange rate is extremely small (e.g., 0.0001) or extremely large (e.g., 999999.99)? The rounding invariant must still hold.
- What happens when every day in the deadline-adjustment window is a holiday or weekend? The calculator must still advance to the next working day without infinite looping.
- What happens when the paginated dataset is empty (0 items)? Pagination must return zero pages and zero items without error.
- What happens when a payment reference contains only whitespace? It must be normalized to null/empty rather than stored as whitespace.
- What happens when the CSV export contains fields with commas, quotes, or newlines? The snapshot must confirm correct escaping.
- What happens when a ViewModel is constructed with null or missing optional fields? Properties must default gracefully rather than throwing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The test suite MUST include at least 6 new property-based tests covering exchange-rate rounding, filing-status transitions, deadline weekday/holiday avoidance, report-sum consistency, pagination completeness, and payment-reference format.
- **FR-002**: The test suite MUST include at least 4 new snapshot tests covering XML serialization with interest income, XML serialization with payment references, CSV export format, and filing-details export.
- **FR-003**: The test suite MUST include unit tests for HolidayEntryViewModel, SyncProgressEntryViewModel, and ImporterItemViewModel, covering construction, property correctness, and round-trip DTO conversion where applicable.
- **FR-004**: All new tests MUST follow the existing naming convention: `MethodName_StateUnderTest_ExpectedBehavior`.
- **FR-005**: All new property-based tests MUST generate at least the default number of random cases (typically 100) per run and report a minimal counterexample on failure.
- **FR-006**: All new snapshot tests MUST store approved baselines alongside the test files and fail on any byte-level deviation from the baseline.
- **FR-007**: All new tests MUST pass on a clean build with no external dependencies (no network, no filesystem side effects, no database connections for unit tests).
- **FR-008**: The filing-status property-based test MUST exhaustively cover all possible transitions (valid and invalid) between Init, Filed, and Paid states.
- **FR-009**: The deadline property-based test MUST accept configurable holiday sets, including empty sets, single-day sets, and consecutive multi-day blocks.
- **FR-010**: The pagination property-based test MUST verify correctness across a range of page sizes (including page size = 1 and page size ≥ total items).

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature adds only test code. No production layers are modified. All new tests reside in the existing test projects (unit-test and infrastructure-test projects). Clean Architecture boundaries are unaffected.
- **CA-002 (Money and Dates)**: The exchange-rate rounding test asserts that monetary conversions use two-decimal-place precision (consistent with `decimal` usage). The deadline test asserts that `DateOnly` values are correctly advanced past weekends and holidays.
- **CA-003 (Privacy and Security)**: No changes to data storage or secret handling. Tests use synthetic/mock data only.
- **CA-004 (Network Scope)**: No outbound network calls. All tests are fully offline and self-contained.
- **CA-005 (Async and UI)**: Snapshot tests use async verification patterns. ViewModel tests exercise synchronous property accessors. No blocking I/O is introduced.
- **CA-006 (Testing Impact)**: This feature IS the testing update. It adds 13+ new tests across Domain (property-based), Infrastructure (snapshot), and Desktop (ViewModel) test projects.

### Key Entities

- **ExchangeRate**: A value object representing a currency conversion rate on a specific date. Key attributes: date, currency code, rate-to-RSD. Central to the rounding-invariant test.
- **Filing**: The core entity with a status state machine (Init → Filed → Paid), a payment reference (max 200 characters), and a calculated deadline. Central to status-transition, deadline, and payment-reference tests.
- **HolidayConf**: A value object containing a set of holiday dates used for deadline adjustment. Central to the deadline-avoidance test.
- **DashboardDto**: An aggregation result containing filing counts and totals. Central to the report-sum consistency test.
- **Pagination Results (FilingsPageResult, ReportsPageResult)**: Paged query responses containing rows, total count, and total pages. Central to the no-loss/no-duplication test.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The total number of property-based tests increases from 4 to at least 10 (≥ 6 new tests added).
- **SC-002**: The total number of snapshot tests increases from 1 to at least 5 (≥ 4 new tests added).
- **SC-003**: 100% of ViewModels in the application have at least one associated unit test (closing the 3-ViewModel gap).
- **SC-004**: All new tests pass on the first CI pipeline run after merge, with zero flaky failures across 3 consecutive runs.
- **SC-005**: The property-based test for filing-status transitions covers all 9 possible state pairs (3 source × 3 target states) and confirms exactly 2 succeed and 7 are rejected.
- **SC-006**: Every approved snapshot baseline is committed to version control alongside the test, enabling reviewers to inspect exact output formats.
- **SC-007**: The full test suite (including all new tests) completes within the existing CI time budget with no more than a 15% increase in total test execution time.

## Assumptions

- The existing test frameworks (xUnit, FluentAssertions, NSubstitute, FsCheck, Verify, Avalonia Headless) are already configured and available in the test projects; no new framework installation is required.
- The existing test naming convention (`MethodName_StateUnderTest_ExpectedBehavior`) is the standard to follow; no new naming patterns are introduced.
- Property-based tests use the default FsCheck configuration (100 random cases per property) unless a specific test requires a higher count.
- Snapshot baselines are approved once and committed; they are updated only through an explicit review process when serialization changes are intentional.
- The three untested ViewModels (HolidayEntryViewModel, SyncProgressEntryViewModel, ImporterItemViewModel) are data-presentation models without complex command logic; their tests focus on construction, property mapping, and DTO round-tripping.
- The DevOps analysis that identified these gaps is the authoritative source; no additional coverage gaps beyond these three categories are in scope for this feature.
- The "Paid → any" transition rejection includes Paid → Paid (self-transition), which is treated as invalid.
