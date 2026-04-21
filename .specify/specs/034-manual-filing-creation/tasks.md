# Tasks: Manual Filing Creation (034)

**Branch**: `feature/032-033-034-column-xml-manual`
**Input**: `.specify/specs/034-manual-filing-creation/` — spec.md, plan.md, research.md, data-model.md, contracts/ui-contract.md
**Generated**: 2025-07-22

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Parallelizable — different files, no incomplete dependencies
- **[Story]**: Which user story this task belongs to (US1–US5)
- Exact file paths included in all descriptions

---

## Phase 1: Setup

**Purpose**: Confirm project prerequisites and existing foundations are in place before writing new code. No new projects are required — all new code fits within the existing 4-project Clean Architecture structure.

- [ ] T001 Verify branch `feature/032-033-034-column-xml-manual` is active and `dotnet build Rentier.slnx` succeeds from the repo root before starting any implementation

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create the shared Application data types that both command handlers and the ViewModel depend on. **No handler or ViewModel work can begin until T002–T005 are complete.**

⚠️ **CRITICAL**: These records are the foundation for all five user stories. Complete this phase first.

- [ ] T002 [P] Create `CalculateManualFilingCommand` record in `src/Rentier.Application/Commands/CalculateManualFilingCommand.cs` — fields: `TaxpayerProfileId Guid`, `IncomeType IncomeType`, `Ticker string`, `IncomeDate DateOnly`, `Currency string`, `GrossAmount decimal`, `NetReceived decimal?`
- [ ] T003 [P] Create `CreateManualFilingCommand` record in `src/Rentier.Application/Commands/CreateManualFilingCommand.cs` — same fields as `CalculateManualFilingCommand`; handler trims and uppercases Ticker before use
- [ ] T004 [P] Create `ManualFilingPreviewDto` record in `src/Rentier.Application/DTOs/ManualFilingPreviewDto.cs` — fields: `GrossIncomeRsd decimal`, `WhtPaidRsd decimal`, `GrossTaxPayableRsd decimal`, `TaxPayableRsd decimal`, `FilingDeadline DateOnly`, `ExchangeRateValue decimal`, `ExchangeRateSourceDate DateOnly`, `ExchangeRateSourceType ExchangeRateSourceType`
- [ ] T005 Add all `ManualFiling_*` localization keys to `src/Rentier.Desktop/Resources/Strings.resx` per the ui-contract.md Localization Keys table (29 keys: labels, button captions, preview labels, all error messages including `ManualFiling_Error_TickerRequired`, `ManualFiling_Error_GrossRequired`, `ManualFiling_Error_DateRequired`, `ManualFiling_Error_NetExceedsGross`, `ManualFiling_Error_RateNotFound`, `ManualFiling_Error_DuplicateFiling`, `ManualFiling_Error_NoProfile`, `ManualFiling_Error_NetworkFailure`)

**Checkpoint**: T002–T005 complete — Application records and resource strings exist. Handler and ViewModel implementation may now begin.

---

## Phase 3: User Story 1 — Create a Manual Filing with Full Inputs (Priority: P1) 🎯 MVP

**Goal**: A user fills the form (ticker, income type, date, USD, gross 100.00, net 85.00), clicks Calculate (preview shown with all six computed fields), clicks Save, and the filing appears in the Filings list.

**Independent Test**: Open the app → Filings screen → click "New Filing" → fill all fields → Calculate → verify RSD values and deadline in preview → Save → confirm new row appears in the Filings list with filter=All.

### Tests for User Story 1

> **Write these tests FIRST so they fail, then implement until they pass.**

- [ ] T006 [P] [US1] Write `CalculateManualFilingCommandHandlerTests` — happy path with WHT in `tests/Rentier.Application.Tests/CalculateManualFilingCommandHandlerTests.cs`: given valid command with `NetReceived = 85.00m`, handler resolves NBS rate, computes `GrossIncomeRsd`, `WhtPaidRsd`, `GrossTaxPayableRsd`, `TaxPayableRsd`, computes `FilingDeadline`, returns `Result.Ok(ManualFilingPreviewDto)` with all six fields populated; verify `ExchangeRateSourceDate` and `ExchangeRateSourceType` are set on the DTO
- [ ] T007 [P] [US1] Write `CreateManualFilingCommandHandlerTests` — happy path with WHT in `tests/Rentier.Application.Tests/CreateManualFilingCommandHandlerTests.cs`: given valid command with `NetReceived = 85.00m`, handler persists a `Filing` with `ReportId = null`, ticker uppercased and trimmed, `WhtPaidRsd > 0`, `Status = Init`, returns `Result.Ok` with the new `FilingId`; verify `IFilingRepository.AddAsync` was called once
- [ ] T008 [P] [US1] Write `ManualFilingViewModelTests` — full Calculate→Save flow in `tests/Rentier.Desktop.Tests/ManualFilingViewModelTests.cs`: given all fields filled, `CalculateCommand.Execute()` sets `Preview != null` and enables `SaveCommand`; then `SaveCommand.Execute()` calls `CreateManualFilingCommandHandler` and triggers the `navigateBackToFilings` delegate; verify `IsLoading` goes true→false during each command

### Implementation for User Story 1

- [ ] T009 [P] [US1] Implement `CalculateManualFilingCommandHandler` in `src/Rentier.Application/Handlers/CalculateManualFilingCommandHandler.cs`: validate all inputs (ticker non-blank after trim, gross > 0, incomeDate not default, currency non-blank, netReceived ≤ grossAmount if provided); resolve exchange rate via `ExchangeRateResolver.ResolveAsync(incomeDate, currency)`; build rate closure `(_, _) => Task.FromResult(resolution.Rate)` and call `TaxCalculationService.CalculateAsync(incomeType, ticker, incomeDate, grossAmount, wht, rateProvider)`; compute `FilingDeadline` via `FilingDeadlineCalculator.CalculateDeadline(incomeDate, holidayConf)`; return `Result.Ok(new ManualFilingPreviewDto(...))` — no persistence; all failures return `Result.Fail(Error)` per the `Result<T, Error>` pattern
- [ ] T010 [P] [US1] Implement `CreateManualFilingCommandHandler` in `src/Rentier.Application/Handlers/CreateManualFilingCommandHandler.cs`: same five-step orchestration as `CalculateManualFilingCommandHandler` (validate → resolve rate → calculate tax → compute deadline → check duplicates via `IFilingRepository.ExistsByIncomeAsync(taxpayerProfileId, ticker.Trim().ToUpperInvariant(), incomeDate, grossIncomeRsd)`) → create filing via `Filing.CreateFromIncome(...)` with `reportId: null` → persist via `IFilingRepository.AddAsync` → return `Result.Ok(filingId)`; set `ExchangeRateSourceDate` and `ExchangeRateSourceType` on the created filing from the `RateResolution`
- [ ] T011 [US1] Implement `ManualFilingViewModel` in `src/Rentier.Desktop/ViewModels/ManualFilingViewModel.cs`: expose reactive properties `SelectedIncomeType` (default Dividend), `Ticker`, `IncomeDate DateTimeOffset?`, `SelectedCurrency` (default "USD"), `GrossAmountText`, `NetReceivedText`, `Preview ManualFilingPreviewDto?`, `ErrorMessage string?`, `IsLoading bool`; static `AvailableCurrencies` list (15 NBS currencies from ui-contract.md); `CalculateCommand` canExecute = `WhenAnyValue` guard: Ticker.Trim() non-empty AND GrossAmountText parses to > 0 AND IncomeDate != null AND NOT IsLoading; `SaveCommand` canExecute = `Preview != null AND NOT IsLoading`; `CancelCommand` always enabled; on any input property change after calculation, set `Preview = null`; on `WhenActivated` load `TaxpayerProfileId` from `ITaxpayerProfileRepository.GetAsync()` — if null set `ErrorMessage` to `ManualFiling_Error_NoProfile` key value; convert `IncomeDate DateTimeOffset?` to `DateOnly` at command boundary; all commands use `ReactiveCommand.CreateFromTask`
- [ ] T012 [P] [US1] Implement `ManualFilingView.axaml` and `ManualFilingView.axaml.cs` in `src/Rentier.Desktop/Views/`: ReactiveUserControl bound to `ManualFilingViewModel`; layout per ui-contract.md: Income Type toggle group (Dividend/Interest), Ticker TextBox, Income Date DatePicker, Currency ComboBox (ItemsSource = AvailableCurrencies), Gross Amount TextBox, Net Received TextBox (optional); preview panel (IsVisible bound to `Preview != null`) showing all six preview fields formatted per ui-contract.md (`N,NNN.NN RSD` for amounts, `yyyy-MM-dd` for dates, rate source label distinguishing Exact vs Fallback); error banner (IsVisible bound to `ErrorMessage != null`) with dismiss button; ProgressBar IsIndeterminate bound to `IsLoading`; Calculate, Save Filing, Cancel buttons; all labels from Strings.resx
- [ ] T013 [P] [US1] Add `NewFilingCommand ReactiveCommand<Unit, Unit>` to `FilingsViewModel` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: constructor receives `Action navigateToManualFiling` delegate; `NewFilingCommand.Execute()` invokes `navigateToManualFiling()`; command always enabled
- [ ] T014 [P] [US1] Add "New Filing" toolbar button to `src/Rentier.Desktop/Views/FilingsView.axaml`: plus-icon button bound to `NewFilingCommand` placed on the existing toolbar; use `ManualFiling_Title` resource key for tooltip; matches existing toolbar button style
- [ ] T015 [US1] Wire `ManualFilingViewModel` navigation in `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs`: create `ManualFilingViewModel` factory (via DI) with `navigateBackToFilings` delegate that switches `CurrentViewModel` back to the `FilingsViewModel` with filter set to All; pass `navigateToManualFiling` delegate to `FilingsViewModel` constructor; follows the existing delegate-based navigation pattern (see `DashboardViewModel` wiring)
- [ ] T016 [P] [US1] Register `ManualFilingViewModel`, `CalculateManualFilingCommandHandler`, and `CreateManualFilingCommandHandler` in `src/Rentier.Desktop/Composition/CompositionRoot.cs`: `services.AddTransient<ManualFilingViewModel>()` (navigation delegate injected at construction time by MainWindowViewModel factory); `services.AddTransient<ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>, CalculateManualFilingCommandHandler>()` and corresponding `CreateManualFilingCommandHandler` registration
- [ ] T017 [P] [US1] Register new command handler interfaces in `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` if handler registration belongs there per project convention; otherwise confirm T016 registration is sufficient and document the decision

**Checkpoint**: User Story 1 fully functional. Build succeeds, all US1 tests pass. Manual smoke test: open app → Filings → New Filing → fill all fields → Calculate → preview shows RSD values → Save → new row in list.

---

## Phase 4: User Story 2 — Create a Manual Filing Without Withholding (Priority: P1)

**Goal**: A user leaves Net Received blank; WHT is treated as zero; the preview shows `WhtPaidRsd = 0.00 RSD` and `TaxPayable = GrossTaxPayable`; saved filing has `WhtPaidRsd = 0`.

**Independent Test**: Open the form, fill all fields except Net Received, click Calculate, verify `WHT Paid = 0.00 RSD` and `Tax Payable = Gross Tax Payable` in preview, click Save, confirm filing persists with `WhtPaidRsd = 0`.

> No new source files are required — `NetReceived decimal?` is already nullable in both commands and handlers. These tasks add test coverage for this execution path.

- [ ] T018 [P] [US2] Add no-WHT test case to `tests/Rentier.Application.Tests/CalculateManualFilingCommandHandlerTests.cs`: given command with `NetReceived = null`, handler computes `WhtPaidRsd = 0.00m`, `TaxPayableRsd = GrossTaxPayableRsd`; verify the closure passed to `TaxCalculationService` uses WHT = 0
- [ ] T019 [P] [US2] Add no-WHT persistence test to `tests/Rentier.Application.Tests/CreateManualFilingCommandHandlerTests.cs`: given command with `NetReceived = null`, persisted `Filing.WhtPaidRsd = 0m`; verify `IFilingRepository.AddAsync` was called with a filing where `WhtPaidRsd == 0m`
- [ ] T020 [US2] Add no-WHT ViewModel test to `tests/Rentier.Desktop.Tests/ManualFilingViewModelTests.cs`: given `NetReceivedText = ""` (empty), `CalculateCommand.Execute()` sets `Preview.WhtPaidRsd == 0m` and `Preview.TaxPayableRsd == Preview.GrossTaxPayableRsd`; `SaveCommand` remains enabled; `SaveCommand.Execute()` invokes `CreateManualFilingCommandHandler` with `NetReceived = null`

**Checkpoint**: Both WHT-present and WHT-absent paths tested and passing.

---

## Phase 5: User Story 3 — Validation Prevents Incomplete or Invalid Submissions (Priority: P2)

**Goal**: All validation errors (blank ticker, zero gross, missing date, net > gross, rate not found, network error, duplicate filing, missing taxpayer profile) produce visible inline error messages — no exception pop-ups.

**Independent Test**: Submit the form in each invalid state listed in spec.md §US3 acceptance scenarios and confirm the correct `ErrorMessage` appears without any modal dialogs.

> Handler validation logic is already coded in T009/T010. These tasks add explicit test coverage for each validation branch and confirm ViewModel error display wiring.

- [ ] T021 [P] [US3] Add field-validation failure tests to `tests/Rentier.Application.Tests/CalculateManualFilingCommandHandlerTests.cs`: (a) blank ticker after trim → `Result.Fail(Error)` with code matching `ManualFiling_Error_TickerRequired`; (b) `GrossAmount = 0` → error code matching `ManualFiling_Error_GrossRequired`; (c) `IncomeDate = default` → error code matching `ManualFiling_Error_DateRequired`; (d) `NetReceived > GrossAmount` → error code matching `ManualFiling_Error_NetExceedsGross`; (e) `NetReceived < 0` → error result
- [ ] T022 [P] [US3] Add rate-fetch failure tests to `tests/Rentier.Application.Tests/CalculateManualFilingCommandHandlerTests.cs`: (a) `ExchangeRateResolver.ResolveAsync` returns `Result.Fail` (rate not found) → handler returns error matching `ManualFiling_Error_RateNotFound` pattern; (b) resolver throws `HttpRequestException` (network down) → handler catches and returns error matching `ManualFiling_Error_NetworkFailure`
- [ ] T023 [P] [US3] Add duplicate-detection test to `tests/Rentier.Application.Tests/CreateManualFilingCommandHandlerTests.cs`: `IFilingRepository.ExistsByIncomeAsync` returns `true` → handler returns `Result.Fail(Error)` with code matching `ManualFiling_Error_DuplicateFiling`; verify `IFilingRepository.AddAsync` is **not** called
- [ ] T024 [P] [US3] Add error-display ViewModel tests to `tests/Rentier.Desktop.Tests/ManualFilingViewModelTests.cs`: (a) `CalculateCommand` handler returns error → `ErrorMessage` property set to the error's message, `Preview` remains null; (b) `SaveCommand` handler returns duplicate-filing error → `ErrorMessage` set, no navigation occurs; (c) `WhenActivated` with no taxpayer profile → `ErrorMessage = ManualFiling_Error_NoProfile` value, `CalculateCommand` canExecute = false

**Checkpoint**: All validation paths covered by tests. No error scenario results in an unhandled exception or modal dialog.

---

## Phase 6: User Story 4 — Preview Before Committing (Priority: P2)

**Goal**: The preview panel shows all six computed fields with correct formatting before Save is enabled. Changing any input after calculation clears the preview and re-disables Save.

**Independent Test**: Calculate a filing → verify all six fields appear with correct formats (`N,NNN.NN RSD`, `yyyy-MM-dd`, rate source label) → change one input field → verify preview disappears and Save is disabled → recalculate → verify preview re-appears.

> Implementation already in ManualFilingViewModel (T011) and ManualFilingView (T012). These tasks add explicit test coverage and confirm the state machine is correct.

- [ ] T025 [P] [US4] Add preview-fields test to `tests/Rentier.Desktop.Tests/ManualFilingViewModelTests.cs`: after successful `CalculateCommand.Execute()`, verify `Preview.GrossIncomeRsd`, `Preview.WhtPaidRsd`, `Preview.GrossTaxPayableRsd`, `Preview.TaxPayableRsd`, `Preview.FilingDeadline`, `Preview.ExchangeRateValue`, `Preview.ExchangeRateSourceDate`, `Preview.ExchangeRateSourceType` are all populated with expected values from the mocked handler response
- [ ] T026 [P] [US4] Add preview-clear-on-input-change test to `tests/Rentier.Desktop.Tests/ManualFilingViewModelTests.cs`: (a) after calculate, change `Ticker` → `Preview == null`, `SaveCommand.CanExecute == false`; (b) after calculate, change `GrossAmountText` → same result; (c) after calculate, change `IncomeDate` → same result; (d) after calculate, change `SelectedCurrency` → same result; (e) after calculate, change `NetReceivedText` → same result
- [ ] T027 [US4] Add initial-state test to `tests/Rentier.Desktop.Tests/ManualFilingViewModelTests.cs`: on ViewModel construction before any command, `Preview == null`, `SaveCommand.CanExecute == false`, `ErrorMessage == null`, `IsLoading == false`; verify `CalculateCommand.CanExecute == false` when any required field is empty

**Checkpoint**: Preview state machine fully tested. Preview shows correctly → clears on change → re-appears after recalculate.

---

## Phase 7: User Story 5 — Navigate Back Without Saving (Priority: P3)

**Goal**: A user who opens the form and clicks Cancel (or Back) navigates to the Filings list without any filing being persisted.

**Independent Test**: Open the form, fill some fields, click Cancel, confirm the Filings list is unchanged (no new row) and no `IFilingRepository.AddAsync` call was made.

- [ ] T028 [P] [US5] Add cancel-navigation test to `tests/Rentier.Desktop.Tests/ManualFilingViewModelTests.cs`: `CancelCommand.Execute()` invokes the `navigateBackToFilings` delegate; `IFilingRepository.AddAsync` is **never** called; command is always enabled regardless of form state
- [ ] T029 [US5] Add partial-form-cancel test to `tests/Rentier.Desktop.Tests/ManualFilingViewModelTests.cs`: given partially filled fields (Ticker set, GrossAmount set, no date), `CancelCommand.Execute()` still invokes `navigateBackToFilings`; no side effects on the Filings list (verify via mocked `navigateBackToFilings` delegate call count = 1)

**Checkpoint**: All five user stories have passing tests. Full feature is complete.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Build verification, compliance gates, and quickstart validation.

- [ ] T030 [P] Run full build `dotnet build Rentier.slnx` from repo root and confirm zero errors or warnings on new/modified files
- [ ] T031 [P] Run all tests `dotnet test Rentier.slnx --no-build` and confirm all `CalculateManualFilingCommandHandlerTests`, `CreateManualFilingCommandHandlerTests`, and `ManualFilingViewModelTests` pass
- [ ] T032 [P] Audit `src/Rentier.Desktop/Resources/Strings.resx` to confirm all 29 `ManualFiling_*` keys added in T005 are referenced in `ManualFilingView.axaml` or `ManualFilingViewModel.cs` (FR-016 compliance — no hardcoded user-visible strings)
- [ ] T033 Manually execute quickstart.md scenarios end-to-end: (a) full inputs with WHT → calculate → preview → save → appears in list; (b) no WHT → WHT Paid = 0.00 RSD in preview; (c) blank ticker → inline error, no modal; (d) cancel mid-form → no new filing in list
- [ ] T034 Verify Application layer test coverage gate: `CalculateManualFilingCommandHandler` and `CreateManualFilingCommandHandler` must reach ≥ 90% branch coverage per constitution CA-006; run coverage report (`dotnet test --collect:"XPlat Code Coverage"`) and confirm gate passes before merge

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 (build must be green) — **BLOCKS all user story phases**
- **Phase 3 (US1)**: Depends on Phase 2 completion — implements the full feature backbone
- **Phase 4 (US2)**: Depends on Phase 3 completion — adds no-WHT test paths to US1 handlers
- **Phase 5 (US3)**: Depends on Phase 3 completion — adds validation test paths to US1 handlers
- **Phase 6 (US4)**: Depends on Phase 3 completion — adds preview-state tests to US1 ViewModel
- **Phase 7 (US5)**: Depends on Phase 3 completion — adds cancel-navigation tests to US1 ViewModel
- **Phases 4–7**: Can proceed in **parallel** once Phase 3 is complete (all test-only additions to separate classes)
- **Phase 8 (Polish)**: Depends on Phases 3–7 completion

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational (Phase 2) — no dependency on other stories
- **US2 (P1)**: Depends on US1 handler implementation (T009, T010) — tests the null-NetReceived branch
- **US3 (P2)**: Depends on US1 handler implementation (T009, T010) and ViewModel (T011) — tests error paths
- **US4 (P2)**: Depends on US1 ViewModel (T011) — tests preview state management
- **US5 (P3)**: Depends on US1 ViewModel (T011) — tests cancel navigation

### Within Phase 3 (US1) — Internal Dependencies

```
T006, T007, T008 (tests) — parallel, write first
T009, T010 (handlers)    — parallel (different files), after tests fail
T011 (ViewModel)         — after T009, T010 (calls both handlers)
T012 (View)              — parallel with T011 (different files)
T013, T014 (Filings wiring) — parallel (different files), after T011
T015 (MainWindowVM)      — after T011, T013 (wires navigation)
T016, T017 (DI/Infra)    — parallel (different files), after T009, T010
```

### Parallel Opportunities

- All Phase 2 tasks (T002–T005) run in parallel
- Within Phase 3: T006/T007/T008 tests in parallel; T009/T010 handlers in parallel; T012/T013/T014 in parallel
- Phases 4, 5, 6, 7 run in parallel with each other once Phase 3 completes
- Phase 8: T030/T031/T032 in parallel

---

## Parallel Example: Phase 3 (US1)

```text
# Write all three test stubs in parallel (all fail):
Task: "CalculateManualFilingCommandHandlerTests — happy path with WHT" (T006)
Task: "CreateManualFilingCommandHandlerTests — happy path with WHT"    (T007)
Task: "ManualFilingViewModelTests — Calculate+Save flow"               (T008)

# Implement both handlers in parallel (different files):
Task: "CalculateManualFilingCommandHandler"  (T009)
Task: "CreateManualFilingCommandHandler"     (T010)

# Once handlers exist, implement ViewModel + View + wiring in parallel:
Task: "ManualFilingViewModel"     (T011)
Task: "ManualFilingView.axaml"    (T012)
Task: "FilingsViewModel changes"  (T013)
Task: "FilingsView.axaml changes" (T014)
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only)

1. Complete Phase 1: Setup verification
2. Complete Phase 2: Foundational records + strings (T002–T005)
3. Complete Phase 3: User Story 1 — full filing creation flow (T006–T017)
4. Complete Phase 4: User Story 2 — no-WHT path tests (T018–T020)
5. **STOP and VALIDATE**: Manual smoke test per quickstart.md steps (a) and (b)
6. Demo / merge to branch if MVP is satisfactory

### Incremental Delivery

1. Setup + Foundational → Application records compiled, strings added
2. User Story 1 → Full filing creation works end-to-end (MVP!)
3. User Story 2 → No-WHT path verified with tests
4. User Story 3 → All validation errors produce inline messages (polish)
5. User Story 4 → Preview state machine tested (polish)
6. User Story 5 → Cancel navigation tested (polish)
7. Phase 8 → Coverage gate passes, manual validation complete

---

## Summary

| Phase | User Story | Tasks | Files Changed |
|-------|------------|-------|--------------|
| 1 — Setup | — | T001 | — |
| 2 — Foundational | — | T002–T005 | 4 new files + Strings.resx |
| 3 — US1 (P1) 🎯 | Full filing with WHT | T006–T017 | 5 new, 5 modified |
| 4 — US2 (P1) | No-WHT path | T018–T020 | 3 test additions |
| 5 — US3 (P2) | Validation errors | T021–T024 | 3 test additions |
| 6 — US4 (P2) | Preview behavior | T025–T027 | 3 test additions |
| 7 — US5 (P3) | Cancel navigation | T028–T029 | 2 test additions |
| 8 — Polish | — | T030–T034 | — |
| **Total** | **5 stories** | **34 tasks** | **9 new, 5 modified** |

**Suggested MVP scope**: Complete Phases 1–4 (T001–T020) for a fully functional manual filing creation feature covering both WHT-present and WHT-absent paths.
