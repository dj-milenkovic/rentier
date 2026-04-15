# Tasks: Bulk Delete for Filings and Reports

**Input**: Design documents from `specs/025-bulk-delete-fillings-reports/`
**Branch**: `025-bulk-delete-fillings-reports`
**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ data-model.md ✅ contracts/ ✅ quickstart.md ✅

**Tests**: Included — Application-layer handler tests and Desktop-layer ViewModel tests are required by spec (CA-006) and constitution quality gates (≥90% Application coverage).

**Organization**: Tasks grouped by user story. US1 (Filings bulk delete) and US2 (Reports bulk delete) are both P1 and can be implemented in parallel after Phase 2 completes. US3 and US4 are P2 polish phases.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)
- Exact file paths are included in all descriptions

---

## Phase 1: Setup

**Purpose**: Confirm the working branch is clean before any modifications.

- [ ] T001 Checkout branch `025-bulk-delete-fillings-reports` and confirm `dotnet build Rentier.slnx` succeeds with zero errors or warnings

**Checkpoint**: Branch is clean — implementation may begin.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Repository interface extensions and shared localisation strings that every user story depends on. No story can be wired end-to-end until these exist.

**⚠️ CRITICAL**: Phases 3–6 all depend on this phase being complete.

- [ ] T002 [P] Add `Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)` method signature to `src/Rentier.Application/Repositories/IFilingRepository.cs`
- [ ] T003 [P] Add `Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)` method signature to `src/Rentier.Application/Repositories/IReportRepository.cs`
- [ ] T004 Implement `DeleteManyAsync` in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` using `Where(e => ids.Contains(e.Id))` → `ToListAsync` → `RemoveRange` → `SaveChangesAsync` (load-then-remove pattern; empty list is a no-op)
- [ ] T005 [P] Implement `DeleteManyAsync` in `src/Rentier.Infrastructure/Repositories/ReportRepository.cs` using the same load-then-remove pattern as T004
- [ ] T006 Add all 10 `BulkDelete_*` string resources to `src/Rentier.Desktop/Resources/Strings.resx`: `BulkDelete_SelectAll_Button`, `BulkDelete_ClearSelection_Button`, `BulkDelete_Button_Template` (`Delete Selected ({0})`), `BulkDelete_Filings_Confirmation_Title`, `BulkDelete_Filings_Confirmation_Message`, `BulkDelete_Reports_Confirmation_Title`, `BulkDelete_Reports_Confirmation_Message`, `BulkDelete_Confirm_Button`, `BulkDelete_Cancel_Button`, `BulkDelete_Error_Failed`

**Checkpoint**: Foundation ready — `dotnet build Rentier.slnx` must still succeed. User story phases can now begin.

---

## Phase 3: User Story 1 — Bulk Delete Filings (Priority: P1) 🎯 MVP

**Goal**: Users can select multiple filings via checkboxes, see the reactive count in the toolbar, confirm deletion via dialog, and have the filings removed with the list refreshed.

**Independent Test**: Navigate to the Filings page, check 3 of 5 filings, verify "Delete Selected (3)" appears in toolbar, confirm deletion, verify 3 filings removed and list shows 2, and selection is cleared.

### Application Layer

- [ ] T007 [P] [US1] Create `BulkDeleteFilingsCommand` record in `src/Rentier.Application/Commands/BulkDeleteFilingsCommand.cs` with `IReadOnlyList<Guid> FilingIds` as per cqrs-commands.md contract
- [ ] T008 [US1] Create `BulkDeleteFilingsCommandHandler` in `src/Rentier.Application/Handlers/BulkDeleteFilingsCommandHandler.cs` implementing `ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>`: validate `FilingIds` non-null/non-empty (return `Error.Domain` if invalid), call `_filingRepository.DeleteManyAsync(command.FilingIds, ct)`, return `Success(VoidResult)`, wrap exceptions in `Failure(Error("BULK_DELETE_FILINGS_FAILED", ex.Message))`

### Application Tests

- [ ] T009 [P] [US1] Create `BulkDeleteFilingsCommandHandlerTests` in `tests/Rentier.Application.Tests/BulkDeleteFilingsCommandHandlerTests.cs` with NSubstitute mocks covering: null `FilingIds` returns domain error, empty list returns domain error, valid IDs call `DeleteManyAsync` and return success, exception from repository is wrapped in failure result, cancellation token is forwarded

### Desktop Row ViewModel

- [ ] T010 [P] [US1] Add `IsSelected` observable property (ReactiveUI `RaiseAndSetIfChanged` pattern) to `src/Rentier.Desktop/ViewModels/FilingRowViewModel.cs`

### Desktop Parent ViewModel

- [ ] T011 [US1] Add reactive observable properties to `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: `SelectedCount` (`int`, aggregated from `Rows.Count(r => r.IsSelected)` via row `WhenAnyValue` subscriptions), `HasSelection` (`bool`, `SelectedCount > 0` via `WhenAnyValue`), `DeleteSelectedLabel` (`string`, `string.Format(Strings.BulkDelete_Button_Template, SelectedCount)`) — all backed by `ObservableAsPropertyHelper`
- [ ] T012 [US1] Add commands to `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: `SelectAllCommand` (`ReactiveCommand<Unit,Unit>`, `canExecute: HasItems`, sets all `Rows[n].IsSelected = true`), `ClearSelectionCommand` (`canExecute: HasSelection`, sets all `Rows[n].IsSelected = false`), `BulkDeleteCommand` (`ReactiveCommand.CreateFromTask`, `canExecute: HasSelection && !IsExecuting`) — execution flow: collect selected IDs → show `ConfirmDialogHelper` with filings message → if cancelled return → dispatch `BulkDeleteFilingsCommand` → on error set `ErrorMessage` → apply page-decrement edge case (if all visible items deleted and `_currentPage > 1` decrement page) → reload list → selection auto-cleared by fresh `Rows` collection

### DI Registration

- [ ] T013 [US1] Register `BulkDeleteFilingsCommandHandler` as `ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>` (transient) in `src/Rentier.Desktop/Composition/CompositionRoot.cs`; inject handler into `FilingsViewModel` constructor

### View

- [ ] T014 [US1] Add `DataGridTemplateColumn` (Width=40) as the **first** column in the Filings `DataGrid` in `src/Rentier.Desktop/Views/FilingsView.axaml` with a `CheckBox` cell template bound `{Binding IsSelected, Mode=TwoWay}` and `HorizontalAlignment="Center"` per ui-contracts.md
- [ ] T015 [US1] Add three toolbar buttons to the Filings toolbar in `src/Rentier.Desktop/Views/FilingsView.axaml`: "Select All" (`Command={Binding SelectAllCommand}`, `IsVisible={Binding HasItems}`, `Content={x:Static res:Strings.BulkDelete_SelectAll_Button}`), "Clear Selection" (`Command={Binding ClearSelectionCommand}`, `IsVisible={Binding HasItems}`, `Content={x:Static res:Strings.BulkDelete_ClearSelection_Button}`), "Delete Selected (N)" (`Command={Binding BulkDeleteCommand}`, `IsVisible={Binding HasSelection}`, `Content={Binding DeleteSelectedLabel}`, `Foreground="Red"`)

### Desktop Tests

- [ ] T016 [US1] Create `FilingsViewModelBulkDeleteTests` in `tests/Rentier.Desktop.Tests/FilingsViewModelBulkDeleteTests.cs` covering: `SelectedCount` updates when row `IsSelected` toggles, `HasSelection` transitions, `DeleteSelectedLabel` format, `SelectAllCommand` sets all rows, `ClearSelectionCommand` clears all rows, `BulkDeleteCommand` shows dialog with correct filings message, cancel leaves selection intact, confirm dispatches command with correct IDs, successful delete reloads list, page-decrement logic when all page-1 items selected on page > 1

**Checkpoint**: Filings bulk delete is fully functional and independently testable. `dotnet test tests/Rentier.Application.Tests` and `dotnet test tests/Rentier.Desktop.Tests` must pass.

---

## Phase 4: User Story 2 — Bulk Delete Reports with Cascade Warning (Priority: P1)

**Goal**: Users can bulk-delete reports from the Reports page; the confirmation dialog explicitly warns that all linked filings will also be deleted; cascade deletion removes linked filings before reports.

**Independent Test**: Navigate to the Reports page, select reports (some with linked filings), trigger bulk delete, verify the confirmation dialog mentions cascade deletion of linked filings, confirm, verify reports and their linked filings are removed and the list refreshes with cleared selection.

### Application Layer

- [ ] T017 [P] [US2] Create `BulkDeleteReportsCommand` record in `src/Rentier.Application/Commands/BulkDeleteReportsCommand.cs` with `IReadOnlyList<Guid> ReportIds` as per cqrs-commands.md contract
- [ ] T018 [US2] Create `BulkDeleteReportsCommandHandler` in `src/Rentier.Application/Handlers/BulkDeleteReportsCommandHandler.cs` implementing `ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>`: validate `ReportIds` non-null/non-empty (return `Error.Domain` if invalid), loop `_filingRepository.DeleteByReportIdAsync(reportId, ct)` per report ID (cascade), call `_reportRepository.DeleteManyAsync(command.ReportIds, ct)`, return `Success(VoidResult)`, wrap exceptions in `Failure(Error("BULK_DELETE_REPORTS_FAILED", ex.Message))`

### Application Tests

- [ ] T019 [P] [US2] Create `BulkDeleteReportsCommandHandlerTests` in `tests/Rentier.Application.Tests/BulkDeleteReportsCommandHandlerTests.cs` with NSubstitute mocks covering: null `ReportIds` returns domain error, empty list returns domain error, `DeleteByReportIdAsync` is called for each report ID before `DeleteManyAsync`, valid IDs return success, exception wrapped in failure, cancellation token forwarded

### Desktop Row ViewModel

- [ ] T020 [P] [US2] Add `IsSelected` observable property (ReactiveUI `RaiseAndSetIfChanged` pattern) to `src/Rentier.Desktop/ViewModels/ReportRowViewModel.cs`

### Desktop Parent ViewModel

- [ ] T021 [US2] Add reactive observable properties to `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`: `SelectedCount`, `HasSelection`, `DeleteSelectedLabel` — using identical `WhenAnyValue` / `ObservableAsPropertyHelper` pipeline as T011, derived from `Rows` rows' `IsSelected` changes
- [ ] T022 [US2] Add commands to `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`: `SelectAllCommand`, `ClearSelectionCommand`, `BulkDeleteCommand` — identical structure to T012, **except** `BulkDeleteCommand` uses `BulkDelete_Reports_Confirmation_Title` / `BulkDelete_Reports_Confirmation_Message` (which includes the cascade warning) and dispatches `BulkDeleteReportsCommand`; no page-decrement logic (Reports page is not paginated)

### DI Registration

- [ ] T023 [US2] Register `BulkDeleteReportsCommandHandler` as `ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>` (transient) in `src/Rentier.Desktop/Composition/CompositionRoot.cs`; inject handler into `ReportsViewModel` constructor

### View

- [ ] T024 [US2] Add `DataGridTemplateColumn` (Width=40) as the **first** column in the Reports `DataGrid` in `src/Rentier.Desktop/Views/ReportsView.axaml` with `CheckBox` cell template bound `{Binding IsSelected, Mode=TwoWay}` per ui-contracts.md
- [ ] T025 [US2] Add the three bulk-select toolbar buttons to the Reports toolbar in `src/Rentier.Desktop/Views/ReportsView.axaml` using the same bindings as T015 (`SelectAllCommand`, `ClearSelectionCommand`, `BulkDeleteCommand`, `HasItems`, `HasSelection`, `DeleteSelectedLabel`)

### Desktop Tests

- [ ] T026 [US2] Create `ReportsViewModelBulkDeleteTests` in `tests/Rentier.Desktop.Tests/ReportsViewModelBulkDeleteTests.cs` covering: `SelectedCount`/`HasSelection`/`DeleteSelectedLabel` reactivity, `SelectAllCommand`, `ClearSelectionCommand`, `BulkDeleteCommand` shows **cascade warning** dialog (message contains linked-filings warning text), cancel leaves selection intact, confirm dispatches `BulkDeleteReportsCommand` with correct IDs, successful delete reloads list and clears selection

**Checkpoint**: Both US1 and US2 are independently functional. `dotnet test Rentier.slnx` must pass.

---

## Phase 5: User Story 3 — Selection State and Toolbar Reactivity (Priority: P2)

**Goal**: Toolbar state machine is verified to be fully reactive: "Select All" and "Clear Selection" are visible only when `HasItems`, "Delete Selected" appears/disappears in real time as checkboxes toggle, count N updates within 200ms, and the empty-state hides all three buttons.

**Independent Test**: Toggle checkboxes without confirming any deletion. Observe toolbar buttons appear/disappear and count updates in real time. Verify empty-state hides all buttons.

- [ ] T027 [P] [US3] Verify and extend `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: confirm `HasItems` is an `ObservableAsPropertyHelper` (or equivalent) that reacts to `Rows.Count` changes and is exposed as the `canExecute` source for `SelectAllCommand` and the `IsVisible` binding of the "Select All"/"Clear Selection" buttons; add any missing `WhenAnyValue` wiring
- [ ] T028 [P] [US3] Verify and extend `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` with the same `HasItems` reactive wiring as T027
- [ ] T029 [P] [US3] Extend `tests/Rentier.Desktop.Tests/FilingsViewModelBulkDeleteTests.cs` with edge-case test scenarios: empty `Rows` → all buttons hidden, `Rows` populated → Select All and Clear Selection visible, single checkbox checked → "Delete Selected (1)" appears, second checkbox unchecked from 2 → updates to "Delete Selected (1)", Clear Selection from N → count drops to 0 and Delete Selected hidden
- [ ] T030 [P] [US3] Extend `tests/Rentier.Desktop.Tests/ReportsViewModelBulkDeleteTests.cs` with the same empty-state and reactive count-update edge-case test scenarios as T029

**Checkpoint**: All toolbar reactivity edge cases pass. `dotnet test tests/Rentier.Desktop.Tests` must pass.

---

## Phase 6: User Story 4 — Non-Blocking Async Delete Operation (Priority: P2)

**Goal**: The bulk delete operation runs entirely async; the UI shows a loading indicator during deletion, the "Delete Selected" button is disabled while a delete is in flight (preventing double-submission), and errors are surfaced to the user without data loss.

**Independent Test**: Select 10 items, trigger delete, observe button disables and loading indicator appears while awaiting, verify UI remains responsive, verify the button re-enables and list reloads after completion. Simulate an error and verify the error message is displayed.

- [ ] T031 [US4] Verify `BulkDeleteCommand` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` uses `ReactiveCommand.CreateFromTask` so that `IsExecuting` is automatically true during execution; bind "Delete Selected" button `IsEnabled` to `!BulkDeleteCommand.IsExecuting` in `src/Rentier.Desktop/Views/FilingsView.axaml` to prevent double-submission (FR-018)
- [ ] T032 [P] [US4] Apply the same `IsExecuting`-based `IsEnabled` binding for the "Delete Selected" button in `src/Rentier.Desktop/Views/ReportsView.axaml` (FR-018)
- [ ] T033 [US4] Verify `BulkDeleteCommand` error path in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: on handler failure result, set `ErrorMessage` from `Strings.BulkDelete_Error_Failed`, reload the list to reflect current state, and clear selection — confirming FR-019 is satisfied
- [ ] T034 [P] [US4] Verify the same error-path behaviour in `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` (FR-019)
- [ ] T035 [P] [US4] Extend `tests/Rentier.Desktop.Tests/FilingsViewModelBulkDeleteTests.cs` with async/loading test scenarios: `BulkDeleteCommand.IsExecuting` is true while handler is awaited, handler failure result sets `ErrorMessage`, list reloads after failure, selection cleared after failure
- [ ] T036 [P] [US4] Extend `tests/Rentier.Desktop.Tests/ReportsViewModelBulkDeleteTests.cs` with the same async/loading/error test scenarios as T035

**Checkpoint**: All four user stories are complete and independently testable. `dotnet test Rentier.slnx` must pass with ≥90% Application-layer coverage.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, string compliance check, edge-case verification, and build sign-off.

- [ ] T037 Run `dotnet build Rentier.slnx` and resolve any remaining compilation errors or warnings introduced by this feature
- [ ] T038 Run `dotnet test Rentier.slnx` and confirm all existing tests still pass (no regression) and all new tests pass
- [ ] T039 [P] Audit `src/Rentier.Desktop/Resources/Strings.resx` and all modified `.axaml` / `.cs` files to confirm zero hardcoded display strings for this feature (SC-002); all user-facing text must reference `Strings.*` keys
- [ ] T040 [P] Verify post-delete page-decrement edge case in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: when the number of selected items equals `Rows.Count` (all visible items on current page) and `_currentPage > 1`, page is decremented before reload (research.md decision #10)
- [ ] T041 Execute quickstart.md validation scenarios end-to-end: Steps 1–9 (repo interfaces → commands → app tests → row VMs → parent VMs → strings → views → DI → desktop tests) and confirm all build and test commands pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — **BLOCKS all user story phases**
- **US1 (Phase 3)** and **US2 (Phase 4)**: Both depend on Phase 2 — can run in **parallel** (completely disjoint file sets)
- **US3 (Phase 5)** and **US4 (Phase 6)**: Depend on Phase 3 and Phase 4 — can run in parallel with each other
- **Polish (Phase 7)**: Depends on all prior phases

### User Story Dependencies

- **US1 (P1)**: Unblocked after Phase 2 — no dependency on US2, US3, or US4
- **US2 (P1)**: Unblocked after Phase 2 — no dependency on US1, US3, or US4
- **US3 (P2)**: Depends on US1 and US2 ViewModel implementations (T011, T012, T021, T022)
- **US4 (P2)**: Depends on US1 and US2 command/ViewModel implementations (T008, T012, T018, T022)

### Within Each User Story

- Application command (T007/T017) can be created in parallel with row ViewModel (T010/T020)
- Application handler (T008/T018) must follow its command record
- Application tests (T009/T019) require the handler to exist
- Desktop parent ViewModel properties (T011/T021) require the row ViewModel `IsSelected` property (T010/T020)
- Desktop parent ViewModel commands (T012/T022) require the properties (T011/T021) and the application handler (T008/T018)
- DI registration (T013/T023) requires the handler (T008/T018)
- View changes (T014/T024, T015/T025) require the ViewModel commands and properties
- Desktop tests (T016/T026) require all of the above

---

## Parallel Execution Examples

### Phase 2 (Foundational) — Run Together

```
Task: "Add DeleteManyAsync to IFilingRepository"           → T002
Task: "Add DeleteManyAsync to IReportRepository"           → T003
(then)
Task: "Implement DeleteManyAsync in FilingRepository"      → T004
Task: "Implement DeleteManyAsync in ReportRepository"      → T005
Task: "Add BulkDelete_* strings to Strings.resx"          → T006
```

### Phase 3 (US1) — Parallelisable Starts

```
Task: "Create BulkDeleteFilingsCommand record"             → T007
Task: "Add IsSelected to FilingRowViewModel"               → T010
(then, in parallel after T007)
Task: "Create BulkDeleteFilingsCommandHandler"             → T008
Task: "Create BulkDeleteFilingsCommandHandlerTests"        → T009
```

### Phase 3 + Phase 4 — US1 and US2 Fully Parallel

```
Developer A: Phase 3 (T007 → T016) — Filings end-to-end
Developer B: Phase 4 (T017 → T026) — Reports end-to-end
(Zero file conflicts — entirely disjoint file sets)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T006)
3. Complete Phase 3: User Story 1 — Bulk Delete Filings (T007–T016)
4. **STOP and VALIDATE**: Filings page supports full bulk delete independently
5. Demo / ship if sufficient

### Incremental Delivery

1. Setup + Foundational → infrastructure ready
2. Add US1 → Filings bulk delete functional → validate independently
3. Add US2 → Reports bulk delete + cascade warning → validate independently
4. Add US3 → Reactive toolbar polish → validate edge cases
5. Add US4 → Async quality gates → validate error handling and double-submit prevention
6. Polish phase → final sign-off

### Parallel Team Strategy

With two developers after Phase 2:

- **Developer A**: Phase 3 (T007–T016) — Filings all the way through
- **Developer B**: Phase 4 (T017–T026) — Reports all the way through
- Both complete → continue to Phases 5 and 6 together

---

## Notes

- **[P] tasks** share no files with other [P] tasks in the same phase — safe to run concurrently
- **[Story] labels** map each task to its user story for traceability against spec.md acceptance scenarios
- **No domain changes**: `Filing` and `Report` entities are untouched — only Application, Infrastructure, and Desktop layers are modified
- **No migrations**: No schema changes — `DeleteManyAsync` operates on existing tables
- **Cascade strategy**: `BulkDeleteReportsCommandHandler` loops `DeleteByReportIdAsync` per report (application-level cascade) before calling `DeleteManyAsync` — consistent with existing `DeleteReportCommandHandler` pattern (research.md decision #3)
- **Selection scope**: "Select All" on Filings applies to the current page only (20 items max); on Reports it selects all loaded records (research.md decision #9)
- **Confirmation dialog**: Reuses `ConfirmDialogHelper.ShowAsync` — no new dialog infrastructure (research.md decision #6)
- Each user story has a clear checkpoint — stop and validate independently before proceeding
