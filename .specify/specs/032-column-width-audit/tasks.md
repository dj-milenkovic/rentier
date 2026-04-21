# Tasks: Column Width Audit — Filings & Reports Tables

**Feature**: 032-column-width-audit  
**Branch**: `feature/032-033-034-column-xml-manual`  
**Input**: `.specify/specs/032-column-width-audit/` (spec.md, plan.md, research.md, data-model.md, contracts/ui-layout-contract.md)  
**Target files**: `src/Rentier.Desktop/Views/FilingsView.axaml`, `src/Rentier.Desktop/Views/ReportsView.axaml`

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no incomplete task dependencies)
- **[Story]**: User story this task belongs to (US1, US2, US3)
- All paths are relative to repository root

---

## Phase 1: Setup

**Purpose**: Confirm prerequisites and locate target files before any AXAML edits begin

- [ ] T001 Verify prerequisite features 027–031 are present on branch `feature/032-033-034-column-xml-manual` and confirm `src/Rentier.Desktop/Views/FilingsView.axaml` and `src/Rentier.Desktop/Views/ReportsView.axaml` exist with the baseline column widths recorded in `.specify/specs/032-column-width-audit/research.md` (Selection 42/44, Actions 108/88, etc.)

**Checkpoint**: Both target files found with expected baseline — safe to begin edits

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: No new infrastructure is required — this feature modifies only two existing AXAML view files. There are no database migrations, new services, or shared base types to establish before user story work can begin.

> ✅ No foundational tasks. Proceed directly to user story phases after Phase 1.

---

## Phase 3: User Story 1 — Filings Table Columns Are Right-Sized (Priority: P1) 🎯 MVP

**Goal**: Every column in the Filings DataGrid matches the specified pixel / star / auto width so that content (dates, amounts, references, badges) is fully visible without truncation or excess whitespace.

**Independent Test**: Load Filings page with representative data (long paying-entity names, amount `1,234.56 RSD`, date `2025-12-31`, reference text); verify all eight columns match the target widths from `contracts/ui-layout-contract.md` with zero truncation in any fixed column.

### Implementation for User Story 1

- [ ] T002 [P] [US1] Update all Filings DataGrid column `Width` attributes in `src/Rentier.Desktop/Views/FilingsView.axaml`: Selection `42`→`40` (FR-001), Status `84`→`90` (FR-002), Income Type `96`→`110` (FR-003), Filing Deadline `110`→`120` (FR-005), Tax Payable `120`→`130` (FR-006), Payment Reference `140`→`180` (FR-007), Actions `108`→`Auto` (FR-008); leave Paying Entity `Width="*"` unchanged (FR-004)

- [ ] T003 [US1] Apply `Margin="4,0"` to all Filings DataGrid cell content elements in `src/Rentier.Desktop/Views/FilingsView.axaml` (FR-017): add `Margin="4,0"` on the CheckBox in the Selection CellTemplate; normalize the Status badge Border from `Margin="4,2"` → `Margin="4,0"`; add `DataGridTextColumn.ElementStyle` with `<Setter Property="Margin" Value="4,0"/>` on the Income Type, Paying Entity, Filing Deadline, and Tax Payable text columns; add `Margin="4,0"` on the Payment Reference TextBox in its CellTemplate; normalize the Actions StackPanel from `Margin="4,2"` → `Margin="4,0"`

**Checkpoint**: Filings table fully satisfies US1 — all eight column widths correct, all cell content elements have `Margin="4,0"`

---

## Phase 4: User Story 2 — Reports Table Columns Are Right-Sized (Priority: P1)

**Goal**: Every column in the Reports DataGrid matches the specified pixel / star / auto width so that content (report names, dates, importer names, status text, filing counts) is fully visible and the table presents the same disciplined layout as the Filings table.

**Independent Test**: Load Reports page with representative data (long report names, dates `2025-07-15`, importer display names, statuses `Init`/`Processed`/`Error`, filing counts); verify all eight columns match the target widths from `contracts/ui-layout-contract.md` with zero truncation in any fixed column.

### Implementation for User Story 2

- [ ] T004 [P] [US2] Update all Reports DataGrid column `Width` attributes in `src/Rentier.Desktop/Views/ReportsView.axaml`: Selection `44`→`40` (FR-009), Report Name `2*`→`*` (FR-010), Import Date `96`→`110` (FR-011), Email Date `96`→`110` (FR-012), Importer `120`→`160` (FR-013), Status `88`→`100` (FR-014), Filing Count `56`→`70` (FR-015), Actions `88`→`Auto` (FR-016)

- [ ] T005 [US2] Apply `Margin="4,0"` to all Reports DataGrid cell content elements in `src/Rentier.Desktop/Views/ReportsView.axaml` (FR-017): add `Margin="4,0"` on the CheckBox in the Selection CellTemplate; verify the Report Name TextBlock already carries `Margin="4,0"` (expected from prior work — add if absent); add `DataGridTextColumn.ElementStyle` with `<Setter Property="Margin" Value="4,0"/>` on the Import Date, Email Date, Importer, Status, and Filing Count text columns; normalize the Actions StackPanel from `Margin="4,2"` → `Margin="4,0"`

**Checkpoint**: Reports table fully satisfies US2 — all eight column widths correct, all cell content elements have `Margin="4,0"`

---

## Phase 5: User Story 3 — Consistent Cell Padding Across Both Tables (Priority: P2)

**Goal**: Both tables exhibit identical 4-pixel horizontal cell margins with zero per-column or per-table variance, giving a unified appearance when switching between the Filings and Reports pages.

**Independent Test**: Visually inspect cells in both tables; confirm every content element has a consistent 4px left/right gap to its cell boundary with no vertical margin; confirm no cell in either table touches the cell edge; confirm both tables look visually aligned when compared side-by-side.

### Implementation for User Story 3

- [ ] T006 [US3] Cross-audit `src/Rentier.Desktop/Views/FilingsView.axaml` and `src/Rentier.Desktop/Views/ReportsView.axaml` to confirm every cell content element in both DataGrids carries exactly `Margin="4,0"` and no column deviates with a different value (e.g. `"4,2"`, `"2,0"`, or no margin) — fix any discrepancies found so both tables satisfy SC-004 and SC-005 from `.specify/specs/032-column-width-audit/spec.md`

**Checkpoint**: Padding is identical across both tables — US3 satisfied

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Build verification, existing test updates, and end-to-end quickstart validation

- [ ] T007 [P] Build `src/Rentier.Desktop/` with `dotnet build src/Rentier.Desktop/Rentier.Desktop.csproj` and confirm zero AXAML compilation errors after all column width and margin edits in FilingsView.axaml and ReportsView.axaml

- [ ] T008 [P] Locate any existing Avalonia headless rendering tests for the Filings and Reports pages in `tests/` and update them to assert the new column widths (Filings: 40, 90, 110, *, 120, 130, 180, Auto; Reports: 40, *, 110, 110, 160, 100, 70, Auto) and uniform `Margin="4,0"` on cell content elements, per CA-006 in `spec.md`; if no such tests exist, add a note in the test project documenting the expected widths for future coverage

- [ ] T009 Execute the full verification sequence from `.specify/specs/032-column-width-audit/quickstart.md`: run the app on branch `feature/032-033-034-column-xml-manual`, navigate to Filings page (verify SC-001 and SC-003), navigate to Reports page (verify SC-002 and SC-003), inspect cell padding on both pages (verify SC-004 and SC-005), confirm sorting / selection / editing / commands still work (verify SC-006), resize window to narrow width and confirm star columns compress while fixed columns hold their widths

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 3 (US1)** and **Phase 4 (US2)**: Both depend only on Phase 1 completion — they target different files and **can execute in parallel**
- **Phase 5 (US3)**: Depends on Phase 3 and Phase 4 completion (cross-audit requires both files to be updated)
- **Phase 6 (Polish)**: T007 and T008 can start as soon as the AXAML edits are complete; T009 requires T007 to pass first

### User Story Dependencies

| Story | Depends On | Can Parallel With |
|-------|-----------|-------------------|
| US1 (T002–T003) | Phase 1 complete | US2 (different file) |
| US2 (T004–T005) | Phase 1 complete | US1 (different file) |
| US3 (T006) | US1 and US2 complete | — |

### Within Each User Story

- Width changes (T002, T004) before margin normalization (T003, T005) — same file, sequential to avoid conflicts
- US3 cross-audit (T006) after both US1 and US2 — needs both files in final state

### Parallel Opportunities

```
After T001:
  ┌── T002 (Filings widths)     ── T003 (Filings margins)  ──┐
  │                                                           ├── T006 (US3 audit)
  └── T004 (Reports widths)     ── T005 (Reports margins)  ──┘
                                                              │
                                                        T007 (build) ──┐
                                                        T008 (tests)   ├── T009 (smoke test)
```

---

## Implementation Strategy

### MVP First (US1 — Filings Table only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 3: US1 only (T002–T003)
3. **STOP and VALIDATE**: Build (T007), run app, verify Filings page column widths and padding
4. Continue to US2 and US3 once US1 is validated

### Full Feature Delivery

1. T001 — Setup verification
2. T002 + T004 in parallel — all column width changes in both files
3. T003 + T005 in parallel — all margin normalization in both files
4. T006 — Cross-table padding audit
5. T007 + T008 in parallel — build check + test updates
6. T009 — End-to-end smoke test against quickstart.md

### Scope Summary

| Phase | Tasks | Files Changed | User Story |
|-------|-------|---------------|------------|
| Setup | T001 | — | — |
| US1 | T002–T003 | `FilingsView.axaml` | US1 (P1) |
| US2 | T004–T005 | `ReportsView.axaml` | US2 (P1) |
| US3 | T006 | Both | US3 (P2) |
| Polish | T007–T009 | Build / Tests | — |

**Total tasks**: 9  
**Parallelizable tasks**: T002+T004, T003+T005, T007+T008  
**MVP scope**: T001 → T002 → T003 (Filings table only)

---

## Notes

- This feature is **Desktop layer only** — zero changes to Domain, Application, or Infrastructure layers (CA-001)
- `Width` and `Margin` are static layout properties that do not affect bindings, commands, sorting, or selection — no behavioural regressions expected (FR-018, RQ-005)
- Actions columns must use `Width="Auto"`, never a hardcoded pixel value (RQ-002, invariant 2 in ui-layout-contract.md)
- `Width="*"` is equivalent to `Width="1*"` when it is the only star column in the table — use `*` for consistency (RQ-003)
- `ElementStyle` targets the generated TextBlock inside `DataGridTextColumn`; for template columns, apply `Margin` directly on the inner content element (RQ-004)
- [P] tasks operate on different files and have no dependency on incomplete tasks — safe to launch simultaneously
- Commit after each logical group (e.g. after T002+T004, after T003+T005)
