# Specification Analysis Report — Feature 014: Reports List & Manual Import

**Date**: 2026-04-07  
**Branch**: `feature/003-reports-manual-import`  
**Artifacts analysed**: `spec.md`, `clarify.md`, `plan.md`, `data-model.md`,
`contracts/application-contracts.md`, `tasks.md`  
**Source files verified**: `IReportRepository`, `IFilingRepository`, `FilingRepository`,
`ReportRepository`, `Report.cs`, `ReportStatus.cs`, `ReportsViewModel`, `FilingsViewModel`,
`MainWindowViewModel`, `CompositionRoot`, `InfrastructureServiceExtensions`,
`ProcessReportsCommandHandler`, `Result.cs`, `VoidResult.cs`, `Error.cs`, `NavigationEntry.cs`

---

## Findings Table

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| H1 | Inconsistency | **HIGH** | `spec.md:138`, `clarify.md:71` | `spec.md` "Repository Extensions Required" and `clarify.md` §Decision 4 both say `IFilingRepository.DeleteByReportIdAsync` uses a single `ExecuteDeleteAsync` call (EF 7+ bulk delete). This DIRECTLY contradicts the explicit prohibition in `plan.md`, `data-model.md`, `application-contracts.md`, and `tasks.md T004`. `ExecuteDeleteAsync` breaks SQLite in-memory tests. | Treat `plan.md` / `data-model.md` / `tasks.md` as authoritative. No code change needed — tasks already specify the correct load-then-remove pattern. **Add a prominent ⚠️ note to T004** so the developer ignores conflicting spec/clarify references. |
| H2 | Inconsistency | **HIGH** | `spec.md:137` | `spec.md` "Repository Extensions Required" lists `IReportRepository.GetAllWithFilingCountAsync` as a new method to add to the interface. This method does **not** appear in `IReportRepository.cs`, is not added by any task, and was explicitly replaced in `plan.md` §R-001 with the `GetAllAsync` + per-report `GetFilingCountByReportIdAsync` approach. | Treat `plan.md` §R-001 as authoritative — `GetAllWithFilingCountAsync` is NOT to be added. **Add a clarifying note to T010** that `IReportRepository` requires no new methods and that `spec.md` §"Repository Extensions Required" is superseded by `plan.md` §R-001. |
| H3 | Inconsistency / DI breakage | **HIGH** | `tasks.md:T025`, `tasks.md:T029`, `CompositionRoot.cs:33`, `MainWindowViewModel.cs:32-34` | T025 says "DI already resolves both filingsVm and reportsVm" as constructor parameters of `MainWindowViewModel`. T029 says to "remove the existing `services.AddTransient<ReportsViewModel>()` stub". Together these are contradictory: if T029 is executed, `MainWindowViewModel`'s current constructor (`FilingsViewModel filingsVm, ReportsViewModel reportsVm, SettingsViewModel settingsVm`) will fail DI resolution at startup because `ReportsViewModel` is no longer registered. | **T025 must be updated** to specify a definitive construction strategy (see remediation note below). The recommended approach: modify `MainWindowViewModel` to inject `IServiceProvider` and construct `ReportsViewModel` via `ActivatorUtilities.CreateInstance<ReportsViewModel>(provider, navigateToFilings)`. The constructor parameter `ReportsViewModel reportsVm` must be removed. Add a corrective note to T025. |
| H4 | Underspecification | **HIGH** | `tasks.md:T025` | T025 offers three mutually-exclusive construction strategies for `ReportsViewModel` ("register as factory OR construct directly via `ActivatorUtilities` OR inject via DI registration — choose the approach consistent with the codebase") without committing to one. No existing codebase pattern exists for this scenario (`MainWindowViewModel` has never needed a delegate-parameterised ViewModel before). This ambiguity risks an incorrect implementation choice (e.g., resolving `ReportsViewModel` from DI without the delegate). | **Resolve in T025**: commit to `ActivatorUtilities.CreateInstance<ReportsViewModel>(provider, navigateToFilings)` approach. `MainWindowViewModel` should inject `IServiceProvider` (already available as `Microsoft.Extensions.DependencyInjection.IServiceProvider`). Update T025 with explicit steps. |
| M1 | Stale Spec | **MEDIUM** | `spec.md:137,159,172-173`, `clarify.md:§Decision 1` | `spec.md` "Repository Extensions Required", `spec.md` Assumptions, and `clarify.md` §Decision 1 all reference `IReportRepository.GetAllWithFilingCountAsync` (single GroupJoin query). `plan.md` §R-001 explicitly superseded this with `GetAllAsync` + per-report `GetFilingCountByReportIdAsync` to reduce complexity. The spec and clarify.md are now stale and may mislead implementers. | Informational only (spec is superseded by plan). **Add a note to T010** cross-referencing `plan.md §R-001` as authoritative for the query strategy. No code change needed. |
| M2 | Performance Risk | **MEDIUM** | `tasks.md:T010`, `plan.md:§R-001` | `GetReportsQueryHandler` issues one `CountAsync` query per report (N+1) to populate `FilingCount`. For 500 reports (SC-001: ≤ 2s load), this is 500 individual SQLite queries. `plan.md §R-001` explicitly acknowledges this as an accepted trade-off ("deferred to future optimisation if SC-001 is not met"). SC-001 compliance has not been benchmarked. | `plan.md` acceptance is documented. Recommend adding a performance test or acceptance note in T011/T012 that SC-001 should be manually validated on realistic data. No task change required unless SC-001 is at risk. |
| M3 | Inconsistency | **MEDIUM** | `plan.md:656`, `tasks.md:T017`, `tasks.md:T014` | `plan.md` structural AXAML snippet uses `Converter={StaticResource ReportStatusDisplayConverter}` (requires an AXAML `<UserControl.Resources>` block to declare the converter). T017 correctly uses `{x:Static local:ReportStatusDisplayConverter.Instance}` (static field access, consistent with T014 which defines the `Instance` field). The plan example is misleading and will cause a runtime resource-not-found error if copied verbatim. | T017 is correct — use `{x:Static local:ReportStatusDisplayConverter.Instance}`. **Add a ⚠️ note to T017** that `plan.md` structural example uses `{StaticResource ...}` but the correct binding is `{x:Static ...}`. |
| M4 | Stale Clarify | **MEDIUM** | `clarify.md:§Decision 3` | `clarify.md` §Decision 3 sets `SelectedEntry = NavigationEntries[0]` (hardcoded index) to navigate to Filings pane. `plan.md`, `application-contracts.md`, and T025 all use `NavigationEntries.First(e => e.ViewModel is FilingsViewModel)` — a robust, index-independent lookup. Index `[0]` is coincidentally correct today but fragile; if navigation order changes it silently navigates to the wrong pane. | `plan.md` / contracts are authoritative. No task change needed — T025 already specifies the LINQ approach. Informational for developer: ignore `clarify.md §Decision 3` on this point. |
| L1 | Duplication / Count Error | **LOW** | `tasks.md:T013` | T013 says "Add all **25** new string keys" but the body of the task lists **27** distinct keys (including `ReportStatus_Init`, `ReportStatus_Processed`, `ReportStatus_Error`). The count is wrong; a developer who validates by counting could stop early. | Fix count in T013 description to "27 new string keys". |
| L2 | Ambiguity | **LOW** | `application-contracts.md:295-296` | Contracts mention `new Error(string message)` as a "single-arg overload if present". The actual `Error.cs` is `sealed record Error(string Code, string Message)` — there is no single-arg constructor. The "if present" hedge is confusing. All handler tasks correctly use the 2-arg form. | Remove the single-arg mention from contracts. No implementation impact. |
| L3 | Style / Pattern | **LOW** | `tasks.md:T024`, `application-contracts.md:navigation` | `ReportIdFilter` setter calls `LoadPageCommand.Execute().Subscribe()` without `.DisposeWith`. This is not inside `WhenActivated`, so `DisposeWith` is not directly applicable. The existing `FilingsViewModel.ShowAll` setter (line 57) uses the identical pattern. Consistent with existing code. | No action required. Subscription is short-lived and consistent with codebase convention. Informational only. |

---

## Coverage Summary

| Requirement Key | Has Task? | Task IDs | Notes |
|-----------------|-----------|----------|-------|
| FR-001 (DataGrid columns) | ✅ | T013, T015, T016, T017 | Fully covered |
| FR-002 (Auto-refresh on activation) | ✅ | T016 (WhenActivated), T018 | Covered via IActivatableViewModel |
| FR-003 (Import button + file picker) | ✅ | T021 | showImportDialog delegate |
| FR-004 (Importer selector dropdown) | ✅ | T021 | Populated from IImporterRepository |
| FR-005 (Auto-select single importer) | ✅ | T021 | Specified in task body |
| FR-006 (No importers → abort) | ✅ | T021 | Specified in task body |
| FR-007 (CSV validation before persist) | ✅ | T019 | Step 1 of ImportReportCommandHandler |
| FR-008 (Duplicate detection) | ✅ | T019 | Step 2 of ImportReportCommandHandler |
| FR-009 (Persist Report at Init) | ✅ | T019 | Step 3 of ImportReportCommandHandler |
| FR-010 (Trigger ProcessReportsCommand) | ✅ | T019 | Step 4 — uses `new ProcessReportsCommand()` (no reportId) ✅ |
| FR-011 (Pipeline status visible) | ✅ | T016, T019 | LoadReportsAsync reloads after import |
| FR-012 (View Filings navigation) | ✅ | T022, T024, T025 | navigateToFilings delegate |
| FR-013 (Delete with confirmation dialog) | ✅ | T016, T029 | confirmDelete Func<string,string,Task<bool>> |
| FR-014 (Cascade delete filings then report) | ✅ | T027 | DeleteReportCommandHandler sequence |
| FR-015 (All strings in Strings.resx) | ✅ | T013 | 27 keys listed |
| FR-016 (All I/O async) | ✅ | T016 (ReactiveCommand.CreateFromTask) | Consistent throughout |
| FR-017 (Preserve SyncCommand) | ✅ | T016 | SyncCommand preserved verbatim |
| FR-018 (CQRS handler registration) | ✅ | T029 | All in CompositionRoot, AddTransient |
| FR-019 (IActivatableViewModel) | ✅ | T016, T018 | WhenActivated + DisposeWith |
| SC-001 (≤ 2s for 500 reports) | ⚠️ | T010 | N+1 count queries — plan accepts; benchmark needed |
| SC-002 (Import ≤ 30s) | ✅ | T019, T021 | Async throughout |
| SC-003 (Invalid CSV rejection ≤ 3s) | ✅ | T019 | Parse before persist |
| SC-004 (100% duplicate detection) | ✅ | T019 | ExistsByImporterAndNameAsync |
| SC-005 (Delete ≤ 3s) | ✅ | T027 | load-then-remove; lightweight |
| SC-006 (UI non-blocking) | ✅ | T016 | All ops via ReactiveCommand.CreateFromTask |
| SC-007 (Localizable strings) | ✅ | T013, T015, T016 | All strings in Strings.resx |

---

## Constitution Alignment

All five constitution principles are correctly addressed in `plan.md` §"Constitution Check". No
violations identified in tasks or contracts. Key confirmations:

| Principle | Status |
|---|---|
| I. Clean Architecture | ✅ — Desktop injects IQueryHandler/ICommandHandler only; navigateToFilings wired at Desktop composition layer |
| II. Local-First Security | ✅ — No new network calls; CSV bytes stored locally; no credentials involved |
| III. Financial/Temporal Correctness | ✅ — `ImportDate` is `DateOnly` end-to-end; no monetary values in this feature |
| IV. Async/UI Responsiveness | ✅ — All ReactiveCommand.CreateFromTask; file picker awaited; WhenActivated with DisposeWith |
| V. Specification-Driven Quality Gates | ✅ — All tasks traceable to FRs; test tasks defined for all 3 handlers + 2 infra methods + ViewModel |

---

## Unmapped Tasks

All 31 tasks map to at least one functional requirement or user story. No orphaned tasks found.

| Task | Mapped To |
|------|-----------|
| T001 | Branch verification — supporting |
| T002–T009 | Phase 2 foundation — FR-002,007,008,009,010,012,013,014,018 |
| T010–T018 | US1 — FR-001,002,015,019 |
| T019–T021 | US2 — FR-003,004,005,006,007,008,009,010,011 |
| T022–T025 | US3 — FR-012 |
| T026–T028 | US4 — FR-013,014 |
| T029–T031 | Cross-cutting — FR-018, constitution |

---

## Metrics

| Metric | Value |
|--------|-------|
| Total Functional Requirements | 19 (FR-001–FR-019) |
| Total Success Criteria | 7 (SC-001–SC-007) |
| Total Tasks | 31 |
| Requirements with ≥1 task | 19/19 — **100%** |
| Critical (constitution) issues | 0 |
| HIGH findings | 4 |
| MEDIUM findings | 4 |
| LOW findings | 3 |
| Total findings | 11 |

---

## Next Actions

### Must resolve before `/speckit.implement`

| Priority | Finding | Action |
|----------|---------|--------|
| 🔴 H1 | `ExecuteDeleteAsync` in spec.md + clarify.md | Add ⚠️ WARNING note to **T004** that spec.md:138 and clarify.md §Decision 4 are WRONG; load-then-remove pattern is correct and non-negotiable |
| 🔴 H2 | `GetAllWithFilingCountAsync` in spec.md | Add ⚠️ note to **T010** that spec.md:137 is superseded by plan.md §R-001; use `GetAllAsync` + per-report count |
| 🔴 H3/H4 | T025 ↔ T029 DI contradiction | **Update T025** with explicit strategy: `MainWindowViewModel` injects `IServiceProvider`; creates `ReportsViewModel` via `ActivatorUtilities.CreateInstance<ReportsViewModel>(provider, navigateToFilings)`; removes `ReportsViewModel reportsVm` constructor param |

### Safe to proceed (Low/Medium only)

- M2 (N+1 performance): acceptable per plan §R-001; validate SC-001 with real data after implementation
- M3 (AXAML converter reference): T017 is already correct — no code change needed
- M4 (clarify.md index hardcode): T025 already uses LINQ — no code change needed
- L1–L3: cosmetic; no implementation risk

---

## Known-Facts Verification Summary

| # | Fact | Status |
|---|------|--------|
| 1 | `DeleteByReportIdAsync` uses load-then-remove, NEVER `ExecuteDeleteAsync` | ⚠️ **Tasks correct; spec.md:138 and clarify.md §Decision 4 are wrong** → H1 |
| 2 | `GetFilingCountByReportIdAsync` uses `CountAsync`, no entity loading | ✅ Consistent across all artifacts and tasks |
| 3 | `IReportRepository.GetAllAsync` already exists | ✅ Confirmed in `IReportRepository.cs:9`; no new method needed |
| 4 | `ProcessReportsCommand` has no reportId param; processes ALL Init reports | ✅ Confirmed in `ProcessReportsCommand.cs` and `ProcessReportsCommandHandler.cs`; T019 correct |
| 5 | `Report.Create(Guid, string, byte[]?, long?)` — 4-param factory | ✅ Confirmed in `Report.cs:24-28`; T019 uses `mailboxMessageId: null` correctly |
| 6 | `Func<string, string, Task<bool>>` (2-arg) vs `Func<string, Task<bool>>` (1-arg) — distinct DI types, no conflict | ✅ Different C# types; DI registers them independently; no collision |
| 7 | `ReportsViewModel` constructor needs `Action<Guid> navigateToFilings` — wired in `MainWindowViewModel`, NOT DI | ⚠️ **T025 and T029 are contradictory; construction strategy underspecified** → H3/H4 |
| 8 | `GetFilingsQuery.ReportIdFilter` bypasses pagination; handler uses `GetByReportIdAsync` | ✅ `GetByReportIdAsync` confirmed in `IFilingRepository.cs:12` and `FilingRepository.cs:48-55`; T022 correct |
| 9 | `FilingsViewModel.ReportIdFilter` resets page 1 and triggers `LoadPageCommand.Execute()` | ✅ T024 specifies correctly; matches existing `ShowAll` setter pattern |
| 10 | All handlers registered in `CompositionRoot.AddDesktopServices()` — NOT InfrastructureServiceExtensions | ✅ T029 correct; infra handlers already correctly in `InfrastructureServiceExtensions` |
| 11 | `AddTransient` ONLY — no `AddScoped` | ✅ All tasks and registrations consistently use `AddTransient` |
| 12 | No EF migration — `ReportId` already exists on Filings | ✅ Confirmed by `FilingRepository.cs:48-55` using `f.ReportId` |
| 13 | `WhenActivated` subscriptions → `.DisposeWith(disposables)` | ✅ T016 correct; matches `FilingsViewModel.cs:182` existing pattern |
| 14 | `x:CompileBindings="False"` on ReportsView | ✅ T017 correct; current `ReportsView.axaml:5` has `x:DataType` (compiled) — T017 is a full replacement |
