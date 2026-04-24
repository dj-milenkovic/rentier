# Research: Test Coverage Expansion

**Feature**: 042-test-coverage-expansion  
**Date**: 2025-07-15

## R-001: CSV Export Snapshot — No CSV Export Code Exists

**Context**: The spec (User Story 2, Acceptance Scenario 3) requests a snapshot test for "reports exported to CSV format." Codebase exploration found no CSV export/writer code. The only CSV-related code is `IbkrCsvParser` (an importer that reads IBKR CSV statements into `StatementParseResult`).

**Decision**: Reinterpret the "CSV export" snapshot test as an **IBKR CSV parser output stability** test. This test parses a known IBKR CSV fixture through `IbkrCsvParser.ParseAsync()` and snapshots the resulting `StatementParseResult` (dividends, interest, withholding tax, exchange rates). This validates serialization/parsing stability in the CSV pipeline — which is the actual serialization pathway the application has.

**Rationale**: The parser is the application's only CSV boundary. If its output structure changes (field parsing, aggregation, rounding, ISIN stripping), downstream consumers (filing creation, tax calculation) could silently break. A snapshot baseline locks the parsed output format just as a serialization snapshot locks output format.

**Alternatives considered**:
- *Create a CSV export feature for reports*: Rejected — out of scope for a test-only feature; would require production code changes.
- *Skip the CSV snapshot test entirely*: Rejected — would leave only 3 of 4 required snapshots and miss a real coverage gap.
- *Snapshot the raw CSV fixture itself*: Rejected — the fixture is static input, not application output; no stability value.

## R-002: Filing Details Snapshot — Maps to XML Export with Fully-Populated Fields

**Context**: The spec (Acceptance Scenario 4) requests a snapshot for "filing-details document" with fully populated detail fields. The `ExportFilingCommandHandler` produces XML bytes via `PpOpoXmlSerializer.Serialize()`.

**Decision**: The filing-details snapshot test calls `PpOpoXmlSerializer.Serialize()` with a fully-populated `Filing` (including payment reference set via `SetPaymentReference()`) and verifies the complete XML output. This doubles as coverage for Acceptance Scenario 2 (payment reference in XML) — a single test with a fully-populated filing satisfies both the "payment reference snapshot" and "filing details snapshot" requirements.

**Rationale**: The existing snapshot (`Serialize_RepresentativeDividendFiling_MatchesSnapshot`) uses a dividend filing without payment reference. The two new snapshot tests cover: (1) interest income type (different `SifraVrstePrihoda`: `111401000` vs `111402000`), and (2) fully-populated filing with payment reference.

**Alternatives considered**:
- *Two separate tests for payment ref and filing details*: Acceptable but creates redundancy — a fully-populated filing naturally includes the payment reference path.
- *Use ExportFilingCommandHandler for the snapshot*: Rejected — the handler depends on repositories (requires mocking); the serializer is the true serialization unit.

## R-003: FsCheck v3 API Patterns — Confirmed from Existing Code

**Context**: Need to confirm FsCheck 3.x API conventions for writing new property-based tests.

**Decision**: Follow existing patterns from `TaxCalculationProperties.cs` and `FilingDeadlineProperties.cs`:
- Attribute: `[Property]` (from `FsCheck.Xunit`)
- Return type: `Property` (from `FsCheck`)
- Assertion: `(bool_expression).ToProperty()` (from `FsCheck.Fluent`)
- Built-in generators: `PositiveInt` for bounded random integers, converted to `decimal` via `/ 100m`
- Async domain calls in sync property methods: `.GetAwaiter().GetResult()` (FsCheck v3 requires sync `Property` return)

**Rationale**: Consistency with existing 4 property tests; no new API patterns needed.

## R-004: Verify.Xunit Configuration — Default Config Sufficient

**Context**: Need to understand Verify snapshot configuration for new tests.

**Decision**: Use default Verify.Xunit configuration (global usings auto-generated, no custom `ModuleInitializer`):
- `await Verify(content, extension)` where extension is `"xml"` for XML snapshots
- For parser output snapshot: `await Verify(parsedResult)` using Verify's built-in serialization of records/lists
- Baseline files auto-generated on first run, then committed alongside test files
- File naming: `{ClassName}.{MethodName}.verified.{ext}`

**Rationale**: Existing `PpOpoXmlSerializerSnapshotTests` uses this exact pattern successfully. No custom settings needed.

## R-005: Pagination Property Test — Handler vs In-Memory Verification

**Context**: The spec requires a property test verifying "no-loss, no-duplication" across paginated results. The pagination handlers (`GetFilingsQueryHandler`, `GetReportsQueryHandler`) depend on repositories.

**Decision**: Test pagination logic in isolation using an in-memory collection approach. Generate a random list of N items and a random page size, then paginate using the same `Skip`/`Take` logic as the handlers: verify that collecting all pages yields exactly the original items (no loss, no duplication, correct total count). This tests the pagination *algorithm* without handler/repository coupling.

**Rationale**: Property-based tests should be fast (<1ms per case × 100 cases) and dependency-free. Testing through the full handler would require mocked repositories and async overhead.

**Alternatives considered**:
- *Test through handler with mocked repository*: Rejected — too slow for 100+ random cases; tests handler plumbing rather than pagination invariant.
- *Test using EF Core in-memory provider*: Rejected — integration-level concern; property tests belong in Domain/unit layer.

## R-006: Report Aggregation Property Test — Sum Consistency

**Context**: The spec requires a property test verifying "the total equals the sum of all individual amounts (no rounding drift or missing items)" for report aggregation.

**Decision**: Generate a random list of `decimal` amounts, sum them manually, then verify that the aggregation produces the same total. Since `DashboardDto.TotalUnpaidRsd` is computed in the query handler (sums `TaxPayableRsd` for non-Paid filings), the property test verifies the mathematical invariant: `sum(individual amounts) == reported total`. Implement as a pure domain-level test on decimal aggregation to catch rounding drift.

**Rationale**: Financial sums with `decimal` should never drift — unlike `double`, `decimal` addition is exact for financial values. The test proves this invariant holds across large collections and varied amounts.

## R-007: ViewModel Test Patterns — Simple POCO Testing

**Context**: The 3 untested ViewModels are simple data-presentation models. Need to confirm test approach.

**Decision**: Follow the existing ViewModel test pattern but simplified (no `IActivatableViewModel` lifecycle, no mock command handlers):
- **HolidayEntryViewModel**: Test construction via `FromDto()`, property getters, `RaiseAndSetIfChanged` property setters, `ToDto()` round-trip equality.
- **SyncProgressEntryViewModel**: Test construction from `SyncProgressEntry`, verify `Icon` derivation for all severity levels (Error→"✕", Warning→"⚠", default→"•"), verify `Timestamp` formatting (`HH:mm:ss`), verify `Message` pass-through.
- **ImporterItemViewModel**: Test factory `From()`, verify `Id`/`DisplayName` pass-through, verify `ReportTypeDisplay` uses `ToDisplayString()` extension (currently only `IbkrCsv`→`"IBKR CSV"`).

**Rationale**: These are data-binding models with no commands. Tests focus on construction + property mapping + DTO round-trip (where applicable). Follows spec FR-003 requirements.
