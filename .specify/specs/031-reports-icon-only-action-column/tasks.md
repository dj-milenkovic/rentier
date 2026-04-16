---
description: "Task list for feature 031 — Reports Icon-Only Action Column"
---

# Tasks: Reports — Icon-Only Action Column

**Feature**: `031-reports-icon-only-action-column`  
**Branch**: `feat/027-031-ux-improvements`  
**Input**: `.specify/specs/031-reports-icon-only-action-column/spec.md`  
**Prerequisites (external)**: Feature 030 fully implemented — `src/Rentier.Desktop/Assets/Icons.axaml` exists and is merged into `App.axaml`, containing the `TrashIcon` StreamGeometry resource.

**Scope**: Desktop layer only (`Rentier.Desktop`). No Domain, Application, or Infrastructure changes.  
**Tests**: UI headless test added to verify icon-only rendering (per CA-006 from spec.md).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to
- T003 and T004 both modify `ReportsView.axaml`; T004 depends on T003.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add shared resources that all user-story phases depend on.

- [X] T001 Add `Reports_Tooltip_ViewFilings` (value: `"View linked filings"`) and `Reports_Tooltip_Delete` (value: `"Delete report"`) entries to `src/Rentier.Desktop/Resources/Strings.resx`; confirm `Strings.Designer.cs` auto-regenerates two matching `public static string` properties
- [X] T002 [P] Add `ViewFilingsIcon` as a named `StreamGeometry` resource to `src/Rentier.Desktop/Assets/Icons.axaml` — use a list/arrow path (24×24 viewport, Lucide-style `ListTree` or equivalent MIT-licensed path): the icon must visually convey "navigate to a related list" at 16×16 logical pixels

**Checkpoint**: Both string resources compile and `Icons.axaml` exports `ViewFilingsIcon` alongside the existing `TrashIcon` from feature 030.

---

## Phase 2: User Story 1 + 2 — Icon Buttons with Tooltips (Priority: P1) 🎯 MVP

**Goal**: Replace the two text-labelled action buttons in the Reports DataGrid with compact icon-only buttons; make each action discoverable via tooltip on hover.

**Independent Test**: Open the Reports page with at least one report loaded → action column shows two icon buttons with no visible text labels; hover each button → correct tooltip text appears.

- [X] T003 [US1] [US2] Replace both text-content `<Button>` elements inside the `Width="Auto"` `DataGridTemplateColumn` CellTemplate in `src/Rentier.Desktop/Views/ReportsView.axaml`:
  - View Filings button: remove `Content="{x:Static res:Strings.Reports_Button_ViewFilings}"`, set button content to `<PathIcon Data="{StaticResource ViewFilingsIcon}" Width="16" Height="16"/>`, add `ToolTip.Tip="{x:Static res:Strings.Reports_Tooltip_ViewFilings}"`, add `Padding="6" Background="Transparent" BorderThickness="0"`; retain `Command` and `CommandParameter` bindings unchanged
  - Delete button: remove `Content="{x:Static res:Strings.Reports_Button_Delete}"`, set button content to `<PathIcon Data="{StaticResource TrashIcon}" Width="16" Height="16"/>`, add `ToolTip.Tip="{x:Static res:Strings.Reports_Tooltip_Delete}"`, add `Padding="6" Background="Transparent" BorderThickness="0"`; retain `Command` and `CommandParameter` bindings unchanged

**Checkpoint**: Reports page renders with two icon-only buttons per row (no text labels); tooltips appear on hover; View Filings and Delete commands fire correctly with the report's `Id` as the parameter.

---

## Phase 3: User Story 3 — Destructive Delete Styling (Priority: P2)

**Goal**: Visually distinguish the irreversible Delete action from the safe View Filings action via a red foreground on the delete button.

**Independent Test**: Inspect the delete icon button on any report row → icon is rendered in red; View Filings icon uses the default (non-red) foreground.

- [X] T004 [US3] Add `Foreground="Red"` to the Delete `<Button>` element (or its `<PathIcon>` child) in the action column of `src/Rentier.Desktop/Views/ReportsView.axaml`, matching the destructive-action pattern used by the bulk-delete button on the same page (line 41 of current markup)

**Checkpoint**: Delete icon button renders red; View Filings icon button uses default theme foreground. Pattern is visually consistent with the bulk-delete button `Foreground="Red"` style already in use on the Reports page.

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Test coverage, build validation, and cross-page consistency verification.

- [X] T005 [P] Add headless UI test `ReportsView_ActionColumn_RendersIconOnlyButtons` to `tests/Rentier.UnitTests/Desktop/Views/ReportsViewHeadlessTests.cs`: load a report row, activate the ViewModel, call `window.UpdateLayout()` + `Dispatcher.UIThread.RunJobs()`, then assert (a) no `Button` in the action column has a non-null/non-empty `Content` of type `string`, and (b) the action column contains exactly two `PathIcon` descendants — satisfying CA-006 from the spec
- [X] T006 [P] Verify cross-page icon consistency (US4): navigate to the Filings page (feature 030) and then the Reports page; confirm icon button size, `ToolTip.Tip` behaviour, and `Foreground="Red"` destructive styling are visually uniform across both pages — no code change required if consistent; document the verification outcome as a comment in the PR description
- [X] T007 Build the solution (`dotnet build Rentier.slnx`) and run all unit tests (`dotnet test Rentier.slnx`) to confirm zero regressions; all existing `ReportsViewModelTests`, `ReportRowViewModelTests`, and `ReportsViewHeadlessTests` must pass without modification

---

## Dependencies & Execution Order

### External Dependency (BLOCKING)

> ⚠️ **Feature 030 must be fully implemented before any task in this feature can begin.**  
> Feature 031 consumes `Icons.axaml` (merged in `App.axaml`) and the `TrashIcon` StreamGeometry resource that feature 030 creates. Without these, T002 and T003 cannot reference the `TrashIcon` key.

### Phase Dependencies

- **Phase 1 (Setup)**: Requires feature 030 to be complete — can start immediately once that gate is met
- **Phase 2 (US1+US2)**: Depends on T001 (string resources) and T002 (ViewFilingsIcon) — BOTH Phase 1 tasks must complete first
- **Phase 3 (US3)**: Depends on T003 (Phase 2) — delete button must exist before its style can be set; same file as T003
- **Phase 4 (Polish)**: T005 and T006 can start after T003+T004; T007 runs after T005

### User Story Dependencies

- **User Story 1 (P1)**: Depends on Phase 1 setup only; no dependency on other user stories
- **User Story 2 (P2, tooltips)**: Implemented in the same task as US1 (T003) — inseparable from icon button replacement
- **User Story 3 (P2, red foreground)**: Can start after T003 (US1+US2) is complete; modifies the same file
- **User Story 4 (P3, consistency)**: Automatically satisfied when US1–3 are complete; verified in T006

### Within Each Phase

- T001 and T002 are independent files → can be worked in parallel
- T003 depends on T001 (string resources) and T002 (icon key)
- T004 depends on T003 (button markup must exist first)
- T005 and T006 are independent of each other → can be worked in parallel after T004
- T007 must follow T005 (so new test is included in the run)

---

## Parallel Execution Examples

### Phase 1 — Run Together

```
Task T001: Add tooltip strings to src/Rentier.Desktop/Resources/Strings.resx
Task T002: Add ViewFilingsIcon to src/Rentier.Desktop/Assets/Icons.axaml
```

### Phase 4 — Run Together (after T003 + T004)

```
Task T005: Add ReportsView_ActionColumn_RendersIconOnlyButtons headless test
Task T006: Cross-page visual consistency verification (Filings vs Reports)
```

---

## Implementation Strategy

### MVP (User Stories 1 + 2 only)

1. Confirm feature 030 is fully implemented (Icons.axaml + TrashIcon present)
2. Complete T001 + T002 in parallel (Phase 1)
3. Complete T003 (Phase 2)
4. **STOP and VALIDATE**: Open Reports page → two icon-only buttons with tooltips visible; View Filings and Delete commands still work
5. This delivers the core feature value immediately

### Incremental Delivery

1. Phase 1 (T001, T002) → shared resources ready
2. Phase 2 (T003) → icon buttons + tooltips ✅ independently testable
3. Phase 3 (T004) → destructive style ✅ independently testable
4. Phase 4 (T005, T006, T007) → test coverage + consistency verified
5. Each phase adds value without breaking the previous phase

---

## Requirement Traceability

| Requirement | Task | Notes |
|---|---|---|
| FR-001: View Filings icon button | T003 | PathIcon replaces text content |
| FR-002: Delete icon button | T003 | PathIcon replaces text content |
| FR-003: "View linked filings" tooltip | T001, T003 | String resource + ToolTip.Tip binding |
| FR-004: "Delete report" tooltip | T001, T003 | String resource + ToolTip.Tip binding |
| FR-005: Delete button red foreground | T004 | Foreground="Red" on delete Button |
| FR-006: View Filings default foreground | T003 | No Foreground override = theme default |
| FR-007: Action column auto-sizes to icon width | T003 | Width="Auto" already in markup; narrow icons naturally shrink it |
| FR-008: View Filings command binding preserved | T003 | Command + CommandParameter bindings retained |
| FR-009: Delete command binding preserved | T003 | Command + CommandParameter bindings retained |
| FR-010: Tooltip strings are localised resources | T001 | Defined in Strings.resx |

---

## Notes

- **No ViewModel changes**: `ReportRowViewModel` and `ReportsViewModel` require zero modifications. All command bindings (`ViewFilingsCommand`, `DeleteCommand`) already use `RelativeSource` to the parent DataGrid `DataContext`; only the visual button content changes.
- **No new NuGet packages**: `PathIcon` is built into Avalonia. All icon geometry comes from `Icons.axaml` (created in feature 030).
- **`Strings.Designer.cs`**: Auto-generated by MSBuild on next build after Strings.resx is saved. No manual editing required.
- **Icon path data**: Choose an MIT-licensed path from Lucide Icons (`https://lucide.dev`) or Heroicons. The path must read correctly at 16×16 logical pixels. Use a `Viewbox` or set explicit `Width`/`Height` on the `PathIcon` if the source geometry uses a 24×24 viewport.
- [P] tasks = different files, no shared state
- [Story] label maps each task to a specific user story for traceability
- Commit after T003+T004 as a logical unit; commit T005+T006+T007 as the polish unit
