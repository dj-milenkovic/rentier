# Tasks: Reports Page – Smarter Naming and Sync Clarification

**Feature**: 022-reports-page-smarter-naming-sync  
**Branch**: `feature/021-024-qa-fixes`  
**Spec**: `.specify/specs/022-reports-page-smarter-naming-sync/spec.md`

**Tech Stack**: C# / .NET 8, Avalonia UI (MVVM), EF Core, SQLite, XUnit + NSubstitute + FluentAssertions  
**Layers touched**: Application · Infrastructure · Desktop  
**Tests**: Explicitly requested as User Story 3 (P3). Test tasks are included.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no shared-state dependency)
- **[US#]**: User story this task belongs to
- Exact file paths are included in all task descriptions

---

## Phase 1: Setup

**Purpose**: Orient to the feature, verify constitution compliance. No new projects, packages, or migrations required — this feature modifies only existing files across four layers.

- [ ] T001 Verify Clean Architecture boundaries hold: Application defines `IFilingRepository` contract → Infrastructure implements → Desktop consumes DTO. Confirm no Domain changes are needed (`Filing.IncomeDate` and `Report.ImportDate` already exist as `DateOnly`).

---

## Phase 2: Foundational (Blocking Prerequisite)

**Purpose**: Extend the `IFilingRepository` contract with the new query method. This is the single prerequisite that gates both the Application handler update (T005) and the Infrastructure implementation (T004). Nothing in US1 can be wired up until this interface exists.

**⚠️ CRITICAL**: T005 and T004 cannot start until T002 is complete.

- [ ] T002 Add `Task<DateOnly?> GetEarliestIncomeDateByReportIdAsync(Guid reportId, CancellationToken ct = default)` to `IFilingRepository` in `src/Rentier.Application/Repositories/IFilingRepository.cs`. Returns `null` when no filings exist for the report (the null signals the "no filings" fallback path in the handler). Include XML doc comment.

**Checkpoint**: Interface is extended — US1 implementation can now proceed.

---

## Phase 3: User Story 1 – Friendly Report Display Names (Priority: P1) 🎯 MVP

**Goal**: Each report row on the Reports page shows `"<ImporterDisplayName> – <yyyy-MM-dd>"` as its primary label, where the date is the earliest filing income date (or import date fallback). Original file name is preserved as a hover tooltip.

**Independent Test**: Import two reports: one via "IBKR CSV" importer with filings (earliest income date 2024-03-15), one with no filings (import date 2024-07-09). Open Reports page — verify rows show `"IBKR CSV – 2024-03-15"` and `"IBKR CSV – 2024-07-09"`. Hover over each display name — verify tooltip shows the original CSV filename.

### Implementation for User Story 1

- [ ] T003 [P] [US1] Add `DisplayName` property to the `ReportRowDto` record in `src/Rentier.Application/DTOs/ReportRowDto.cs`. The property is a `string` positioned after `ImporterName`. Keep all existing properties (`Id`, `ReportName`, `ImportDate`, `ImporterName`, `Status`, `FilingCount`) unchanged — `ReportName` remains available for the Desktop tooltip binding.

- [ ] T004 [P] [US1] Implement `GetEarliestIncomeDateByReportIdAsync` in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs`. Use EF Core: `await _db.Filings.AsNoTracking().Where(f => f.ReportId == reportId).MinAsync(f => (DateOnly?)f.IncomeDate, ct)`. This returns `null` (not an exception) when no filings match.

- [ ] T005 [US1] Update `GetReportsQueryHandler.HandleAsync` in `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs` to: (1) call `_filings.GetEarliestIncomeDateByReportIdAsync(r.Id, ct)` alongside the existing `GetFilingCountByReportIdAsync` call, (2) derive `displayName` as `$"{importerName} \u2013 {(earliestDate ?? r.ImportDate):yyyy-MM-dd}"` (en dash U+2013), (3) pass `displayName` as the new `DisplayName` argument in the `ReportRowDto` constructor. Depends on T002, T003, T004.

- [ ] T006 [P] [US1] Add `public string DisplayName { get; }` to `ReportRowViewModel` in `src/Rentier.Desktop/ViewModels/ReportRowViewModel.cs`. Map it from `dto.DisplayName` in the private constructor and `From` factory. `ReportName` is already present and serves as the tooltip source — no rename needed.

- [ ] T007 [US1] Update `ReportsView.axaml` in `src/Rentier.Desktop/Views/ReportsView.axaml`: change the Name `DataGridTextColumn` `Binding` from `{Binding ReportName}` to `{Binding DisplayName}` and add a `DataGridTemplateColumn` tooltip (or use `DataGridTextColumn.ElementStyle` with `ToolTip.Tip="{Binding ReportName}"`) so hovering the display name shows the original file name. Depends on T006.

**Checkpoint**: Reports page shows friendly display names with tooltips. User Story 1 is independently verifiable.

---

## Phase 4: User Story 2 – Sync Button Clarification (Priority: P2)

**Goal**: A descriptive info text appears near the "Sync Mailboxes" button on the Reports page, explaining what the sync action does and how it differs from the dedicated Sync page. All text sourced from `Strings.resx`.

**Independent Test**: Navigate to Reports page — verify a short explanatory text block is visible near/below the Sync button without clicking anything. Inspect `Strings.resx` to confirm the text is resource-bound (not hardcoded in AXAML).

### Implementation for User Story 2

- [ ] T008 [P] [US2] Add two new string resource entries to `src/Rentier.Desktop/Resources/Strings.resx`:
  - `Reports_Sync_InfoText` — e.g. `"Syncing downloads new statements from configured mailboxes and processes them into reports."`
  - `Reports_Sync_DiffersFromSyncPage` — e.g. `"For per-mailbox status and sync history, see the Sync page."` 
  Place both entries in alphabetical order within the Reports section.

- [ ] T009 [US2] Regenerate `src/Rentier.Desktop/Resources/Strings.Designer.cs` to expose the two new properties (`Reports_Sync_InfoText`, `Reports_Sync_DiffersFromSyncPage`). Follow the existing project regeneration workflow (ResXFileCodeGenerator or equivalent — check `.csproj` for the generator configuration). Depends on T008.

- [ ] T010 [US2] Update `src/Rentier.Desktop/Views/ReportsView.axaml`: directly below the existing toolbar `StackPanel`, add a `StackPanel` (DockPanel.Dock="Top") containing two `TextBlock` elements bound to `{x:Static res:Strings.Reports_Sync_InfoText}` and `{x:Static res:Strings.Reports_Sync_DiffersFromSyncPage}`, with appropriate `Margin`, `TextWrapping="Wrap"`, and a muted foreground style (e.g. `Foreground="{DynamicResource SystemControlForegroundBaseMediumBrush}"`). Depends on T009.

**Checkpoint**: Info banner visible on Reports page. User Story 2 is independently verifiable without touching US1 files.

---

## Phase 5: User Story 3 – Unit Tests for Display Name Derivation (Priority: P3)

**Goal**: Comprehensive unit tests in `GetReportsQueryHandlerTests.cs` verify the display name derivation logic — happy path (earliest income date), fallback (no filings → import date), and edge case (unresolvable importer → "Unknown").

**Independent Test**: `dotnet test tests/Rentier.Application.Tests --filter "GetReportsQueryHandler"` — all tests green.

**Note**: US3 tests are pure Application-layer logic tests. They mock `IFilingRepository` via NSubstitute and verify `ReportRowDto.DisplayName`. The `GetEarliestIncomeDateByReportIdAsync` mock must be set up in all existing tests too — NSubstitute returns `null` (`DateOnly?`) by default for the new method, which correctly triggers the import-date fallback path. Existing tests will pass without modification provided the fallback uses `r.ImportDate` when `earliestDate` is null.

### Tests for User Story 3

- [ ] T011 [P] [US3] Add test `HandleAsync_WithFilings_DisplayNameUsesEarliestIncomeDate` to `tests/Rentier.Application.Tests/GetReportsQueryHandlerTests.cs`: set up importer "IBKR CSV", report with `ImportDate = 2024-07-09`, mock `GetEarliestIncomeDateByReportIdAsync` returning `DateOnly(2024, 3, 15)`. Assert `dto.DisplayName == "IBKR CSV \u2013 2024-03-15"`.

- [ ] T012 [P] [US3] Add test `HandleAsync_WithNoFilings_DisplayNameFallsBackToImportDate` to `tests/Rentier.Application.Tests/GetReportsQueryHandlerTests.cs`: mock `GetEarliestIncomeDateByReportIdAsync` returning `null`. Assert `dto.DisplayName` uses `report.ImportDate` formatted as `yyyy-MM-dd` (e.g. `"IBKR CSV \u2013 2024-07-09"`).

- [ ] T013 [P] [US3] Add test `HandleAsync_WithUnresolvableImporter_DisplayNameUsesUnknown` to `tests/Rentier.Application.Tests/GetReportsQueryHandlerTests.cs`: return no importers, mock `GetEarliestIncomeDateByReportIdAsync` returning a known date. Assert `dto.DisplayName` starts with `"Unknown \u2013"`.

- [ ] T014 [US3] Update existing test `HandleAsync_MapsAllDtoFieldsCorrectly` in `tests/Rentier.Application.Tests/GetReportsQueryHandlerTests.cs`: (1) set up `GetEarliestIncomeDateByReportIdAsync` mock, (2) add assertion `dto.DisplayName.Should().Be("IBKR EU \u2013 <expected-date>")`. This prevents regression if `DisplayName` mapping is silently dropped.

- [ ] T015 [US3] Run `dotnet test tests/Rentier.Application.Tests` and confirm all tests in the assembly pass (including pre-existing handler tests). Fix any NSubstitute setup gaps in pre-existing tests if required.

**Checkpoint**: All display-name unit tests pass. User Story 3 independently verified.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T016 [P] Run full solution test suite `dotnet test` from repository root and confirm zero regressions across all test projects (`Rentier.Domain.Tests`, `Rentier.Application.Tests`, `Rentier.Infrastructure.Tests`, `Rentier.Desktop.Tests`).

- [ ] T017 [P] Review `Reports_Col_Name` string value in `src/Rentier.Desktop/Resources/Strings.resx` — if the column header should be updated from "Name" to "Report" or similar to reflect the new friendly display name semantics, update it now; otherwise confirm the existing value is still appropriate.

- [ ] T018 Verify the en dash character (–, U+2013) renders correctly in the Avalonia DataGrid cell on the target platform. If rendering issues arise, confirm font support or substitute with ` - ` and update display name derivation in `GetReportsQueryHandler`.

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)         → no dependencies, start immediately
Phase 2 (Foundational)  → depends on Phase 1
Phase 3 (US1, P1)       → T003, T004 unblock after T002 (Phase 2)
                          T005 requires T002 + T003 + T004
                          T006 requires T003; T007 requires T006
Phase 4 (US2, P2)       → fully independent of Phase 3 (different files)
                          T009 requires T008; T010 requires T009
Phase 5 (US3, P3)       → T011–T013 require Phase 3 complete (implementation must exist)
                          T014 requires T011–T013
                          T015 requires T011–T014
Phase 6 (Polish)        → requires all user story phases complete
```

### User Story Dependencies

| Story | Depends on | Notes |
|-------|-----------|-------|
| US1 (P1) | Phase 2 (T002) | Core naming feature |
| US2 (P2) | None | Pure UI text — fully independent of US1 |
| US3 (P3) | US1 complete | Tests exercise the handler logic from US1 |

### Within Phase 3 (US1)

```
T002 (interface) → T003 (DTO) ──────────────────────────┐
                 → T004 (infrastructure impl) ──────────→ T005 (handler) → T006 (VM) → T007 (View)
```

T003 and T004 are parallelizable after T002.  
T005 requires T002 + T003 + T004.  
T006 requires T003.  
T007 requires T006.

### Parallel Opportunities

```bash
# After T002 completes, run in parallel:
Task T003: "Add DisplayName to ReportRowDto in src/Rentier.Application/DTOs/ReportRowDto.cs"
Task T004: "Implement GetEarliestIncomeDateByReportIdAsync in src/Rentier.Infrastructure/Repositories/FilingRepository.cs"

# US2 can run entirely in parallel with US1 (no shared files):
Task T008: "Add sync info string keys to Strings.resx"

# US3 tests can run in parallel once US1 is complete:
Task T011: "HandleAsync_WithFilings_DisplayNameUsesEarliestIncomeDate"
Task T012: "HandleAsync_WithNoFilings_DisplayNameFallsBackToImportDate"
Task T013: "HandleAsync_WithUnresolvableImporter_DisplayNameUsesUnknown"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (Setup) — constitution check
2. Complete Phase 2 (Foundational) — extend `IFilingRepository`
3. Complete Phase 3 (US1) — display name end-to-end
4. **STOP and VALIDATE**: Import reports, verify friendly names and tooltips on Reports page
5. Merge US1 if QA confirms the naming fix

### Incremental Delivery

1. Setup + Foundational → unblocks all further work
2. US1 → test independently → **fixes primary QA complaint** (MVP)
3. US2 → test independently → **fixes sync button confusion** (parallel with US3 if staffed)
4. US3 → run tests → **adds regression coverage** (can be merged last)
5. Polish → full suite green → merge PR

---

## Notes

- `ReportName` is **not removed** from `ReportRowDto` or `ReportRowViewModel` — it remains available as the tooltip source (FR-005).
- The en dash (`\u2013`) separator is specified in the spec assumptions. Use the Unicode escape in the C# string literal to avoid encoding issues.
- NSubstitute returns `default(DateOnly?)` = `null` for unmocked `Task<DateOnly?>` calls, which is exactly the "no filings" fallback. Existing tests in `GetReportsQueryHandlerTests.cs` do not need mock setup for `GetEarliestIncomeDateByReportIdAsync` unless they explicitly assert `DisplayName` — the fallback path is safe.
- `Strings.Designer.cs` is auto-generated — do not edit it manually. Regenerate it after every `.resx` change (T009).
- [P] tasks target different files and have no incomplete task dependencies — safe to implement in parallel.
