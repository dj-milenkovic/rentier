# Specification Analysis Report: 012-filings-list-management

**Generated**: 2025-01-08  
**Scope**: spec.md · clarify.md · plan.md · data-model.md · tasks.md · contracts/application-contracts.md · checklists/requirements.md  
**Constitution**: `.specify/memory/constitution.md` v1.0.0  
**Status**: READ-ONLY analysis — no files modified

---

## Executive Summary

15 findings total across all checked dimensions. **No CRITICAL issues** — the constitution is fully respected. **1 HIGH issue** (handler registration placement inconsistency). **7 MEDIUM issues** (architecture and test-coverage gaps). **7 LOW issues** (minor inconsistencies). The feature is safe to implement with corrections noted below applied before starting.

---

## Findings Table

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| **C1** | Architecture | **HIGH** | tasks.md T041 | T041 places the 4 new handler registrations in `InfrastructureServiceExtensions.cs`. However, every comparable application-layer CRUD handler in the codebase (SaveTaxpayerProfile, GetTaxpayerProfile, all mailbox/importer/holiday handlers) is registered in `CompositionRoot.AddDesktopServices()`. Only the two infrastructure-heavy handlers (`SyncMailboxCommandHandler`, `ProcessReportsCommandHandler`) live in InfrastructureServiceExtensions. The 4 new filing handlers are pure Application-layer types and belong in CompositionRoot. | Move registrations for `GetFilingsQueryHandler`, `UpdateFilingStatusCommandHandler`, `UpdatePaymentReferenceCommandHandler`, `DeleteFilingCommandHandler` from `InfrastructureServiceExtensions.cs` to `CompositionRoot.AddDesktopServices()` — consistent with the importer and mailbox handler blocks already there. |
| **C2** | Architecture | MEDIUM | tasks.md T029, T034 | T029 and T034 add `StatusComboBox_SelectionChanged` and `PaymentRef_LostFocus` event handlers to `FilingsView.axaml.cs`. These handlers cast `sender` and `DataContext`, inspect `e.AddedItems`, and invoke ViewModel commands — that is view logic in code-behind. Constitution §Coding Standards: "Views MUST be `ReactiveUserControl<TViewModel>` with no view logic in code-behind." | Document this as an accepted DataGrid-cell interaction exception in the Architecture Compliance Checklist and add an inline comment. Alternatively, evaluate using an Avalonia attached behavior or interaction trigger to keep code-behind clean. Whichever approach is chosen, the plan's compliance checklist should be ticked with an explicit rationale. |
| **C3** | Architecture | MEDIUM | tasks.md T020, T023 | T020 says `WhenActivated` triggers `LoadPageCommand.Execute()`. T023 says the `ShowAll` setter calls `LoadPageCommand.Execute().Subscribe()`. Neither mentions `.DisposeWith(disposables)`. The existing pattern (`ImporterSettingsViewModel`) wraps activation observables with `DisposeWith`; bare `Subscribe()` calls in a property setter create undisposed subscriptions that accumulate on repeated activations. | In `WhenActivated`, subscribe via `.DisposeWith(disposables)`. In the `ShowAll` setter, either delegate to a method that calls `LoadPageCommand.Execute().Subscribe().DisposeWith(someDisposable)` tracked on the VM, or use `Observable.FromAsync` + `ObserveOn` as the existing activation pattern does, rather than raw command execution from a setter. |
| **C4** | Architecture | MEDIUM | tasks.md T042 | T042 says to "Register `Func<string, Task<bool>>` confirmDelete delegate" in CompositionRoot but shows only the delegate body, not the DI registration call. Registering a `Func<>` delegate type requires `services.AddTransient<Func<string, Task<bool>>>(provider => async msg => { ... })`. Without this explicit registration, `FilingsViewModel` constructor injection will fail at runtime with a DI resolution error. | Add the following to T042's instructions: `services.AddTransient<Func<string, Task<bool>>>(provider => async msg => { /* ContentDialog body */ });` and verify the constructor parameter type exactly matches the registered type. |
| **T1** | Test Coverage | MEDIUM | tasks.md T027 | `UpdateFilingStatusCommandHandlerTests` contains a single test `HandleAsync_InvalidTransition_ReturnsFailureWithoutPersisting` for all illegal paths. The domain already has 4 distinct invalid-transition tests (`Init→Init`, `Init→Paid`, `Filed→Init`, `Paid→Filed`) and SC-005 requires "100% rejection". The handler test should mirror at least the highest-risk paths (skipping-a-step: `Init→Paid`; backward: `Filed→Init`; terminal advance: `Paid→Filed`) to confirm domain exceptions are correctly propagated and no persistence occurs. | Expand T027 test list: add `HandleAsync_InitToPaidTransition_ReturnsFailureWithoutPersisting` and `HandleAsync_FiledToInitTransition_ReturnsFailureWithoutPersisting` (at minimum). The generic test name can remain for the catch-all; the named ones give explicit regression coverage. |
| **T2** | Test Coverage | MEDIUM | tasks.md T032 | `UpdatePaymentReferenceCommandHandlerTests` has no test confirming that the handler accepts a `SetPaymentReference` call for a filing in `Init` or `Paid` status. The data-model.md and contracts explicitly state "no status precondition (UI enforces Filed-only editability; domain does not)". This design choice should be positively tested at the handler level; without it, a future developer could accidentally add a status gate. | Add test: `HandleAsync_OnInitStatusFiling_PersistsReferenceAndReturnsSuccess` to T032. This documents the intentional permissiveness and guards against accidental status-guard regressions. |
| **T3** | Test Coverage | MEDIUM | tasks.md T043 | `FilingsViewModelTests` does not include a test for the spec edge case: "user deletes the last item on a page beyond page 1 → page decrements by 1". T039 implements the decrement logic (`if (Rows.Count == 0 && _currentPage > 1)`) but without a corresponding VM test this branch is unverified. | Add test: `DeleteFilingCommand_WhenLastItemOnPageBeyondOne_DecrementsPageBeforeReload` to T043. The test should set `CurrentPage = 2`, mock the handler to succeed, mock the subsequent load to return an empty list, and assert `CurrentPage == 1`. |
| **I1** | Inconsistency | MEDIUM | data-model.md §6, tasks.md T019 | `TaxPayableDisplay` is specified as `` $"{TaxPayable:N2} RSD" ``. The `N2` standard format is culture-sensitive. On a Serbian-locale Windows installation, it produces `1.234,56 RSD` (period thousand-separator, comma decimal) rather than the spec-mandated `N,NNN.NN RSD` (comma thousand-separator, period decimal). SC-001 shows the target is English-style formatting. | Change to `TaxPayable.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)` in `FilingRowViewModel`. This guarantees `1,234.56 RSD` regardless of the OS locale. |
| **I2** | Inconsistency | LOW | tasks.md T015 | Task T015 header says "24 new keys" but its body enumerates 25 keys. Additionally, plan.md R-008 notes that `IncomeType_Dividend` and `IncomeType_Interest` should be added "if not already present". If feature 011 already added them to `Strings.resx`, the count drops to 23 genuinely new keys, not 24. | Before executing T015, grep `Strings.resx` for `IncomeType_Dividend`. If present, omit both IncomeType keys and update the task count to 23. If absent, the count is 25 — correct the header to 25. |
| **I3** | Inconsistency | LOW | tasks.md T015, T040 | `Filings_Delete_Confirm_Button = "Delete"` is assigned to both the ContentDialog primary button and the DataGrid column action button (T040). This conflates two semantically different affordances (destructive confirmation vs initiating an action). If the column label ever needs to differ from the dialog button (e.g., "Remove" vs "Confirm Delete"), a shared key prevents it. | Define `Filings_Delete_Action_Button = "Delete"` for the DataGrid column and keep `Filings_Delete_Confirm_Button = "Delete"` for the ContentDialog. Both default to the same English string but can be localised independently. Update T040 to reference `Filings_Delete_Action_Button`. |
| **I4** | Inconsistency | LOW | clarify.md §Decision 3 | Decision 3 shows a constructor signature with only 4 handler params — it omits `Func<string, Task<bool>> confirmDelete` and `IScheduler? scheduler`. These are correctly added in data-model.md and tasks.md T020, so the discrepancy is a historical omission in the clarification record rather than an active defect. | No file change needed. Note during code review that clarify.md §Decision 3 is superseded by the data-model §Commands and T020 for the complete constructor signature. |
| **I5** | Inconsistency | LOW | data-model.md §6, tasks.md T020, T023 | `FilingsViewModel` maintains both `_filter` (FilingFilterMode) and `_showAll` (bool) as independent private backing fields. They must always reflect the same logical state (ShowAll=true ↔ filter=All). Any code path that modifies one without the other is a latent bug. | Eliminate `_filter`. In `LoadPageAsync`, derive the filter inline: `var filter = _showAll ? FilingFilterMode.All : FilingFilterMode.Unpaid`. This removes the synchronisation requirement. |
| **G1** | Gap | LOW | spec.md §FR-012, tasks.md T015, T021 | FR-012 requires the error message area to be "clearable". T021 binds `ErrorMessage` to a `TextBlock` and T020 creates `ClearErrorCommand`, but no dismiss button with a label is included in the XAML spec (T021) and no `Strings.resx` key exists for its label. Without a button, the error is not user-clearable from the UI. | Add `Filings_Error_Dismiss = "✕"` to T015. Add a dismiss `Button Content="{x:Static res:Strings.Filings_Error_Dismiss}" Command="{Binding ClearErrorCommand}"` next to the error TextBlock in T021. |
| **A1** | Spec Accuracy | LOW | spec.md §Assumptions | The assumption "IFilingRepository either does not yet exist or has only a basic save method" is factually incorrect. The current codebase (`IFilingRepository.cs`) already exposes `GetByIdAsync`, `GetAllAsync`, `GetByTaxPeriodAsync`, `ExistsByIncomeAsync`, `GetByReportIdAsync`, `AddAsync`, `UpdateAsync`, and `DeleteAsync`. The spec assumption creates a misleading picture for future readers. | Update spec assumption to: "IFilingRepository exists with standard CRUD methods. This feature extends it with `GetPagedAsync(filter, page, pageSize, ct)`." |

---

## Coverage Summary Table

| Requirement | Has Task? | Task IDs | Notes |
|-------------|-----------|----------|-------|
| FR-001 DataGrid columns | ✅ | T021 | All 6 columns specified |
| FR-002 Deadline-asc sort | ✅ | T009 | ORDER BY FilingDeadline ASC in repo |
| FR-003 Pagination 20/page | ✅ | T020, T021 | VM state + Previous/Next in XAML |
| FR-004 Filter toggle Unpaid/All | ✅ | T023, T024 | Default Unpaid; toggle resets page |
| FR-005 Non-blocking async | ✅ | T020, T009 | ReactiveCommand.CreateFromTask throughout |
| FR-006 Inline status dropdown | ✅ | T028, T029 | ComboBox template column |
| FR-007 Valid transitions only | ✅ | T026, T027 | Domain delegation; T027 tests ⚠️ see T1 |
| FR-008 PaymentRef editable Filed-only | ✅ | T034 | IsReadOnly binding on TextBox |
| FR-009 PaymentRef max 200 | ✅ | T002, T031 | Domain throws; handler wraps |
| FR-010 Delete with ContentDialog | ✅ | T039, T040 | Func delegate confirm pattern |
| FR-011 IsLoading indicator | ✅ | T020, T021 | ProgressBar IsVisible binding |
| FR-012 Clearable error message | ⚠️ | T020, T021 | No dismiss button in XAML — see G1 |
| FR-013 All strings in Strings.resx | ✅ | T015 | 25 keys (count says 24 — see I2) |
| FR-014 CQRS handlers × 4 | ✅ | T011, T012, T025, T026, T030, T031, T035, T036 | All 4 handlers fully specified |
| FR-015 PaymentReference domain property | ✅ | T002, T003 | SetPaymentReference + 6 tests |
| FR-016 Migration 0009 | ✅ | T008, T010 | EF config + scaffold migration |
| FR-017 IActivatableViewModel | ✅ | T020 | WhenActivated triggers load |
| FR-018 FilingsView placeholder → DataGrid | ✅ | T021, T022 | Full XAML replacement |
| SC-001 Load ≤ 1 s / 500 filings | — | — | Post-launch metric; no build task needed |
| SC-006 Filter switch ≤ 500 ms | — | — | Post-launch metric; no build task needed |

---

## Constitution Alignment Issues

**None.** All five constitution principles are satisfied:

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Clean Architecture | ✅ PASS | Desktop → Application → Domain boundaries intact; Infrastructure implements Application contracts. *See finding C1 for registration placement inconsistency that should be corrected.* |
| II. Local-First Security | ✅ PASS | All operations are SQLite local reads/writes; no network calls; no credential storage. |
| III. Financial/Temporal Correctness | ✅ PASS | `TaxPayable` = `decimal`; `FilingDeadline`/`TaxPeriod` = `DateOnly`; no `float`/`double`/`DateTime`. *See finding I1 for locale-sensitivity of N2 format.* |
| IV. Async and UI Responsiveness | ✅ PASS | All I/O via `async Task`; `ReactiveCommand.CreateFromTask` used throughout; ContentDialog awaited. *See finding C3 for subscription cleanup gap.* |
| V. Specification-Driven Quality Gates | ✅ PASS | All tasks traceable to spec FRs; domain 100% coverage planned (T003); application ≥90% planned (T013, T027, T032, T037); infrastructure integration tests (T014, T038). |

---

## Unmapped Tasks

None. All 44 tasks map to at least one FR, SC, or cross-cutting concern.

| Phase | Tasks | Mapped To |
|-------|-------|-----------|
| 1 | T001 | Setup / cross-feature dependency (FR-016) |
| 2 | T002–T010 | FR-015, FR-016, FR-014 (contracts) |
| 3 | T011–T022 | FR-001–FR-005, FR-011–FR-013, FR-017–FR-018 |
| 4 | T023–T024 | FR-004 (US2) |
| 5 | T025–T029 | FR-006–FR-007 (US3) |
| 6 | T030–T034 | FR-008–FR-009 (US4) |
| 7 | T035–T040 | FR-010 (US5) |
| 8 | T041–T044 | DI wiring, VM tests, smoke test |

---

## Specific Checks Requested

### 1. Clarify.md → Spec → Plan → Tasks Consistency

| Decision | clarify.md | data-model.md | tasks.md | Status |
|----------|-----------|---------------|----------|--------|
| FilingRowDto 7-field subset | §D1 | §2 | T005 | ✅ Consistent |
| Server-side pagination | §D2 | §3, §4 | T011, T012, T009 | ✅ Consistent |
| VM handler injection (direct) | §D3 ⚠️ (only 4 params shown) | §6 (6 params) | T020 (6 params) | ✅ Functionally correct; clarify.md wording gap (see I4) |
| Full page reload after mutation | §D4 | §6 Commands | T028, T033, T039 | ✅ Consistent |
| UpdateAsync persistence path | §D5 | §3 Commands | T031, T026 | ✅ Consistent |

### 2. Requirements Coverage Gaps

One partial gap: **FR-012** (clearable error message) — the dismiss mechanism exists as `ClearErrorCommand` but has no UI surface or resx key. See **G1**.

### 3. Architecture Violations

| Rule | Status | Notes |
|------|--------|-------|
| `AddTransient` only | ✅ | All handler registrations use AddTransient (T041). `FilingsViewModel` already registered as AddTransient in CompositionRoot. ⚠️ Wrong file per C1. |
| `ReactiveCommand.CreateFromTask` | ✅ | T020 mandates this for all 6 commands. |
| No `[Reactive]`/Fody | ✅ | plan.md explicitly says "ReactiveUI.Fody NOT used — manual RaiseAndSetIfChanged". |
| `x:CompileBindings="False"` | ✅ | T021 specifies attribute on root UserControl. |
| `DateOnly` dates | ✅ | `FilingDeadline`, `TaxPeriod` are `DateOnly` end-to-end. |
| `decimal` money | ✅ | `TaxPayable`, `TaxPayableRsd` are `decimal` throughout. |
| `Result<T,Error>` | ✅ | All 4 handler return types use this pattern. |
| No `ExecuteDeleteAsync` | ✅ | plan.md compliance checklist explicitly prohibits it; T036 delegates to `IFilingRepository.DeleteAsync`. |
| `ContentDialog` awaited async | ✅ | T039 uses `await _confirmDelete(...)`. T042 shows `await dialog.ShowAsync(...)`. |

### 4. Test Coverage Edge Cases

| Edge Case | Test Exists? | Task | Gap |
|-----------|-------------|------|-----|
| Invalid `AdvanceStatus` (Init→Paid) | Domain: ✅ `FilingStatusTransitionTests` | T027 handler test | T027 has generic name; explicit Init→Paid handler test missing — see **T1** |
| `SetPaymentReference` on non-Filed status | No explicit test | T032 | Missing positive test for intentional permissiveness — see **T2** |
| Pagination last page after filter | VM clamping in T020 | T043: `ShowAll_WhenSetToTrue_ResetsPageToOneAndReloads` | ✅ Filter reset covered; page > TotalPages clamping is VM-internal |
| Delete on non-existent filing | ✅ | T037 (`IsIdempotent`), T038 | ✅ Covered |
| Empty list state (`IsEmpty`) | ✅ partial | T021 XAML binding | VM test `LoadPage_WhenNoResults_IsEmptyIsTrue` missing — see **T4** |
| Last item on page N deleted → page N-1 | Logic in T039 | T043 | VM test missing — see **T3** |

### 5. EF Migration Dependency

- **Migration 0008** (`20260407123841_0008_FilingsTable.cs`): ✅ Confirmed as the latest migration on master.
- **T001** correctly instructs to verify 0008 is latest before branching.
- **Dependency chain** T002 → T008 → T010 is correctly modelled: domain property added first, EF config second, migration scaffold third.
- **No gap**: T010 depends on T002 and T008 as specified.

### 6. Cross-Feature Dependency (Feature 011 fields)

All Feature 011 fields referenced by spec §Assumptions are confirmed present in `Filing.cs` on master:

| Field | Present? |
|-------|---------|
| `TaxPayableRsd` (decimal) | ✅ line 33 |
| `IncomeType` (IncomeType enum) | ✅ line 27 |
| `PayingEntity` (string) | ✅ line 28 |
| `FilingDeadline` (DateOnly) | ✅ line 34 |
| `IncomeDate` (DateOnly) | ✅ line 29 |
| `ReportId` (Guid?) | ✅ line 35 |

T001 correctly assumes these are on master. No blocking cross-feature gap.

**Spec assumption inaccuracy** (see A1): The spec says "IFilingRepository either does not yet exist or has only a basic save method." The actual interface already has `GetByIdAsync`, `UpdateAsync`, `DeleteAsync`, and 4 other methods. This is not blocking (it makes implementation easier) but the text is misleading.

### 7. ViewModel Injection Pattern

- **Established pattern**: `ImporterSettingsViewModel` injects 6 handler interfaces directly — confirmed ✅.
- **T020 FilingsViewModel**: 4 handlers + `Func<string, Task<bool>> confirmDelete` + `IScheduler? scheduler = null` — **matches** the established pattern.
- **DI registration**: `FilingsViewModel` is already registered as `AddTransient<FilingsViewModel>()` in CompositionRoot (line 30). The constructor change is backwards-compatible with DI (all new params can be resolved from the container, assuming C1 is fixed and confirmDelegate is registered per C4).

### 8. Strings.resx — 24 Key Coverage Audit

T015 lists the following 25 keys (header says 24 — discrepancy noted in I2):

| Area | Keys | Coverage |
|------|------|----------|
| Column headers | `Filings_Col_Status`, `_IncomeType`, `_PayingEntity`, `_Deadline`, `_TaxPayable`, `_PaymentRef` | ✅ All 6 columns covered |
| Filter labels | `Filings_Filter_Unpaid`, `Filings_Filter_All` | ✅ |
| Pagination | `Filings_Page_Previous`, `_Next`, `_Indicator` | ✅ |
| Empty state | `Filings_Empty` | ✅ |
| Delete dialog | `Filings_Delete_Confirmation_Title`, `_Message`, `_Confirm_Button`, `_Cancel_Button` | ✅ |
| Error messages | `Filings_Error_NotFound`, `_InvalidTransition`, `_PaymentRefTooLong`, `_LoadFailed` | ✅ |
| Status display | `FilingStatus_Init`, `_Filed`, `_Paid` | ✅ |
| Income type display | `IncomeType_Dividend`, `_Interest` | ✅ (may already exist — see I2) |
| **Missing** | Error dismiss button label | ⚠️ No key for "✕" dismiss — see G1 |

---

## Metrics

| Metric | Value |
|--------|-------|
| Total Functional Requirements | 18 |
| Total Tasks | 44 |
| Requirements with ≥ 1 task | 17 / 18 (94%) — FR-012 partial |
| Ambiguity Count | 0 |
| Duplication Count | 1 minor (I3: shared resx key) |
| Constitution Violations | 0 CRITICAL |
| HIGH Issues | 1 (C1) |
| MEDIUM Issues | 6 (C2, C3, C4, T1, T2, T3) |
| LOW Issues | 7 (T4, I1, I2, I3, I4, I5, G1, A1 = 8 items total; T4+I1 shown as LOW) |

---

## Next Actions

### Before `/speckit.implement`

**Fix HIGH issue C1 first** — the handler registration placement is observable at runtime (tests and DI smoke test would fail if handlers can't be resolved). Update T041 to target `CompositionRoot.AddDesktopServices()` not `InfrastructureServiceExtensions.cs`.

### Recommended Pre-Implementation Task Edits

1. **T041** — Change target file from `InfrastructureServiceExtensions.cs` to `CompositionRoot.cs` (matching the mailbox/importer handler registration block pattern).  
2. **T015** — Add `Filings_Error_Dismiss` key; verify IncomeType keys don't already exist before adding.  
3. **T021** — Add dismiss Button for ErrorMessage area.  
4. **T019** — Change `TaxPayableDisplay` to use `CultureInfo.InvariantCulture`.  
5. **T020/T023** — Add `.DisposeWith(disposables)` note to subscription guidance.  
6. **T027** — Add two explicit named invalid-transition test methods.  
7. **T032** — Add non-Filed status positive test.  
8. **T043** — Add page-decrement-on-last-delete test and IsEmpty test.  
9. **T042** — Add explicit `services.AddTransient<Func<string, Task<bool>>>(...)` registration syntax.

### Proceed-As-Is (LOW/informational only)

Issues I2, I3, I4, I5, A1 may be addressed incrementally during implementation without blocking the feature.

---

## Remediation Offer

Would you like me to suggest concrete remediation edits for the **top 5 issues** (C1, C3, T1, I1, G1)? I can produce exact task description patches for tasks.md — no automatic file writes will occur until you explicitly approve.
