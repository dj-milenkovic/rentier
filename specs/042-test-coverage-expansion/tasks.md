# Tasks: Test Coverage Expansion

**Input**: Design documents from `/specs/042-test-coverage-expansion/`
**Branch**: `042-test-coverage-expansion` | **Date**: 2025-07-15 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

**Tests**: This IS the testing feature. Every task is a test task. No production code changes.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths included in every task description

---

## Phase 1: Setup

**Purpose**: Confirm baseline is clean before adding any new tests.

- [ ] T001 Verify full solution builds with zero warnings by running `dotnet build -warnaserror` from the repository root and confirming exit code 0

---

## Phase 2: Foundational (Blocking Prerequisite for US2 CSV Snapshot)

**Purpose**: The IBKR CSV parser snapshot test (T012) cannot be written without a known-good fixture file. Create it first so US1 and US3 can proceed in parallel while the fixture is prepared.

**⚠️ CRITICAL**: T012 cannot begin until T002 is complete. T003–T008 (US1) and T014–T016 (US3) are not blocked and can start immediately after T001.

- [ ] T002 Create IBKR CSV fixture file at `tests/Rentier.Infrastructure.Tests/Fixtures/ibkr-sample.csv` containing a representative IBKR Activity Statement CSV with at least one dividend record, one interest record, one withholding-tax record, and one exchange-rate record, formatted exactly as the real IBKR export (header rows, section headers such as `Dividends,Header,...` and `Interest,Header,...`, correct column ordering); this fixture will be parsed by `IbkrCsvParser.ParseAsync()` in T012

**Checkpoint**: T002 done → T012 (IBKR CSV snapshot) can begin. T003–T008 and T014–T016 may already be in progress.

---

## Phase 3: User Story 1 — Property-Based Tests for Domain Invariants (Priority: P1) 🎯 MVP

**Goal**: Add ≥6 new FsCheck properties that assert critical financial and scheduling invariants hold across hundreds of randomised inputs, raising the total property-test count from 4 to ≥10 (SC-001).

**Independent Test**:
```powershell
dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~Properties"
# Expect: ≥10 tests listed and all pass; FsCheck reports the number of cases tried per property
```

### Implementation for User Story 1

- [ ] T003 [US1] Extend `tests/Rentier.UnitTests/Domain/Properties/TaxCalculationProperties.cs` with a new `[Property]`-attributed method `ConvertToRsd_AnyPositiveAmountAndRate_ResultIsRoundedToTwoDecimalPlaces` that generates random positive `decimal` amounts (via `PositiveInt / 100m`) and random positive `decimal` rates, computes `Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero)`, and asserts the result equals `decimal.Round(amount * rate, 2)` within a tolerance of `0.005m`; include edge-case generators for extremely small rates (e.g., `0.0001m`) and extremely large rates (e.g., `99999.99m`) by adding a second property `ConvertToRsd_ExtremeRates_ResultIsStillRoundedToTwoDecimalPlaces`

- [ ] T004 [US1] Extend `tests/Rentier.UnitTests/Domain/Properties/TaxCalculationProperties.cs` with a new `[Property]`-attributed method `AggregateAmounts_AnyCollection_TotalEqualsSumOfIndividuals` that generates a random non-empty list of non-negative `decimal` amounts (via `NonNegativeInt / 100m`), computes the expected total as `amounts.Sum()`, then asserts the aggregated result equals the expected total with no drift; this verifies the decimal-sum consistency invariant for report aggregation (FR-001, SC-001, research R-006)

- [ ] T005 [P] [US1] Extend `tests/Rentier.UnitTests/Domain/Properties/FilingDeadlineProperties.cs` with a new `[Property]`-attributed method `CalculateDeadline_AnyIncomeDateAndHolidaySet_DeadlineNeverFallsOnWeekendOrHoliday` that uses FsCheck generators to produce random `DateOnly` income dates and random lists of holiday `DateOnly` values (including empty lists, single-day lists, and consecutive multi-day blocks), calls `FilingDeadlineCalculator.CalculateDeadline(incomeDate, new HolidayConf(holidays))`, and asserts the result is neither a Saturday nor a Sunday and is not contained in the holiday set; follow the existing pattern using `.GetAwaiter().GetResult()` if the domain service is async (FR-001, FR-009)

- [ ] T006 [P] [US1] Create `tests/Rentier.UnitTests/Domain/Properties/FilingStatusTransitionProperties.cs` with two `[Property]`-attributed methods: (1) `AdvanceStatus_ValidTransition_FilingMovesToNewStatus` that generates all valid `(source, target)` pairs `{(Init, Filed), (Filed, Paid)}`, constructs a `Filing` in the source status, calls `AdvanceStatus(target)`, and asserts the filing is now in the target status; (2) `AdvanceStatus_InvalidTransition_ThrowsDomainException` that generates all 7 invalid pairs (every `(FilingStatus, FilingStatus)` pair except the 2 valid ones, including all self-transitions), calls `AdvanceStatus(target)` inside a `try/catch`, and asserts `DomainException` is thrown and the filing status is unchanged; confirm SC-005: exactly 2 pairs succeed and 7 pairs are rejected across all 9 combinations of `{Init, Filed, Paid} × {Init, Filed, Paid}` (FR-001, FR-008)

- [ ] T007 [P] [US1] Create `tests/Rentier.UnitTests/Domain/Properties/PaginationProperties.cs` with a `[Property]`-attributed method `Paginate_AnyDatasetAndPageSize_AllItemsAppearExactlyOnce` that generates a random list of `int` items (0 to 200 items), a random page size between 1 and the dataset size + 1 (inclusive), applies Skip/Take pagination using the same formula as the query handlers (`skip = (page - 1) * pageSize`, `take = pageSize`), collects all pages, and asserts: (a) every original item appears in exactly one page, (b) no item appears more than once, (c) the total collected count equals the original dataset count, (d) total-page count equals `Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize))`; include the edge case where the dataset is empty (0 items) by adding a dedicated small test or covering it via `Gen.Choose(0, 200)` (FR-001, FR-010, research R-005)

- [ ] T008 [P] [US1] Create `tests/Rentier.UnitTests/Domain/Properties/PaymentReferenceProperties.cs` with two `[Property]`-attributed methods: (1) `SetPaymentReference_ValidString_RoundTripsWithoutAlteration` that generates random ASCII printable strings up to 200 characters (excluding leading/trailing whitespace), calls the `Filing`'s `SetPaymentReference(value)` method, and asserts the stored reference equals the original input; (2) `SetPaymentReference_StringExceedingMaxLength_IsRejected` that generates strings of length 201–500 characters and asserts the call throws a `DomainException` or returns a validation failure (match existing Filing validation pattern); also add a dedicated non-property test `SetPaymentReference_WhitespaceOnly_IsNormalisedToNull` using a `[Fact]` to cover the whitespace edge case (FR-001, FR-007, spec edge case)

**Checkpoint**: T003–T008 done → US1 fully implemented. Run `dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~Properties" --list-tests` and confirm ≥10 tests listed. SC-001 met.

---

## Phase 4: User Story 2 — Snapshot Tests for Serialization Stability (Priority: P2)

**Goal**: Add ≥4 new Verify snapshot tests that lock down exact serialization output, raising the total snapshot count from 1 to ≥5 (SC-002).

**Independent Test**:
```powershell
dotnet test tests/Rentier.Infrastructure.Tests --filter "FullyQualifiedName~Snapshot"
# Expect: ≥5 tests listed (1 existing + 4 new) and all pass with baselines committed
```

### Implementation for User Story 2

- [ ] T009 [US2] Add `Serialize_InterestIncomeFiling_MatchesSnapshot` to `tests/Rentier.Infrastructure.Tests/Serialization/PpOpoXmlSerializerSnapshotTests.cs`: construct a `Filing` with `IncomeType = IncomeType.Interest` (all other required fields populated with representative values), call `PpOpoXmlSerializer.Serialize(filing)`, and `await Verify(xmlBytes, "xml")`; confirm the XML output contains `SifraVrstePrihoda` with the interest code (`111401000`) rather than the dividend code; follow the existing snapshot test structure in the file (FR-002, spec AS US2-1)

- [ ] T010 [US2] Add `Serialize_FilingWithPaymentReference_MatchesSnapshot` to `tests/Rentier.Infrastructure.Tests/Serialization/PpOpoXmlSerializerSnapshotTests.cs`: construct a `Filing` and call `SetPaymentReference("REF-12345")` (a representative non-null reference), call `PpOpoXmlSerializer.Serialize(filing)`, and `await Verify(xmlBytes, "xml")`; confirm the payment reference appears at the correct location in the XML element hierarchy (FR-002, spec AS US2-2)

- [ ] T011 [US2] Add `Serialize_FullyPopulatedFilingDetails_MatchesSnapshot` to `tests/Rentier.Infrastructure.Tests/Serialization/PpOpoXmlSerializerSnapshotTests.cs`: construct a `Filing` with all optional fields populated (payment reference, all decimal fields set to representative non-zero values, `IncomeType.Interest`, a non-trivial `FilingDeadline`), call `PpOpoXmlSerializer.Serialize(filing)`, and `await Verify(xmlBytes, "xml")`; this snapshot serves as the "filing details" regression baseline (FR-002, spec AS US2-4, research R-002)

- [ ] T012 [P] [US2] Create `tests/Rentier.Infrastructure.Tests/Serialization/IbkrCsvParserSnapshotTests.cs`: declare the class with `[UsesVerify]`; add `ParseAsync_KnownIbkrCsvFixture_MatchesSnapshot` as an `async Task` test that reads the fixture file from `tests/Rentier.Infrastructure.Tests/Fixtures/ibkr-sample.csv` (created in T002), passes it to `IbkrCsvParser.ParseAsync()`, and calls `await Verify(parsedResult)` using Verify's default object serialization; this locks the `StatementParseResult` structure (dividends, interest records, withholding tax, exchange rates, parse errors) against future parser regressions; **depends on T002** (FR-002, spec AS US2-3, research R-001)

- [ ] T013 [US2] After T009–T012 are written, run `dotnet test tests/Rentier.Infrastructure.Tests --filter "FullyQualifiedName~Snapshot"` for the first time to generate the four `.verified.*` baseline files; review each generated baseline for correctness (correct XML structure, correct CSV parse output), then `git add` all `.verified.*` files and commit them alongside the test files so CI can reproduce the comparison on subsequent runs; **depends on T009, T010, T011, T012** (FR-006, SC-006)

**Checkpoint**: T009–T013 done → US2 fully implemented. SC-002 met. All four `.verified.*` baseline files are committed.

---

## Phase 5: User Story 3 — ViewModel Unit Tests for Untested ViewModels (Priority: P3)

**Goal**: Add unit tests for the three ViewModels that currently lack coverage, achieving 100% ViewModel test coverage (SC-003).

**Independent Test**:
```powershell
dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~HolidayEntryViewModel|FullyQualifiedName~SyncProgressEntryViewModel|FullyQualifiedName~ImporterItemViewModel"
# Expect: all 3 test classes found, all tests pass
```

### Implementation for User Story 3

- [ ] T014 [P] [US3] Create `tests/Rentier.UnitTests/Desktop/ViewModels/HolidayEntryViewModelTests.cs` with the following `[Fact]` tests, each following the `MethodName_StateUnderTest_ExpectedBehavior` convention:
  - `FromDto_ValidHolidayEntry_DatePropertyMatchesInput`: construct `HolidayEntryViewModel.FromDto(new HolidayEntryDto(new DateOnly(2025, 6, 15), "Vidovdan"))` and assert `vm.Date == new DateOnly(2025, 6, 15)`
  - `FromDto_ValidHolidayEntry_NamePropertyMatchesInput`: assert `vm.Name == "Vidovdan"`
  - `ToDto_AfterFromDto_ProducesEquivalentDto`: call `vm.ToDto()` and assert `dto.Date == originalDto.Date && dto.Name == originalDto.Name` (round-trip equality)
  - `FromDto_EmptyName_DefaultsToEmptyString`: construct with `Name = ""` and assert `vm.Name == ""`
  - `Name_WhenSetViaSetter_PropertyChanges`: set `vm.Name = "NewName"` and assert `vm.Name == "NewName"` (verifies `RaiseAndSetIfChanged` setter works)
  (FR-003, spec AS US3-1, research R-007)

- [ ] T015 [P] [US3] Create `tests/Rentier.UnitTests/Desktop/ViewModels/SyncProgressEntryViewModelTests.cs` with the following `[Fact]` tests covering all severity derivations and display properties:
  - `Icon_ErrorSeverity_ReturnsXMark`: construct with `SyncProgressSeverity.Error` and assert `vm.Icon == "✕"`
  - `Icon_WarningSeverity_ReturnsWarningSymbol`: construct with `SyncProgressSeverity.Warning` and assert `vm.Icon == "⚠"`
  - `Icon_InfoSeverity_ReturnsBullet`: construct with `SyncProgressSeverity.Info` and assert `vm.Icon == "•"`
  - `Icon_CursorTransitionSeverity_ReturnsBullet`: construct with `SyncProgressSeverity.CursorTransition` and assert `vm.Icon == "•"`
  - `Icon_DuplicateHandledSeverity_ReturnsBullet`: construct with `SyncProgressSeverity.DuplicateHandled` and assert `vm.Icon == "•"`
  - `Message_FromEntry_IsPassedThrough`: assert `vm.Message == entry.Message`
  - `Timestamp_FromEntry_IsFormattedAsHHmmss`: use `new DateTimeOffset(2025, 6, 15, 14, 30, 45, TimeSpan.Zero)` and assert `vm.Timestamp == "14:30:45"`
  - `Severity_FromEntry_IsPassedThrough`: assert `vm.Severity == entry.Severity`
  (FR-003, spec AS US3-2, research R-007, data-model.md SyncProgressEntryViewModel)

- [ ] T016 [P] [US3] Create `tests/Rentier.UnitTests/Desktop/ViewModels/ImporterItemViewModelTests.cs` with the following `[Fact]` tests:
  - `From_ValidImporterDto_IdMatchesInput`: construct via `ImporterItemViewModel.From(dto)` and assert `vm.Id == dto.Id`
  - `From_ValidImporterDto_DisplayNameMatchesInput`: assert `vm.DisplayName == dto.DisplayName`
  - `From_ValidImporterDto_ReportTypeDisplayUsesToDisplayString`: use `ReportType.IbkrCsv` and assert `vm.ReportTypeDisplay == "IBKR CSV"` (the value returned by `ReportType.IbkrCsv.ToDisplayString()`)
  - `From_ValidImporterDto_DtoPropertyIsPreserved`: assert `vm.Dto == dto` (internal pass-through for command binding)
  (FR-003, spec AS US3-3, research R-007, data-model.md ImporterItemViewModel)

**Checkpoint**: T014–T016 done → US3 fully implemented. SC-003 met. All 3 previously-untested ViewModels now have verified behaviour.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate all success criteria, check naming convention compliance, and confirm CI budget is respected.

- [ ] T017 Run the full test suite and verify each measurable success criterion:
  - SC-001: `dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~Properties" --list-tests` → count ≥10
  - SC-002: `dotnet test tests/Rentier.Infrastructure.Tests --filter "FullyQualifiedName~Snapshot" --list-tests` → count ≥5
  - SC-003: all 3 ViewModel test classes pass
  - SC-004: run `dotnet test` three consecutive times with zero failures
  - SC-005: confirm FilingStatusTransitionProperties covers exactly 9 pairs (2 succeed, 7 throw DomainException)
  - SC-006: confirm all `.verified.*` files are tracked by git (`git status` shows no untracked baseline files)
  - SC-007: measure `Measure-Command { dotnet test --no-build 2>&1 | Out-Null }` and confirm increase ≤15% vs pre-feature baseline

- [ ] T018 [P] Review all new test method names for compliance with `MethodName_StateUnderTest_ExpectedBehavior` naming convention (FR-004); rename any test that uses a different pattern before merge

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — **blocks T012 only**
- **US1 (Phase 3)**: Depends on Phase 1; independent of Phase 2 and all other stories
- **US2 XML tests (T009–T011)**: Depends on Phase 1; independent of Phase 2 (no fixture needed)
- **US2 CSV test (T012)**: Depends on Phase 2 (T002 fixture) **and** Phase 1
- **US2 baseline commit (T013)**: Depends on T009, T010, T011, T012 all passing
- **US3 (Phase 5)**: Depends on Phase 1; independent of all other phases
- **Polish (Phase 6)**: Depends on all previous phases complete

### User Story Dependencies

- **US1 (P1)**: Starts after T001 — no dependency on US2 or US3
- **US2 (P2)**: T009–T011 start after T001; T012 starts after T002; T013 waits for all four snapshot tests
- **US3 (P3)**: Starts after T001 — no dependency on US1 or US2

### Within Each User Story

- US1: T003 and T004 edit the same file (`TaxCalculationProperties.cs`) — do sequentially; T005, T006, T007, T008 are in separate files — can proceed in parallel
- US2: T009, T010, T011 edit the same file (`PpOpoXmlSerializerSnapshotTests.cs`) — do sequentially; T012 is a different file — can proceed in parallel with T009–T011; T013 waits for all four
- US3: T014, T015, T016 are in separate files — all three can proceed in parallel

### Parallel Opportunities

| Parallel group | Tasks | Condition |
|---|---|---|
| After T001 | T002, T003, T014 | All can start together |
| US1 parallel set | T005, T006, T007, T008 | After T003 is done (same file risk resolved) |
| US2 XML + US1 + US3 | T009, T005–T008, T014–T016 | All independent files |
| US2 CSV + US2 XML | T012, T009–T011 | T012 also needs T002; T009–T011 do not |
| Polish | T017, T018 | After all implementation tasks |

---

## Parallel Example: User Story 1

```powershell
# After T001 (build check), these four tasks can be launched simultaneously
# by separate agents/developers (each touches a different file):

Task T005: "Extend FilingDeadlineProperties.cs with holiday avoidance property"
Task T006: "Create FilingStatusTransitionProperties.cs with transition properties"
Task T007: "Create PaginationProperties.cs with no-loss/no-duplication property"
Task T008: "Create PaymentReferenceProperties.cs with round-trip and length properties"

# T003 and T004 both edit TaxCalculationProperties.cs — run sequentially:
Task T003 → Task T004
```

## Parallel Example: User Story 3

```powershell
# All three ViewModel test files are independent — launch simultaneously:

Task T014: "Create HolidayEntryViewModelTests.cs"
Task T015: "Create SyncProgressEntryViewModelTests.cs"
Task T016: "Create ImporterItemViewModelTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (T001)
2. Run US1 tasks — T003 → T004, then T005/T006/T007/T008 in parallel
3. **STOP and VALIDATE**: `dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~Properties"` → ≥10 tests, all pass
4. SC-001 achieved. Property invariants locked.

### Incremental Delivery

1. Phase 1 (T001) → foundation verified
2. Phase 3 US1 (T003–T008) → domain invariants locked → validate SC-001
3. Phase 2 + Phase 4 US2 (T002, T009–T013) → serialization locked → validate SC-002, SC-006
4. Phase 5 US3 (T014–T016) → ViewModel gaps closed → validate SC-003
5. Phase 6 polish (T017–T018) → all success criteria confirmed → ready for merge

### Parallel Team Strategy

With two developers after T001:
- **Developer A**: T002, then T003 → T004, then T009 → T010 → T011 → T012 → T013
- **Developer B**: T005, T006, T007, T008 in parallel, then T014, T015, T016 in parallel

---

## Notes

- All 18 tasks are test-only. Zero production code changes. No migrations, no schema changes.
- Every new test follows naming: `MethodName_StateUnderTest_ExpectedBehavior` (FR-004).
- FsCheck v3 pattern: `[Property]` attribute, `Property` return type, `(bool).ToProperty()`, sync `.GetAwaiter().GetResult()` for any async domain calls.
- Verify.Xunit pattern: `async Task` test methods, `await Verify(content, "xml")` for XML, `await Verify(obj)` for object graphs, `.verified.*` files committed to version control.
- ViewModel tests: plain `[Fact]` + FluentAssertions. No `IActivatableViewModel` lifecycle or reactive commands in scope.
- [P] tasks = completely different files, no shared state dependencies.
- Commit `.verified.*` baseline files in the same commit as the snapshot test code (T013).
- SC-005 (exactly 2 valid / 7 invalid transition pairs) is verified by T006 logic — exhaustive enumeration, not sampling.
