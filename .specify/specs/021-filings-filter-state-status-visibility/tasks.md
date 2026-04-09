# Tasks: Filings Filter State & Status Visibility

**Feature**: 021-filings-filter-state-status-visibility
**Branch**: `feature/021-024-qa-fixes`
**Spec**: `.specify/specs/021-filings-filter-state-status-visibility/spec.md`
**Scope**: Presentation layer only — no Domain, Application, or Infrastructure changes (CA-001)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no shared-write dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are included in all implementation tasks

---

## Phase 1: Setup

**Purpose**: Confirm scope boundaries and verify no infrastructure work is required.

- [ ] T001 Confirm branch is `feature/021-024-qa-fixes`; verify no Domain/Application/Infrastructure projects are touched (`git status`); confirm `FilingStatus` enum and `FilingStatusExtensions.ToDisplayString()` cover Init/Filed/Paid in `src/Rentier.Desktop/Extensions/FilingStatusExtensions.cs`

---

## Phase 2: Foundational (Blocking Prerequisite)

**Purpose**: New colour converter shared by the US2 badge — must exist before badge AXAML can reference it.

**Warning**: US2 badge implementation (Phase 4) cannot begin until T002 compiles.

- [ ] T002 Create `FilingStatusBrushConverter` using the static-instance `FuncValueConverter<FilingStatus, IBrush>` pattern (matching existing converters): Init maps to amber `#F59E0B`, Filed maps to blue `#3B82F6`, Paid maps to green `#22C55E`, unknown maps to `Brushes.Transparent` in `src/Rentier.Desktop/Converters/FilingStatusBrushConverter.cs`

**Checkpoint**: `FilingStatusBrushConverter.Instance` is referenceable as `local:FilingStatusBrushConverter.Instance` in AXAML — US2 badge can now proceed.

---

## Phase 3: User Story 1 — Active Filter Indication (Priority: P1) MVP

**Goal**: "Unpaid" and "All" ToggleButtons on the Filings page visually distinguish the active filter so users can see at a glance which data subset is displayed, satisfying FR-001 through FR-003.

**Independent Test**: Open Filings page → default "Unpaid" button is visually highlighted; "All" is not → click "All" → "All" becomes highlighted and "Unpaid" reverts to default → click already-active "All" again → no change. Entirely visual; no backend changes needed.

### Implementation for User Story 1

- [ ] T003 [US1] In `src/Rentier.Desktop/Views/FilingsView.axaml`, add an inline `<StackPanel.Styles>` block inside the filter `StackPanel` (DockPanel.Dock="Top") containing a `Style` targeting `ToggleButton:checked` pseudo-class — set `Background` to the Fluent accent resource (`SystemAccentColor` or `ThemeAccentBrush`) and `Foreground` to a contrasting colour (white/OnAccent), so the checked button is clearly distinguished from the unchecked one without modifying the existing IsChecked bindings
- [ ] T004 [P] [US1] Add test `ShowAll_WhenSetToTrue_SendsAllFilterToQuery` and `ShowAll_WhenSetToFalse_SendsUnpaidFilterToQuery` in `tests/Rentier.Desktop.Tests/FilingsViewModelTests.cs` verifying that toggling `ShowAll` fires `LoadPageCommand` with the correct `FilingFilterMode` (mirrors the existing `OnActivation_TriggersLoadPageWithDefaultUnpaidFilter` pattern)

**Checkpoint**: User Story 1 is fully functional and independently testable — filter active state is visible.

---

## Phase 4: User Story 2 — Read-Only Status Badge (Priority: P1)

**Goal**: Each filing row displays a colour-coded pill badge (amber=Init, blue=Filed, green=Paid) that is read-only and purely informational, satisfying FR-004 through FR-007 and FR-010.

**Independent Test**: Load Filings page with rows covering all three statuses → each row shows a rounded pill badge with the correct localised label and colour → verify badge has no click handler and is not interactive. Depends on T002 (Phase 2) but independent of US1 (Phase 3).

### Implementation for User Story 2

- [ ] T005 [US2] In `src/Rentier.Desktop/Views/FilingsView.axaml`, replace the Status `DataGridTemplateColumn` `CellTemplate` content: wrap the existing `ComboBox` in a `StackPanel` (Orientation=Vertical) and add a read-only pill badge above it — the badge is a `Border` (CornerRadius=10, Padding=6,2, Background bound via `FilingStatusBrushConverter.Instance` on `Status`) containing a `TextBlock` (Foreground=White, FontSize=11, FontWeight=SemiBold) bound to `Status` via `FilingStatusDisplayConverter.Instance`; the `Border` must have `IsHitTestVisible=False` to enforce FR-006 (non-interactive)
- [ ] T006 [P] [US2] Create `tests/Rentier.Desktop.Tests/FilingRowViewModelTests.cs` with three `[Theory]` test cases (using `[InlineData]`) verifying that `FilingRowViewModel.From(dto)` correctly exposes `Status` as `FilingStatus.Init`, `FilingStatus.Filed`, and `FilingStatus.Paid` for the three status values — confirms the ViewModel property that feeds the badge converter is correct (CA-006 compliance)

**Checkpoint**: User Story 2 is fully functional — all rows show the correct colour-coded read-only pill badge independently of US1.

---

## Phase 5: User Story 3 — Visible List Refresh on Filter Change (Priority: P2)

**Goal**: When the user switches filters, the loading indicator is clearly visible long enough to confirm the list is refreshing, satisfying FR-008 and SC-004.

**Independent Test**: Toggle between "Unpaid" and "All" filters repeatedly → a loading indicator is briefly visible on each switch (even when the round-trip is fast) → the list rows visibly change when switching from a subset to all filings. US3 builds on US1 (filter toggle) but is independently verifiable.

### Implementation for User Story 3

- [ ] T007 [US3] In `src/Rentier.Desktop/Views/FilingsView.axaml`, increase the `ProgressBar` Height from `4` to `6` and add `MinHeight="6"` so the loading bar is more visually prominent during filter transitions; confirm `IsVisible="{Binding IsLoading}"` binding is unchanged
- [ ] T008 [P] [US3] In `tests/Rentier.Desktop.Tests/FilingsViewModelTests.cs`, add test `LoadPage_SetsIsLoadingTrueDuringExecution_ThenFalseAfterCompletion` verifying that `IsLoading` is `true` while `LoadPageCommand` is executing and `false` after it completes (use the `ImmediateScheduler.Instance` pattern from existing tests)

**Checkpoint**: User Story 3 is complete — loading feedback is clearly visible during filter transitions.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Accessibility validation, code consistency, and final verification across all stories.

- [ ] T009 [P] Verify colour contrast ratios for badge colours meet WCAG AA (4.5:1 minimum): amber `#F59E0B` on white text, blue `#3B82F6` on white text, green `#22C55E` on white text — adjust hex values if contrast is insufficient (use a contrast checker tool); update `src/Rentier.Desktop/Converters/FilingStatusBrushConverter.cs` with the final validated values
- [ ] T010 [P] Check that the badge and ToggleButton styles render correctly under both Avalonia FluentTheme light and dark variants by launching the app in each theme — confirm no illegible colour combinations (report findings; no code change unless a contrast failure is found)
- [ ] T011 Run `dotnet test tests/Rentier.Desktop.Tests --no-build` from repo root to confirm all existing and new Desktop tests pass; fix any regressions before merge
- [ ] T012 Run `dotnet build src/Rentier.Desktop/Rentier.Desktop.csproj` and resolve any AXAML binding warnings introduced by the new badge template or style changes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — T002 must compile before US2 badge AXAML work
- **Phase 3 (US1)**: Depends on Phase 1 only — independent of Phase 2 and US2 (no shared files)
- **Phase 4 (US2)**: Depends on Phase 2 (T002 must exist) — independent of Phase 3 (US1)
- **Phase 5 (US3)**: Depends on Phase 3 (filter toggle must be styled) — independent of US2
- **Phase 6 (Polish)**: Depends on all prior phases completing

### User Story Dependencies

- **US1 (P1)**: Depends only on Phase 1 — modifies only `FilingsView.axaml` (filter StackPanel styles) and `FilingsViewModelTests.cs`
- **US2 (P1)**: Depends on Phase 2 (T002 converter) — modifies only `FilingsView.axaml` (status column) and adds `FilingRowViewModelTests.cs`
- **US3 (P2)**: Depends on US1 being visually complete (filter works) — modifies only `FilingsView.axaml` (ProgressBar) and `FilingsViewModelTests.cs`

### Within Each User Story

- T002 (converter) before T005 (badge AXAML uses it)
- T003 (ToggleButton style) is independent of T004 (tests can be written first or after)
- T005 (badge AXAML) after T002 (converter required)
- T006 (FilingRowViewModel tests) is independent of T005 (tests the ViewModel, not the view)

### Parallel Opportunities

US1 and US2 can proceed in parallel once Phase 2 (T002) is done:
- Developer A: T003 + T004 (US1 — only touches filter StackPanel styles and ViewModel tests)
- Developer B: T005 + T006 (US2 — only touches status column template and new test file)
- After both complete: T007 + T008 (US3) then Phase 6 polish

---

## Parallel Example: US1 + US2 (after T002)

```
# After T002 compiles — launch in parallel:
Task A: T003  Style ToggleButton:checked in FilingsView.axaml (filter StackPanel only)
Task B: T005  Add badge Border+TextBlock in FilingsView.axaml (status column only)

# In parallel with implementation:
Task C: T004  Write ShowAll filter tests in FilingsViewModelTests.cs
Task D: T006  Write FilingRowViewModelTests.cs for badge status properties
```

---

## Implementation Strategy

### MVP First (US1 + US2 — both P1)

1. Complete **Phase 1**: Confirm scope, check branch
2. Complete **Phase 2**: Create `FilingStatusBrushConverter` (T002) — ~10 min
3. Complete **Phase 3**: Style ToggleButton active state (T003 + T004) — ~30 min
4. Complete **Phase 4**: Add status badge in DataGrid (T005 + T006) — ~45 min
5. **Stop and validate**: Both P1 stories are independently testable — demo to QA
6. Add **Phase 5** (US3 loading polish) if time permits

### Incremental Delivery

1. T001 → T002 → branch ready
2. T003 + T004 → US1 resolved: QA issue (1) fixed
3. T005 + T006 → US2 resolved: QA issue (2) fixed
4. T007 + T008 → US3 polish: transition feedback improved
5. T009–T012 → accessibility + build validation → merge-ready

---

## Notes

- **Presentation-only feature**: Zero changes to `Rentier.Domain`, `Rentier.Application`, or `Rentier.Infrastructure` — any modification to those projects is out of scope per CA-001
- **Existing ToggleButton bindings are correct**: `IsChecked="{Binding ShowAll}"` and its inverse already drive the filter logic — only the visual styling needs to change (T003)
- **Existing status converter reused for badge text**: `FilingStatusDisplayConverter.Instance` (already in `FilingsView.axaml`) is reused for the badge `TextBlock` — no new localisation strings needed (FR-010 compliance)
- **[P] tasks** = touch different files or non-overlapping sections; safe to execute concurrently
- **[Story] label** maps each task to a specific user story for traceability and independent delivery
- Commit after each logical group (e.g., after T003+T004, after T005+T006) to keep the branch history clean
