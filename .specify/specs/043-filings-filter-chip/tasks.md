# Tasks: Filings Per-Report Filter Chip

**Feature**: `043-filings-filter-chip`
**Branch**: `feature/043-filings-filter-chip`
**Input**: `.specify/specs/043-filings-filter-chip/` (spec.md, plan.md, research.md, data-model.md, quickstart.md)
**Scope**: Desktop layer only — 3 source files modified, 2 test files updated

**Tests**: Included per spec CA-006. ViewModel unit tests and Avalonia headless rendering tests required.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete preceding task)
- **[Story]**: Which user story this task belongs to ([US1] = chip visibility, [US2] = chip dismissal)
- All file paths are absolute from repo root

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: Add the ViewModel state that both user stories depend on. These changes live in the same
file (`FilingsViewModel.cs`) and must complete before any View or test work begins.

**⚠️ CRITICAL**: Phase 2 and Phase 3 cannot start until T001 and T002 are complete.

- [x] T001 Add `_hasReportFilter` field (`ObservableAsPropertyHelper<bool>`) and public `HasReportFilter` property to `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`; wire it in the constructor/`WhenActivated` block: `this.WhenAnyValue(x => x.ReportIdFilter).Select(id => id.HasValue).ToProperty(this, x => x.HasReportFilter)`

- [x] T002 Add `ClearReportFilterCommand` (`ReactiveCommand<Unit, Unit>`) to `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`; implement body as `() => { ReportIdFilter = null; }` with `canExecute: this.WhenAnyValue(x => x.HasReportFilter)` and the scheduler overload; place declaration after `HasReportFilter` property

- [x] T003 [P] Add two new string entries to `src/Rentier.Desktop/Resources/Strings.resx`: `Filings_FilterChip_Report` = `"Filtered by report"` and `Filings_FilterChip_Dismiss` = `"Remove report filter"` — follow the existing key naming convention `Filings_Filter_*`

**Checkpoint**: `HasReportFilter` and `ClearReportFilterCommand` compile; `Strings.resx` has both keys. Phase 2 and Phase 3 can now begin.

---

## Phase 2: User Story 1 — Visible Report Filter Indication (Priority: P1) 🎯 MVP

**Goal**: A pill-shaped chip reading "Filtered by report ✕" appears in the Filings filter bar whenever `ReportIdFilter` is non-null, and is entirely absent when the filter is null.

**Independent Test**: Navigate from the Reports page via "View Filings" → observe the chip in the filter bar. Then navigate to the Filings page directly from the sidebar → confirm no chip is shown.

### Implementation for User Story 1

- [x] T004 [US1] Insert the report filter chip element into the filter bar `DockPanel` in `src/Rentier.Desktop/Views/FilingsView.axaml` — place it immediately after the radio-buttons `StackPanel`, before any spacer/sort indicator; use this structure:
  ```xml
  <Border IsVisible="{Binding HasReportFilter}"
          Background="{DynamicResource RentierChipBackgroundBrush}"
          CornerRadius="10" Padding="8,2" Margin="8,0,0,0"
          VerticalAlignment="Center">
    <StackPanel Orientation="Horizontal" Spacing="4">
      <TextBlock Text="{Binding [Filings_FilterChip_Report], Source={StaticResource Localizer}}"
                 VerticalAlignment="Center" FontSize="12" />
      <Button Command="{Binding ClearReportFilterCommand}"
              AutomationProperties.Name="{Binding [Filings_FilterChip_Dismiss], Source={StaticResource Localizer}}"
              Padding="2" Background="Transparent" BorderThickness="0"
              VerticalAlignment="Center" Cursor="Hand">
        <TextBlock Text="✕" FontSize="12" />
      </Button>
    </StackPanel>
  </Border>
  ```
  If `RentierChipBackgroundBrush` does not exist, substitute the nearest existing secondary-surface brush from the design token file.

### Tests for User Story 1

- [x] T005 [P] [US1] Add two `HasReportFilter` visibility tests to `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`:
  - `HasReportFilter_WhenReportIdFilterIsNull_ReturnsFalse` — assert `sut.HasReportFilter == false` after constructing the VM with no filter set
  - `HasReportFilter_WhenReportIdFilterIsSet_ReturnsTrue` — set `sut.ReportIdFilter = Guid.NewGuid()` and assert `sut.HasReportFilter == true`

- [x] T006 [US1] Add two chip rendering headless tests to `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs` (requires Avalonia headless test host):
  - `FilterChip_WhenReportFilterActive_IsVisible` — set `vm.ReportIdFilter = Guid.NewGuid()`, render `FilingsView`, assert the chip `Border` (`IsVisible == true`) is present in the visual tree
  - `FilterChip_WhenNoReportFilter_IsNotVisible` — leave `ReportIdFilter` null, assert the chip `Border` is not visible (or not present)

**Checkpoint**: Navigate from Reports → chip visible. Navigate from sidebar → chip absent. Stories are independently verifiable.

---

## Phase 3: User Story 2 — Dismiss Report Filter (Priority: P1)

**Goal**: Clicking ✕ on the chip sets `ReportIdFilter = null`, removes the chip, and triggers a page reload showing all filings from page 1.

**Independent Test**: Navigate from Reports via "View Filings" → click ✕ → full unfiltered filing list loads, chip disappears, pagination resets to page 1.

### Tests for User Story 2

- [x] T007 [US2] Add three `ClearReportFilterCommand` behavior tests to `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs` (same class, new test methods — sequential with T005):
  - `ClearReportFilterCommand_WhenNoReportFilter_CannotExecute` — assert `sut.ClearReportFilterCommand.CanExecute(Unit.Default) == false` when `ReportIdFilter` is null
  - `ClearReportFilterCommand_WhenExecuted_SetsReportIdFilterToNull` — set filter to a GUID, execute command, assert `sut.ReportIdFilter == null` and `sut.HasReportFilter == false`
  - `ClearReportFilterCommand_WhenExecuted_TriggersLoadPageCommand` — set filter, subscribe to `LoadPageCommand.IsExecuting`, execute `ClearReportFilterCommand`, assert `LoadPageCommand` fired (use `TestScheduler` or verify via mock mediator invocation count increment)

- [x] T008 [P] [US2] Add one dismiss interaction headless test to `tests/Rentier.UnitTests/Desktop/Views/FilingsViewHeadlessTests.cs` (different file from T007, can run in parallel):
  - `FilterChip_DismissButton_WhenClicked_ChipDisappears` — set `ReportIdFilter`, render view, locate dismiss `Button`, simulate click, assert chip `Border` collapses (`IsVisible == false`)

**Checkpoint**: US1 and US2 are both fully functional. The chip appears, persists through pagination/sort/toggle changes, and dismisses correctly in a single click.

---

## Phase 4: Polish & Verification

**Purpose**: Build confirmation, full test run, and quickstart walkthrough to verify all acceptance scenarios.

- [x] T009 Build the Desktop project to confirm no compilation errors: `dotnet build src/Rentier.Desktop/Rentier.Desktop.csproj`

- [x] T010 [P] Run ViewModel tests and confirm all pass: `dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~FilingsViewModelTests"`

- [x] T011 [P] Run headless view tests and confirm all pass: `dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~FilingsViewHeadlessTests"`

- [x] T012 Manually walk through the `quickstart.md` validation steps: navigate Reports → "View Filings" (chip visible), toggle All/Unpaid (chip persists), change sort (chip persists), paginate (chip persists), click ✕ (chip gone, all filings reload at page 1), re-navigate from sidebar (no chip)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — start immediately on T001 and T002 sequentially; T003 can run in parallel with T001/T002 (different file)
- **US1 Phase (Phase 2)**: Requires T001 + T002 + T003 complete
  - T004 (View) and T005 (ViewModel tests) can run in parallel — different files
  - T006 (headless tests) requires T004 complete (tests the rendered view)
- **US2 Phase (Phase 3)**: Requires Phase 2 complete
  - T007 (ViewModel dismiss tests) and T008 (headless dismiss test) can run in parallel — different files
- **Polish (Phase 4)**: Requires Phase 3 complete; T010 and T011 can run in parallel

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational (T001–T003)
- **US2 (P1)**: Depends on US1 being complete (dismiss button is already in the chip from T004; tests build on US1 test scaffolding)

### Within Each Phase

- ViewModel changes before View changes (View binds to ViewModel properties)
- Localization before View (View references resource keys)
- Implementation before tests (tests verify the implemented code)

---

## Parallel Execution Examples

### Phase 1 Parallel Opportunities

```text
Sequential: T001 → T002
Parallel:   T003 (can run alongside T001/T002 — Strings.resx is independent)
```

### Phase 2 Parallel Opportunities

```text
After T001+T002+T003 complete:
  Parallel: T004 (FilingsView.axaml) ‖ T005 (FilingsViewModelTests.cs visibility tests)
  Sequential after T004: T006 (headless tests need the rendered chip)
```

### Phase 3 Parallel Opportunities

```text
After Phase 2 complete:
  Parallel: T007 (FilingsViewModelTests.cs dismiss tests) ‖ T008 (FilingsViewHeadlessTests.cs dismiss)
```

### Phase 4 Parallel Opportunities

```text
After T009 (build): T010 ‖ T011 (different test filter targets)
```

---

## Implementation Strategy

### MVP (Both stories are P1 — deliver together)

1. Complete Phase 1: Foundational ViewModel (T001, T002, T003)
2. Complete Phase 2: US1 chip visibility (T004, T005, T006)
3. **VALIDATE**: Chip appears when navigating from Reports, hidden on direct navigation
4. Complete Phase 3: US2 dismiss (T007, T008)
5. **VALIDATE**: ✕ clears filter, reloads all filings, resets to page 1
6. Complete Phase 4: Polish and full verification (T009–T012)

### Summary

| Phase | Tasks | Files Touched |
|---|---|---|
| Foundational | T001–T003 | `FilingsViewModel.cs`, `Strings.resx` |
| US1 | T004–T006 | `FilingsView.axaml`, `FilingsViewModelTests.cs`, `FilingsViewHeadlessTests.cs` |
| US2 | T007–T008 | `FilingsViewModelTests.cs`, `FilingsViewHeadlessTests.cs` |
| Polish | T009–T012 | Build + test runs |

---

## Notes

- **[P]** tasks touch different files and have no blocking dependency on concurrent tasks
- **[US1]/[US2]** labels map tasks to user stories from `spec.md` for traceability
- The existing `ReportIdFilter` reactive subscription (lines 393-397 of `FilingsViewModel.cs`) already handles the reload — no new pipeline wiring needed beyond setting the property to `null`
- `RentierChipBackgroundBrush`: verify existence in the theme resource dictionary before T004; if absent, use the nearest secondary-surface token and leave a `// TODO: replace with RentierChipBackgroundBrush once defined` comment
- All 5 ViewModel test names from `plan.md` must be present after T005+T007 complete
- All 3 headless test names from `plan.md` must be present after T006+T008 complete
