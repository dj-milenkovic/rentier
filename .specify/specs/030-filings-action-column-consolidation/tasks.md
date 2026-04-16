# Tasks: Filings — Action Column Consolidation & Icon-Only Buttons

**Spec**: `.specify/specs/030-filings-action-column-consolidation/spec.md`
**Plan**: `.specify/specs/030-filings-action-column-consolidation/plan.md`
**Branch**: `feat/027-031-ux-improvements`
**Scope**: `Rentier.Desktop` only — no Domain, Application, or Infrastructure changes

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[US#]**: Which user story this task belongs to
- All paths are relative to repository root

---

## Phase 1: Foundation (Blocking Prerequisites)

**Purpose**: ViewModel model surface and string resources that every user story depends on. No
user story work can proceed until T001–T005 are complete.

**⚠️ CRITICAL**: All five tasks here must complete before any user story phase begins.

- [X] T001 Add 5 string resources to `src/Rentier.Desktop/Resources/Strings.resx`: `Filings_Tooltip_AdvanceStatus` = `"Mark as {0}"`, `Filings_Tooltip_AdvanceStatus_None` = `"No further transitions"`, `Filings_Tooltip_Export` = `"Export PP-OPO XML"`, `Filings_Tooltip_Delete` = `"Delete filing"`, `Filings_Col_Actions` = `"Actions"`
- [X] T002 Add `HasNextStatus` (bool, computed: `AvailableNextStatuses.Count > 0`) and `AdvanceStatusTooltip` (string, computed: formats `string.Format(Strings.Filings_Tooltip_AdvanceStatus, AvailableNextStatuses[0].ToDisplayString())` when `HasNextStatus`, else returns `Strings.Filings_Tooltip_AdvanceStatus_None`) as public properties to `src/Rentier.Desktop/ViewModels/FilingRowViewModel.cs`
- [X] T003 Update `FilingRowViewModel.From()` factory signature to `From(FilingRowDto dto, Action<(Guid, FilingStatus)> advanceStatus, Action<Guid> export, Action<Guid> delete)` and add three `ReactiveCommand<Unit, Unit>` properties: `AdvanceStatusCommand` (canExecute: `Observable.Return(HasNextStatus)`, execute body: `advanceStatus((Id, AvailableNextStatuses[0]))`), `ExportCommand` (no canExecute — always enabled, execute body: `export(Id)`), `DeleteCommand` (no canExecute — always enabled, execute body: `delete(Id)`) in `src/Rentier.Desktop/ViewModels/FilingRowViewModel.cs`
- [X] T004 Update `FilingsViewModel.LoadPageAsync` row-creation loop to pass three action delegates to `FilingRowViewModel.From()`: `args => AdvanceStatusCommand.Execute(args).Subscribe()`, `id => ExportCommand.Execute(id).Subscribe()`, `id => DeleteCommand.Execute(id).Subscribe()` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`
- [X] T005 Inspect `tests/Rentier.UnitTests/Desktop/FilingsViewModelBulkDeleteTests.cs` for any direct calls to `FilingRowViewModel.From(dto)` and, if present, update them to pass three no-op delegate stubs matching the new factory signature

**Checkpoint**: Per-row commands exist on `FilingRowViewModel`. Delegates are wired in `FilingsViewModel.LoadPageAsync`. All string resources added. Build must succeed before proceeding.

---

## Phase 2: User Story 1 — Advance Filing Status from Actions Column (Priority: P1) 🎯 MVP

**Goal**: Replace the ComboBox-based status change with a single per-row advance-status icon button
whose enabled state is driven by the domain state machine.

**Independent Test**: Open the Filings page with filings in Init, Filed, and Paid statuses. Verify
the advance-status button is enabled for Init and Filed, disabled for Paid, and clicking it
transitions the filing to the correct next status (Init→Filed, Filed→Paid).

### Tests for User Story 1

- [X] T006 [P] [US1] Update all `FilingRowViewModel.From(dto)` calls in `tests/Rentier.UnitTests/Desktop/FilingRowViewModelTests.cs` to use the new 3-delegate factory signature — extract a `MakeRowVm(FilingStatus status = FilingStatus.Init)` test helper that passes `delegate { }` no-op stubs for the three delegate parameters so existing tests continue to compile and pass
- [X] T007 [P] [US1] Verify `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs` still compiles and passes after the `FilingRowViewModel.From()` signature change (`FilingsViewModel` calls `From()` internally; no direct `From()` calls are expected in this file, but confirm)
- [X] T008 [P] [US1] Add `HasNextStatus_InitStatus_ReturnsTrue`, `HasNextStatus_FiledStatus_ReturnsTrue`, and `HasNextStatus_PaidStatus_ReturnsFalse` `[Fact]` tests to `tests/Rentier.UnitTests/Desktop/FilingRowViewModelTests.cs`
- [X] T009 [P] [US1] Add `AdvanceStatusTooltip_InitStatus_ReturnsMarkAsFiled`, `AdvanceStatusTooltip_FiledStatus_ReturnsMarkAsPaid`, and `AdvanceStatusTooltip_PaidStatus_ReturnsNoFurtherTransitions` `[Fact]` tests to `tests/Rentier.UnitTests/Desktop/FilingRowViewModelTests.cs`
- [X] T010 [P] [US1] Add `AdvanceStatusCommand_InitStatus_IsEnabled`, `AdvanceStatusCommand_FiledStatus_IsEnabled`, `AdvanceStatusCommand_PaidStatus_IsDisabled`, and `AdvanceStatusCommand_Execute_InvokesAdvanceStatusDelegate` `[Fact]` tests to `tests/Rentier.UnitTests/Desktop/FilingRowViewModelTests.cs` — for the delegation test, pass a captured-variable delegate and assert it was called with the correct `(Id, FilingStatus)` tuple

### Implementation for User Story 1

- [X] T011 [US1] Remove the Status ComboBox column (AXAML lines 114–130, contains a `DataGridTemplateColumn` with a `ComboBox` bound to `AvailableNextStatuses`) from `src/Rentier.Desktop/Views/FilingsView.axaml`
- [X] T012 [US1] Remove the `StatusComboBox_SelectionChanged` event handler method from `src/Rentier.Desktop/Views/FilingsView.axaml.cs`

**Checkpoint**: `FilingRowViewModel` exposes `AdvanceStatusCommand` with correct `canExecute` semantics. T008–T010 tests all pass. ComboBox column is removed from AXAML. Build succeeds with no errors.

---

## Phase 3: User Story 2 — Consolidated Actions Column Layout (Priority: P1)

**Goal**: Collapse the three separate action columns into a single rightmost "Actions" column
containing three horizontally-arranged icon-only `PathIcon` buttons bound to per-row commands.

**Independent Test**: Load the Filings page and visually inspect the DataGrid. Verify a single
"Actions" column appears at the far right. Verify no separate "Change Status", "Export", or
"Delete" columns exist. Verify the read-only status badge column is visually unchanged.

### Tests for User Story 2

- [X] T013 [P] [US2] Add `ExportCommand_AnyStatus_IsAlwaysEnabled` (test Init, Filed, and Paid rows) and `DeleteCommand_AnyStatus_IsAlwaysEnabled` (same three statuses) `[Theory]` tests to `tests/Rentier.UnitTests/Desktop/FilingRowViewModelTests.cs`
- [X] T014 [P] [US2] Add `ExportCommand_Execute_InvokesExportDelegate` and `DeleteCommand_Execute_InvokesDeleteDelegate` `[Fact]` tests to `tests/Rentier.UnitTests/Desktop/FilingRowViewModelTests.cs` — pass captured-variable delegates and assert they were called with the correct `Guid`

### Implementation for User Story 2

- [X] T015 [US2] Identify and record SVG path geometry strings for the three icons from an MIT-licensed icon set (Lucide https://lucide.dev or Heroicons https://heroicons.com): advance-status (chevron-right or play), export (download or file-export), delete (trash-2) — add the chosen path string values as comments at the top of the new Actions column AXAML block before committing
- [X] T016 [US2] Remove the Export button column (AXAML lines 161–171) from `src/Rentier.Desktop/Views/FilingsView.axaml`
- [X] T017 [US2] Remove the Delete button column (AXAML lines 173–183) from `src/Rentier.Desktop/Views/FilingsView.axaml`
- [X] T018 [US2] Add the consolidated Actions `DataGridTemplateColumn` as the rightmost column in `src/Rentier.Desktop/Views/FilingsView.axaml`: header bound to `{x:Static res:Strings.Filings_Col_Actions}`, `Width="130"`, `DataTemplate` with a `StackPanel Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center" VerticalAlignment="Center"` containing: (1) advance-status `Button` with `Command="{Binding AdvanceStatusCommand}"`, `ToolTip.Tip="{Binding AdvanceStatusTooltip}"`, `Padding="6"`, `Background="Transparent"`, `BorderThickness="0"`, and a `PathIcon` child using the advance-status path data; (2) export `Button` with `Command="{Binding ExportCommand}"`, `ToolTip.Tip="{x:Static res:Strings.Filings_Tooltip_Export}"`, same padding/style, and export `PathIcon`; (3) delete `Button` with `Command="{Binding DeleteCommand}"`, `ToolTip.Tip="{x:Static res:Strings.Filings_Tooltip_Delete}"`, same padding/style, and delete `PathIcon` with `Foreground="Red"`
- [X] T019 [US2] Remove `ExportButton_Click` and `DeleteButton_Click` event handler methods from `src/Rentier.Desktop/Views/FilingsView.axaml.cs`

**Checkpoint**: DataGrid shows exactly one "Actions" column at the far right with three icon buttons. No separate action columns remain. Total column count is 8 (down from 10). T013–T014 pass. Build succeeds.

---

## Phase 4: User Story 3 — Export XML from the Actions Column (Priority: P2)

**Goal**: Confirm the Export icon button in the consolidated Actions column is always enabled for
any filing status and correctly triggers the PP-OPO XML export flow.

**Independent Test**: Click the export icon button for filings in each status (Init, Filed, Paid).
Verify the file-save dialog appears each time and the correct XML file is produced.

- [X] T020 [P] [US3] Run `dotnet test tests/Rentier.UnitTests/ --filter "ExportCommand"` and confirm `ExportCommand_AnyStatus_IsAlwaysEnabled` and `ExportCommand_Execute_InvokesExportDelegate` pass (tests written in T013–T014)
- [X] T021 [US3] Manually verify quickstart.md step 6 ("Export and Delete buttons work identically to before") — click the export icon on a filing, confirm the XML file-save dialog opens, save the file, and verify the output file contains valid PP-OPO XML

**Checkpoint**: Export via the Actions column icon button is functionally identical to the previous separate Export column button.

---

## Phase 5: User Story 4 — Delete Filing from the Actions Column (Priority: P2)

**Goal**: Confirm the Delete icon button uses a destructive red style, triggers a confirmation
dialog, and removes the filing on confirmation while leaving it unchanged on cancel.

**Independent Test**: Click the delete icon button on a filing. Verify the confirmation dialog
appears. Confirm deletion and verify the row is removed. Repeat and cancel — verify the row remains.

- [X] T022 [P] [US4] Run `dotnet test tests/Rentier.UnitTests/ --filter "DeleteCommand"` and confirm `DeleteCommand_AnyStatus_IsAlwaysEnabled` and `DeleteCommand_Execute_InvokesDeleteDelegate` pass (tests written in T013–T014)
- [X] T023 [US4] Verify that the delete `PathIcon` in the Actions column in `src/Rentier.Desktop/Views/FilingsView.axaml` has `Foreground="Red"` and that neither the advance-status nor the export `PathIcon` has any foreground color override (added in T018)
- [X] T024 [US4] Manually verify the delete flow: click the delete icon button → confirm the dialog appears → confirm deletion → verify the row is removed from the DataGrid; repeat → click cancel → verify the filing remains unchanged

**Checkpoint**: Delete button is visually red, the confirmation dialog fires correctly, and the filing is deleted or preserved based on the user's choice — identical to the previous Delete column behavior.

---

## Phase 6: User Story 5 — Icon-Only Buttons with Tooltip Discoverability (Priority: P2)

**Goal**: Verify all three action buttons display only an icon (no text label) and that each shows
the correct discoverable tooltip on hover for every filing status.

**Independent Test**: Hover over each icon button in the Actions column. Verify the correct tooltip
text appears. Verify advance-status shows "No further transitions" when disabled (Paid filing).

- [X] T025 [P] [US5] Inspect the Actions column `DataTemplate` in `src/Rentier.Desktop/Views/FilingsView.axaml` and confirm all three `Button` elements contain only a `PathIcon` child element — no `Content` text, no `TextBlock` — and that each `Button` has a `ToolTip.Tip` attribute set (added in T018)
- [X] T026 [P] [US5] Manually execute quickstart.md steps 3–5: hover over each icon button on a filing in each status and confirm the following tooltip texts: advance-status on Init → "Mark as Filed"; advance-status on Filed → "Mark as Paid"; advance-status on Paid (disabled) → "No further transitions"; export (any status) → "Export PP-OPO XML"; delete (any status) → "Delete filing"

**Checkpoint**: All five User Story 5 acceptance scenarios pass. Icon-only layout confirmed across all rows. Tooltip text is correct for every status combination.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Code hygiene, full test suite validation, and final integration verification.

- [X] T027 [P] Remove any unused `using` directives from `src/Rentier.Desktop/Views/FilingsView.axaml.cs` — specifically remove `using Rentier.Domain.Entities;` and `using Rentier.Domain.Enums;` if they are no longer referenced after removing the three event handler methods
- [X] T028 [P] Run `dotnet build src/Rentier.Desktop/Rentier.Desktop.csproj` and confirm zero compiler errors and zero new warnings
- [X] T029 Run `dotnet test tests/Rentier.UnitTests/ --filter "Filing"` and confirm all `FilingRowViewModelTests`, `FilingsViewModelTests`, and `FilingsViewModelBulkDeleteTests` pass
- [X] T030 Run the full test suite `dotnet test tests/Rentier.UnitTests/` and confirm no regressions across all test files
- [X] T031 Execute all 7 quickstart.md verification steps against the running application: (1) Actions column is rightmost with 3 icon buttons; (2) no separate Change Status / Export / Delete columns; (3) correct tooltip per icon; (4) advance-status on Init → status becomes Filed; (5) advance-status button greyed out on Paid; (6) export and delete work identically to before; (7) `FilingsView.axaml.cs` retains only `PaymentRef_LostFocus`

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Foundation           ← no dependencies — start immediately
    ↓
Phase 2: US1 (Advance Status) ← depends on Phase 1
Phase 3: US2 (Column Layout)  ← depends on Phase 1; US3/US4/US5 depend on US2
    ↓
Phase 4: US3 (Export XML)     ← depends on Phase 3 (Actions column must exist)
Phase 5: US4 (Delete)         ← depends on Phase 3 (Actions column must exist)
Phase 6: US5 (Tooltips)       ← depends on Phase 3 (Actions column must exist)
    ↓
Phase 7: Polish               ← depends on all user story phases
```

### User Story Inter-Dependencies

| Story | Depends On | Can Start After |
|-------|------------|-----------------|
| US1 (P1) | Foundation | Phase 1 complete |
| US2 (P1) | Foundation | Phase 1 complete |
| US3 (P2) | US2 AXAML | Phase 3 complete |
| US4 (P2) | US2 AXAML | Phase 3 complete |
| US5 (P2) | US2 AXAML | Phase 3 complete |

### Within User Story 1

- T006–T010 are all `[P]` — write all five test groups simultaneously (different test methods)
- T011 and T012 can run in parallel (different files: `.axaml` and `.axaml.cs`)

### Within User Story 2

- T013–T014 are `[P]` — write both test groups simultaneously
- T015 (icon sourcing) can run in parallel with T016–T017 (AXAML removals)
- T016 → T017 → T018 must be sequential (all edit `FilingsView.axaml`; removals before addition)
- T019 can run in parallel with T016–T018 (different file: `FilingsView.axaml.cs`)

---

## Parallel Execution Examples

### Foundation Phase

```text
T001 (Strings.resx)                       ← parallel with T005
T002 → T003 → T004 (ViewModel chain)      ← sequential: each builds on the previous
T005 (BulkDelete test check)              ← parallel with T001–T004
```

### User Story 1 Tests

```text
T006 + T007 + T008 + T009 + T010          ← all [P], write simultaneously
T011 + T012                               ← [P] after tests written (different files)
```

### User Story 2

```text
T013 + T014                               ← [P] write simultaneously
T015 + T019                               ← [P] icon sourcing + code-behind cleanup
T016 → T017 → T018                        ← sequential AXAML edits (same file)
```

---

## Implementation Strategy

### MVP First — P1 Stories Only (US1 + US2)

1. Complete **Phase 1** (Foundation) — CRITICAL, blocks everything
2. Complete **Phase 2** (US1) — per-row advance-status wiring + ComboBox removal
3. Complete **Phase 3** (US2) — Actions column layout + export/delete button binding
4. **STOP AND VALIDATE**: Run tests (`T029`), execute quickstart (`T031`), confirm no regressions
5. **Ship if ready** — US3/US4/US5 are verification passes, not new functionality

### Incremental P2 Delivery

1. Foundation → US1 → US2 → validate → ship (full Actions column functional — MVP)
2. US3 + US4 + US5 → validate → ship (destructive style + tooltip verification complete)
3. Polish → merge to `feat/027-031-ux-improvements`

### Parallel Team Strategy

With two developers after Foundation is complete:

- **Developer A**: US1 (ViewModel tests + ComboBox removal)
- **Developer B**: US2 icon sourcing (T015) + code-behind cleanup (T019) in parallel, then US2 AXAML edits (T016–T018) once T015 is done

---

## Notes

- **`AvailableNextStatuses` remains on `FilingRowViewModel`**: Used internally by `HasNextStatus` and `AdvanceStatusCommand`; no longer bound to any AXAML ComboBox after T011
- **`PaymentRef_LostFocus` MUST be kept** in `FilingsView.axaml.cs` — TextBox `LostFocus` cannot be expressed as a ReactiveCommand binding without code-behind
- **Icon paths**: Source from Lucide Icons (https://lucide.dev, MIT) or Heroicons (https://heroicons.com, MIT) — record the chosen path geometry strings in a code comment adjacent to the AXAML block for future maintainability
- **`AdvanceStatusCommand` uses `Observable.Return(HasNextStatus)`** for `canExecute` because `FilingRowViewModel` is immutable after construction — no reactive property change notifications are needed for the `canExecute` observable
- **No new NuGet packages**: `PathIcon` and `StreamGeometry` are built-in to Avalonia; no icon font or external icon library is required (research decision D-001)
- **No Domain / Application / Infrastructure changes**: Constitution Check passes with no violations (plan.md § Constitution Check)

