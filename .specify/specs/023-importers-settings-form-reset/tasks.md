# Tasks: Importers Settings Form Reset on Save & Navigation

**Feature**: `023-importers-settings-form-reset`  
**Branch**: `feature/021-024-qa-fixes`  
**Input**: `.specify/specs/023-importers-settings-form-reset/plan.md` + `spec.md`  
**Scope**: Desktop layer only — `Rentier.Desktop` + `Rentier.Desktop.Tests`

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files or independent changes)
- **[Story]**: User story label (US1–US5)
- All paths relative to repository root

---

## Phase 1: Setup

**Purpose**: Confirm baseline before making changes.

- [X] T001 Confirm build passes and all existing tests in `tests/Rentier.Desktop.Tests/` are green before any edits (`dotnet test tests/Rentier.Desktop.Tests/`)

---

## Phase 2: Foundational — Extract Shared Helper Methods

**Purpose**: Extract `PopulateFormFromDto` and `ClearForm` as private methods on `ImporterSettingsViewModel`. These helpers are prerequisites for every user story fix — all story phases depend on them existing first.

⚠️ **CRITICAL**: Both helpers must cover all 8 editable fields: `DisplayName`, `ReportType`, `SelectedProfile`, `SelectedMailbox`, `FromFilter`, `SubjectFilter`, `AttachmentRegex`, `PaymentNotes`.

- [X] T002 Extract `private void ClearForm()` in `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs` — sets all 8 fields to defaults: `DisplayName = string.Empty`, `ReportType = ReportType.IbkrCsv`, `SelectedProfile = null`, `SelectedMailbox = null`, `FromFilter = string.Empty`, `SubjectFilter = string.Empty`, `AttachmentRegex = string.Empty`, `PaymentNotes = string.Empty`; also sets `IsEditMode = false`, `ErrorMessage = null`, `SuccessMessage = null`

- [X] T003 Extract `private void PopulateFormFromDto(ImporterDto dto)` in `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs` — sets all 8 fields from `dto`: `DisplayName = dto.DisplayName`, `ReportType = dto.ReportType`, `SelectedProfile = AvailableProfiles.FirstOrDefault(p => p.Id == dto.TaxpayerProfileId)`, `SelectedMailbox = AvailableMailboxes.FirstOrDefault(m => m.Id == dto.MailboxId)`, `FromFilter = dto.FromFilter`, `SubjectFilter = dto.SubjectFilter`, `AttachmentRegex = dto.AttachmentRegex`, `PaymentNotes = dto.PaymentNotes`; also sets `IsEditMode = true`

**Checkpoint**: Both private methods compile and have the correct signature. No callers yet — existing behaviour is unchanged at this point.

---

## Phase 3: User Story 1 — Form Reflects Saved State After Save (Priority: P1) 🎯 MVP

**Goal**: After a successful save, all form fields reflect the freshly persisted DTO values, not stale user-typed values.

**Root cause**: `OnSaveAsync` writes to `_selectedImporter` (backing field) directly, bypassing the property setter and its population logic. Fix by calling `PopulateFormFromDto` explicitly after re-assigning the backing field.

**Independent Test**: Edit all 8 fields on any importer → Save → every form field matches the saved DTO's values.

- [X] T004 [US1] In `OnSaveAsync` update-path in `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs`: after `_selectedImporter = ImporterItems.FirstOrDefault(i => i.Id == savedId)`, add: if the re-found item is non-null call `PopulateFormFromDto(_selectedImporter.Dto)`; if null (item vanished) call `ClearForm()` and set `_selectedImporter = null`

- [X] T005 [US1] In `OnSaveAsync` add-path in `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs`: after `_selectedImporter = ImporterItems.FirstOrDefault(i => i.Id == newId)`, add: if the re-found item is non-null call `PopulateFormFromDto(_selectedImporter.Dto)`; if null call `ClearForm()` and set `_selectedImporter = null`

**Checkpoint**: US1 acceptance scenarios pass manually — edit fields → save → form shows persisted values with no stale state.

---

## Phase 4: User Story 2 — Form Populates Correctly on Selection Change (Priority: P1)

**Goal**: Switching importers in the list immediately replaces all form fields with the newly selected importer's data.

**Root cause**: Inline property assignments in the `SelectedImporter` setter are scattered and may diverge from `PopulateFormFromDto` over time. Refactor to delegate to the extracted helper.

**Independent Test**: Select Importer A → edit fields → select Importer B → every form field shows B's values.

- [X] T006 [US2] Refactor the `SelectedImporter` setter in `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs`: replace the 8 inline field assignments (`DisplayName = value.Dto.DisplayName; ...`) with a single call to `PopulateFormFromDto(value.Dto)` — keep `this.RaiseAndSetIfChanged` at the top of the setter

**Checkpoint**: US2 acceptance scenarios pass — select importer A (edit), select importer B, all 8 fields show B's data including AttachmentRegex and dropdown selections.

---

## Phase 5: User Story 3 — Form Clears on Deselect (Priority: P2)

**Goal**: When `SelectedImporter` becomes null, all form fields reset to empty/default state.

**Root cause**: The `SelectedImporter` setter has no `else` clause — null assignment silently leaves all fields populated.

**Independent Test**: Select importer → trigger `SelectedImporter = null` → all 8 fields are empty/default.

- [X] T007 [US3] Add `else` branch to the `SelectedImporter` setter in `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs`: when `value` is null, call `ClearForm()` (covers all 8 fields + `IsEditMode = false`)

- [X] T008 [US3] Refactor `OnAddNew()` in `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs`: replace the 8 inline field-clearing assignments with a single call to `ClearForm()`. Keep the `_selectedImporter = null; this.RaisePropertyChanged(nameof(SelectedImporter))` lines before the `ClearForm()` call so the setter's `else` branch does not fire recursively.

**Checkpoint**: US3 acceptance scenarios pass — importer selected with all fields populated, set selection to null, all 8 fields are cleared.

---

## Phase 6: User Story 4 — All Editable Fields Covered by Reset Logic (Priority: P2)

**Goal**: Audit and verify that `PopulateFormFromDto` and `ClearForm` are each complete — every editable/bound form field is handled in both helpers with no omissions.

**Independent Test**: Read both methods and compare against the 8-field list from spec.md FR-005.

- [X] T009 [US4] Audit `PopulateFormFromDto` in `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs` — confirm every one of the 8 editable fields (`DisplayName`, `ReportType`, `SelectedProfile`, `SelectedMailbox`, `FromFilter`, `SubjectFilter`, `AttachmentRegex`, `PaymentNotes`) is assigned from `dto`; add any missing field assignments

- [X] T010 [US4] Audit `ClearForm` in `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs` — confirm every one of the 8 editable fields is reset to empty/default; add any missing resets; verify `IsEditMode = false` is included

**Checkpoint**: Both methods provably cover all 8 fields — no field can be accidentally excluded.

---

## Phase 7: User Story 5 — Automated Tests Verify Reset Behaviour (Priority: P2)

**Goal**: Expand `ImporterSettingsViewModelTests` with ViewModel-level tests covering all three reset scenarios across all 8 fields.

**Tests are REQUIRED** per spec FR-008 and constitution CA-006.

**Independent Test**: Run `dotnet test tests/Rentier.Desktop.Tests/` — all new tests pass.

### Implementation for User Story 5

- [X] T011 [P] [US5] Add test `SaveUpdate_Repopulates_AllEightFields` in `tests/Rentier.Desktop.Tests/ImporterSettingsViewModelTests.cs` — set up `getImporters` to return a refreshed `ImporterDto` with known values for all 8 fields; execute `SaveCommand` in edit mode; assert every VM property matches the refreshed DTO (covers FR-002, SC-001, spec US1 acceptance scenario 1)

- [X] T012 [P] [US5] Add test `SaveUpdate_ItemVanishes_ClearsForm` in `tests/Rentier.Desktop.Tests/ImporterSettingsViewModelTests.cs` — after successful save the refreshed importer list does NOT contain the saved ID; assert `SelectedImporter` is null and all 8 fields are at empty/default (covers spec FR-007, US1 edge case)

- [X] T013 [P] [US5] Add test `SelectDifferentImporter_PopulatesAllEightFields` in `tests/Rentier.Desktop.Tests/ImporterSettingsViewModelTests.cs` — select Importer A (all fields non-empty), then assign `vm.SelectedImporter` to Importer B (different values including empty `AttachmentRegex`); assert all 8 properties match B's DTO — including that `AttachmentRegex` is empty (covers FR-003, SC-002, spec US2 acceptance scenario 2)

- [X] T014 [P] [US5] Add test `Deselect_ClearsAllEightFields` in `tests/Rentier.Desktop.Tests/ImporterSettingsViewModelTests.cs` — select an importer with all 8 fields populated, then set `vm.SelectedImporter = null`; assert every one of the 8 form properties is empty/default and `IsEditMode` is false (covers FR-004, SC-003, spec US3 acceptance scenarios 1 & 2)

- [X] T015 [P] [US5] Add test `SaveUpdate_OnFailure_PreservesEdits` in `tests/Rentier.Desktop.Tests/ImporterSettingsViewModelTests.cs` — `updateImporter.HandleAsync` returns an error `Result`; assert form fields retain the user-typed values that were set before the save attempt — form is NOT reset on failure (covers spec FR-006, US1 edge case)

- [X] T016 [P] [US5] Add test `AddNewCommand_ClearsAllEightFields` in `tests/Rentier.Desktop.Tests/ImporterSettingsViewModelTests.cs` — select an importer (all 8 fields populated), call `AddNewCommand.Execute().Subscribe()`, assert all 8 fields are at empty/default and `IsEditMode` is false (extends existing coverage, validates ClearForm completeness via US3)

**Checkpoint**: `dotnet test tests/Rentier.Desktop.Tests/` — all new and existing tests pass (SC-004).

---

## Phase 8: Polish & Verification

**Purpose**: Final build confirmation, regression check, and cleanup.

- [X] T017 Run `dotnet build src/Rentier.Desktop/Rentier.Desktop.csproj` and confirm zero compiler errors or warnings introduced by this change

- [X] T018 [P] Run `dotnet test tests/Rentier.Desktop.Tests/` and confirm all tests (existing + new) pass with no regressions

- [X] T019 [P] Review `ImporterSettingsViewModel.cs` final state: confirm `OnAddNew` no longer contains inline field assignments (all delegated to `ClearForm`), the `SelectedImporter` setter has both populate and clear branches, and `OnSaveAsync` calls `PopulateFormFromDto` / `ClearForm` on each branch

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user story phases**
- **Phase 3 (US1)**: Depends on Phase 2 (needs `PopulateFormFromDto` + `ClearForm`)
- **Phase 4 (US2)**: Depends on Phase 2 (needs `PopulateFormFromDto`)
- **Phase 5 (US3)**: Depends on Phase 2 (needs `ClearForm`)
- **Phase 6 (US4)**: Depends on Phases 3–5 (audits the fully-wired helpers)
- **Phase 7 (US5)**: Depends on Phases 3–6 (tests the completed behaviour)
- **Phase 8 (Polish)**: Depends on Phase 7

### User Story Dependencies

- **US1 (P1)** and **US2 (P1)** are independent of each other — can be done in either order after Phase 2
- **US3 (P2)** is independent of US1/US2 after Phase 2
- **US4 (P2)** should be done after US1–US3 to audit the fully-wired state
- **US5 (P2) tests** are most efficient after US1–US4 are complete, though individual tests can be written per story (TDD approach)

### Within Each User Story

- Phase 2 helpers first (T002, T003 in sequence — `ClearForm` before `PopulateFormFromDto` as add-path uses it)
- Story phases 3–5 are small (1–2 tasks each), inherently sequential within the single file
- All Phase 7 test tasks (`T011`–`T016`) are parallel (different test methods in same file with no shared state)

### Parallel Opportunities

- T011–T016 (all test tasks in Phase 7) can be written in parallel
- T017, T018, T019 (Polish) can run in parallel after Phase 7

---

## Parallel Example: User Story 5 Tests

```bash
# All new test methods are independent — write in parallel:
Task: T011 "SaveUpdate_Repopulates_AllEightFields"
Task: T012 "SaveUpdate_ItemVanishes_ClearsForm"
Task: T013 "SelectDifferentImporter_PopulatesAllEightFields"
Task: T014 "Deselect_ClearsAllEightFields"
Task: T015 "SaveUpdate_OnFailure_PreservesEdits"
Task: T016 "AddNewCommand_ClearsAllEightFields"
```

---

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational — extract helpers (T002, T003)
3. Complete Phase 3: US1 — post-save repopulation (T004, T005)
4. **STOP and VALIDATE**: manually test save → form shows persisted values
5. Merge or continue to US2

### Incremental Delivery

1. Setup + Foundational → helpers extracted
2. US1 + US2 (both P1) → core bugs fixed, independently testable
3. US3 → deselect cleared (P2 backlog)
4. US4 → completeness audit (P2 backlog)
5. US5 → full automated test coverage (P2 backlog)
6. Polish → build + regression confirmation

---

## Notes

- All changes are confined to **two files**: `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs` and `tests/Rentier.Desktop.Tests/ImporterSettingsViewModelTests.cs`
- No Domain, Application, Infrastructure, or View changes required (Clean Architecture boundary preserved per CA-001)
- `PopulateFormFromDto` and `ClearForm` are private — no public API changes
- The existing `MakeImporterDto` helper in the test class sets specific values for all 8 fields — new tests can rely on it or build variations with named parameters
- Save failure (FR-006) must NOT call `PopulateFormFromDto` or `ClearForm` — the error branch in `OnSaveAsync` must remain unchanged
- Each commit should be after a logical atomic change (e.g., extract helpers, wire US1, wire US2/US3, add tests)
