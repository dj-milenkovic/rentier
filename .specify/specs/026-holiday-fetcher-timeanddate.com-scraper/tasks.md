# Tasks: 026 — Holiday Fetcher (timeanddate.com Scraper)

**Feature**: 026-holiday-fetcher-timeanddate.com-scraper  
**Branch**: `026-holiday-web-scraper`  
**Input**: `.specify/specs/026-holiday-fetcher-timeanddate.com-scraper/`

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no in-progress dependencies)
- **[US#]**: User story label (maps to spec.md stories)

## Pre-conditions — Already Complete (Do NOT Recreate)

| Artifact | Location |
|---|---|
| `TimeAndDateHolidayScraper` | `src/Rentier.Infrastructure/Scraping/TimeAndDateHolidayScraper.cs` |
| `IHolidayImporter` | `src/Rentier.Application/Interfaces/IHolidayImporter.cs` |
| `ImportHolidaysFromWebCommand` | `src/Rentier.Application/Commands/ImportHolidaysFromWebCommand.cs` |
| `ImportHolidaysFromWebCommandHandler` | `src/Rentier.Application/Handlers/ImportHolidaysFromWebCommandHandler.cs` |
| DI registration stub for `FetchHolidaysFromWebCommandHandler` | `src/Rentier.Desktop/Composition/CompositionRoot.cs` lines 50–52 (will compile once T001 + T002 exist) |

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: The command record that ALL Application and Desktop tasks depend on. DI is already wired in `CompositionRoot.cs` — it just needs these two types to exist before the project compiles.

**⚠️ CRITICAL**: T001 must complete before any other task. T002 must complete before T004, T005, T006.

- [X] T001 Create `src/Rentier.Application/Commands/FetchHolidaysFromWebCommand.cs` — `public sealed record FetchHolidaysFromWebCommand(int StartYear, int EndYear);` in namespace `Rentier.Application.Commands`

- [X] T002 [P] Add three string resources to `src/Rentier.Desktop/Resources/Strings.resx` then add matching `static string` properties to `Strings.Designer.cs`: `Holidays_FetchFromWeb_Button` = "Fetch from web" · `Holidays_FetchFromWeb_Success` = "Fetched {0} holidays for {1} year(s)" · `Holidays_FetchFromWeb_PartialFailure` = "Fetched {0} holidays; {1} year(s) failed to fetch"

**Checkpoint**: T001 done → project compiles (DI stub no longer a compile error). T002 done → VM string references resolve.

---

## Phase 2: User Story 1 + 2 — Single-Year Fetch & Error Handling (Priority: P1) 🎯 MVP

**Goal**: "Fetch from web" button fetches Serbian public holidays for the configured year range, merges results into `Entries` de-duplicating by `DateOnly`, shows success/failure messages, and preserves existing data on failure.

**Independent Test**: Set `StartYear = EndYear = 2026`. Click "Fetch from web". Verify: (a) Serbian national holidays appear in the grid, (b) `HasUnsavedChanges = true`, (c) clicking again adds zero duplicates. Disconnect internet, click button — verify error message appears and existing entries are unchanged.

### Tests for US1+US2 ⚠️ Write first — ensure they FAIL before T005–T006

- [X] T003 [P] [US1] Create `tests/Rentier.Application.Tests/FetchHolidaysFromWebCommandHandlerTests.cs`: write `HandleAsync_SingleYear_Success_ReturnsList` (importer returns 3 holidays → handler returns Success with those 3 holidays), `HandleAsync_SingleYear_ImporterFailure_ReturnsFailure` (importer returns Error → handler returns Failure), `HandleAsync_StartEqualsEnd_CallsImporterOnce` (StartYear == EndYear → `ImportAsync` called exactly once with that year)

- [X] T004 [P] [US1] Create `tests/Rentier.Desktop.Tests/HolidaySettingsViewModelFetchTests.cs`: write `FetchFromWebCommand_IsNotNull_AfterConstruction`, `FetchFromWebCommand_OnSuccess_AddsOnlyNewDates` (pre-load entry for 2026-01-01 → fetch returns same date + one new date → only new date added; count = 1), `FetchFromWebCommand_OnSuccess_SetsHasUnsavedChanges`, `FetchFromWebCommand_OnSuccess_ShowsSuccessMessage`, `FetchFromWebCommand_OnFailure_ShowsErrorMessage`, `FetchFromWebCommand_OnFailure_PreservesExistingEntries`, `FetchFromWebCommand_WhenIsLoadingTrue_CannotExecute`

### Implementation for US1+US2

- [X] T005 [US1] Create `src/Rentier.Application/Handlers/FetchHolidaysFromWebCommandHandler.cs`: implement `ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>`; inject `IHolidayImporter`; loop `cmd.StartYear` through `cmd.EndYear` inclusive calling `_importer.ImportAsync(year, ct)` sequentially; collect all returned DTOs and deduplicate by `DateOnly` using a `HashSet<DateOnly>`; track per-year failures; if `failedYearCount == totalYears` return `Result.Failure` with aggregated error message; otherwise return `Result.Success(deduplicatedList)` — no EF Core, no infrastructure dependencies; lives entirely in `Rentier.Application`

- [X] T006 [US1] Refactor `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs`:
  - (a) Replace constructor parameter `ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>> importHandler` with `ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>> fetchHandler`
  - (b) Remove `_importYear` backing field and public `ImportYear` property
  - (c) Remove `ImportCommand` public property declaration and its `ReactiveCommand.CreateFromTask<int>` initialization block
  - (d) Change `FetchFromWebCommand` declaration from `ReactiveCommand<int, Unit>` to `ReactiveCommand<Unit, Unit>`
  - (e) Initialize `FetchFromWebCommand` via `ReactiveCommand.CreateFromTask(async (CancellationToken ct) => { ... }, notLoading)` — body: dispatch `new FetchHolidaysFromWebCommand(StartYear, EndYear)`, on success merge using `var existingDates = Entries.Select(e => e.Date).ToHashSet(); foreach (dto in result.Value) if (existingDates.Add(dto.Date)) { Entries.Add(HolidayEntryViewModel.FromDto(dto)); added++; }` — set `HasUnsavedChanges = true` when `added > 0`, set `SuccessMessage = string.Format(Strings.Holidays_FetchFromWeb_Success, added, EndYear - StartYear + 1)`; on failure set `ErrorMessage = result.Error.Message`; always restore `IsLoading = false` in `finally`
  - (f) In `WhenActivated`: replace `ImportCommand.ThrownExceptions.Subscribe(...)` with `FetchFromWebCommand.ThrownExceptions.Subscribe(ex => ErrorMessage = ex.Message)`

- [X] T007 [US1] Update `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`: in the top `StackPanel` (DockPanel.Dock="Top") — remove the `<TextBlock Text="Year:" .../>` element, the `<NumericUpDown Value="{Binding ImportYear}" .../>` element, and the `<Button ... Command="{Binding ImportCommand}" .../>` element; add `<Button Content="{x:Static res:Strings.Holidays_FetchFromWeb_Button}" Command="{Binding FetchFromWebCommand}" />` after the Save button

**Checkpoint**: T003 + T004 green. Single-year fetch end-to-end works. US1 + US2 acceptance scenarios met.

---

## Phase 3: User Story 3 + 4 — Multi-Year Fetch & Date Validation (Priority: P2)

**Goal**: "Fetch from web" loops `StartYear..EndYear` (up to 11 years); partial per-year failure continues rather than aborting; overall failure only when ALL years fail; dates outside the requested year are excluded before merge (FR-011); partial success shows a distinct warning message.

**Independent Test**: Set `StartYear = 2024`, `EndYear = 2026`. Click "Fetch from web". Verify holidays for all three years appear in the grid. Then mock 2025 to fail — verify 2024 and 2026 holidays still appear and a partial-failure message is shown. Mock all three years to fail — verify error message shown and no entries added.

### Tests for US3+US4 ⚠️ Write first — extend the test files from Phase 2

- [X] T008 [P] [US3] Extend `tests/Rentier.Application.Tests/FetchHolidaysFromWebCommandHandlerTests.cs`: add `HandleAsync_MultiYear_AllSucceed_ReturnsMergedDeduplicatedList` (3 years, same date in years 2024 and 2025 → deduped to one entry), `HandleAsync_MultiYear_OneYearFails_ReturnsSuccessWithOtherYears` (2025 fails → Success with 2024+2026 holidays), `HandleAsync_MultiYear_AllYearsFail_ReturnsFailure`, `HandleAsync_DateOutsideRequestedYear_Excluded` (FR-011: importer returns a DTO whose `Date.Year` ≠ requested year → that entry excluded from merged list)

- [X] T009 [P] [US3] Extend `tests/Rentier.Desktop.Tests/HolidaySettingsViewModelFetchTests.cs`: add `FetchFromWebCommand_OnPartialFailure_ShowsPartialFailureOrSuccessMessage` (handler returns Success for partial case — verify SuccessMessage is non-null), `FetchFromWebCommand_MultiYear_MergesHolidaysFromAllYears` (handler returns holidays spanning 3 years → all appear in Entries), `FetchFromWebCommand_UsesStartYearAndEndYear_FromVmState` (set `StartYear = 2024`, `EndYear = 2026` → verify handler receives `FetchHolidaysFromWebCommand(2024, 2026)`)

### Implementation for US3+US4

- [X] T010 [US3] Extend `src/Rentier.Application/Handlers/FetchHolidaysFromWebCommandHandler.cs` with FR-011 date validation and partial-failure tracking: (a) after calling `ImportAsync(year, ct)` per year, filter the returned list to exclude any `HolidayEntryDto` where `dto.Date.Year != year` before adding to the aggregate; (b) collect failed years in a `List<int> failedYears`; (c) after the loop, if `failedYears.Count == totalYears` return `Result.Failure(new Error("HOLIDAY_FETCH_ALL_FAILED", $"Failed to fetch holidays for all {totalYears} year(s)"))`, otherwise return `Result.Success(deduplicatedList)` — the ViewModel can infer partial failure by comparing `result.Value` distinct years against `(EndYear - StartYear + 1)` if needed for messaging

- [ ] T011 [US3] Update `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs` partial-failure detection: after a successful fetch, compute `int distinctYears = result.Value.Select(d => d.Date.Year).Distinct().Count(); int requestedYears = EndYear - StartYear + 1;` — if `distinctYears < requestedYears && added > 0`, set `SuccessMessage = string.Format(Strings.Holidays_FetchFromWeb_PartialFailure, added, requestedYears - distinctYears)` instead of the standard success message

**Checkpoint**: T008 + T009 green. Multi-year fetch with partial failure resilience fully exercised.

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Fix existing tests broken by ViewModel refactor, verify build health.

- [ ] T012 [P] Update `tests/Rentier.Desktop.Tests/HolidaySettingsViewModelTests.cs`: (a) replace `MockImportHandler()` helper and its type `ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>` with `MockFetchHandler()` returning `ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>`; (b) update `CreateVm()` factory to inject fetch handler instead of import handler; (c) replace all `vm.ImportCommand.Execute(...)` call-sites with `vm.FetchFromWebCommand.Execute(Unit.Default)`; (d) remove tests `ImportYear_DefaultsToCurrentYear`, `ImportYear_WhenSet_ReflectsNewValue`, `ImportCommand_WhenIsLoadingTrue_CannotExecute` (those properties no longer exist); (e) add `FetchFromWebCommand_WhenIsLoadingTrue_CannotExecute` equivalent; (f) update `ImportCommand_OnSuccess_MergesIntoEntries` → rename to `FetchFromWebCommand_OnSuccess_MergesIntoEntries` and use the merge-not-replace assertion (existing entries with same date are NOT cleared)

- [ ] T013 [P] Verify `src/Rentier.Desktop/Composition/CompositionRoot.cs`: confirm lines 50–52 already contain `services.AddTransient<ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>, FetchHolidaysFromWebCommandHandler>()` — no edit needed if present; also confirm usings for `FetchHolidaysFromWebCommand` and `FetchHolidaysFromWebCommandHandler` are present or add them

- [ ] T014 Run `dotnet build` from repository root — confirm zero errors and zero warnings; fix any missing `using` directives or namespace mismatches

- [ ] T015 [P] Run `dotnet test` from repository root — confirm all tests pass; specifically `FetchHolidaysFromWebCommandHandlerTests`, `HolidaySettingsViewModelFetchTests`, and updated `HolidaySettingsViewModelTests` are all green

- [ ] T016 [P] Validate quickstart scenario from `quickstart.md`: launch app → navigate to Holidays settings → set `StartYear = 2026`, `EndYear = 2026` → click "Fetch from web" → confirm ~9–11 Serbian national holidays appear → click Save → restart app → confirm holidays persisted

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Foundational)**: No dependencies — start immediately
  - T001 blocks: T003, T004, T005, T006, T007, T008, T009, T010, T011, T012, T013
  - T002 blocks: T006, T007 (string resource references in VM and XAML)
- **Phase 2 (US1+US2)**: Requires T001 complete
  - T003 and T004 can start as soon as T001 is done (run in parallel with T002)
  - T005 (handler) must complete before T006 (VM) to allow compile-time verification
  - T006 (VM) must complete before T007 (XAML binding names)
  - T013 (DI verification) can run in parallel with T005–T007
- **Phase 3 (US3+US4)**: Requires Phase 2 complete (T005 + T006)
  - T008 and T009 (tests) can run in parallel with each other after T001
  - T010 (handler extension) requires T005 base implementation
  - T011 (VM partial-failure) requires T006 base refactor
- **Phase 4 (Polish)**: Requires Phases 2 and 3 complete
  - T012, T013 can run in parallel
  - T014 before T015
  - T015 and T016 can run in parallel

### User Story Dependencies

- **US1+US2 (P1)**: No dependency on US3/US4 — deliverable as MVP after Phase 2
- **US3+US4 (P2)**: Extends Phase 2 handler and ViewModel; independently testable

### Within Each Phase

- Write tests BEFORE implementation (verify they FAIL first)
- Command record (T001) before handler (T005)
- Handler (T005) before ViewModel refactor (T006)
- ViewModel (T006) before XAML (T007)
- All implementation before Phase 4 polish

---

## Parallel Execution Examples

### Phase 2 (US1+US2) — After T001 + T002 complete

```
Parallel — write tests first:
  T003  FetchHolidaysFromWebCommandHandlerTests (Application.Tests)
  T004  HolidaySettingsViewModelFetchTests (Desktop.Tests)

Sequential — implementation:
  T005  FetchHolidaysFromWebCommandHandler.cs
  T006  HolidaySettingsViewModel.cs          ← depends on T005 compile
  T007  HolidaySettingsView.axaml            ← depends on T006 binding names

Parallel with sequential group:
  T013  Verify CompositionRoot.cs DI stub    ← independent of T005–T007
```

### Phase 3 (US3+US4) — After Phase 2 complete

```
Parallel — extend tests:
  T008  Extend FetchHolidaysFromWebCommandHandlerTests
  T009  Extend HolidaySettingsViewModelFetchTests

Sequential — implementation:
  T010  Extend FetchHolidaysFromWebCommandHandler (FR-011 + partial failure)
  T011  Extend HolidaySettingsViewModel (partial failure message)
```

---

## Implementation Strategy

### MVP First (US1+US2 — Phase 2 Only)

1. Complete Phase 1: T001, T002 (10 min)
2. Write failing tests: T003, T004 (parallel, ~30 min)
3. Implement: T005 → T006 → T007; verify T013 (60–90 min)
4. **STOP and VALIDATE**: T003 + T004 green; smoke-test in running app
5. Merge US1+US2 if ready — US3+US4 ships as a follow-up

### Incremental Delivery

1. Phase 1 → Foundation ready; project compiles
2. Phase 2 → US1+US2 functional, all P1 acceptance scenarios met
3. Phase 3 → US3+US4 adds multi-year loop, partial failure, date validation
4. Phase 4 → Tests clean, build green → merge to main

---

## Architecture Notes

- `FetchHolidaysFromWebCommandHandler` lives in `Rentier.Application` — no EF Core, no `DbContext`
- `IHolidayImporter` is the only outbound dependency; called only on explicit user action (CA-EXT-001 ✅)
- `FetchFromWebCommand` in ViewModel is `ReactiveCommand<Unit, Unit>` — year range captured from `StartYear`/`EndYear` VM state, not passed as a command parameter
- De-duplication is by `DateOnly` value: existing entries with the same date are silently skipped; names are never overwritten
- `HasUnsavedChanges` is set to `true` only when at least one new entry is actually added
- The existing `ImportHolidaysFromWebCommandHandler` registration in `CompositionRoot.cs` is preserved — do not remove it
