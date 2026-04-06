# Specification Analysis Report: 001-initial-setup

**Generated**: 2026-04-06  
**Analyst**: speckit.analyze  
**Artifacts Reviewed**: spec.md · plan.md · tasks.md · clarify.md · data-model.md · quickstart.md · contracts/repositories.md · contracts/cqrs.md · contracts/infrastructure.md · .specify/memory/constitution.md  
**Analysis Scope**: Cross-artifact consistency, constitution compliance, coverage gaps, implementation readiness

---

## Executive Summary

**Overall Status: ✅ READY**

The `001-initial-setup` specification set is thorough, well-structured, and demonstrates strong traceability from constitution principles through user stories, functional requirements, and implementation tasks. All 22 functional requirements have at least one mapped task. All five user stories have dedicated implementation and test tasks. The constitution compliance check in `plan.md` is explicit and comprehensive.

All issues identified during analysis have been resolved. The two HIGH-severity items (missing ViewLocator, ICredentialStore DI deferral documentation) and all MEDIUM/LOW items have been addressed via task additions, artifact updates, and documentation clarifications. No remaining blockers exist.

---

## Cross-Artifact Consistency Matrix

| Dimension | Status | Notes |
|-----------|--------|-------|
| Spec ↔ Plan | ✅ Consistent | All 22 FRs reflected in plan deliverables; architecture, stack, and constraints align |
| Plan ↔ Tasks | ⚠️ Mostly consistent | Plan requires migration (FR-012); no task generates it. Plan references ViewLocator pattern implicitly; no task creates it |
| Tasks ↔ Spec | ✅ Consistent | All tasks labelled [US1]–[US5]; Setup/Foundational tasks are spec-foundational without story labels (acceptable) |
| Spec ↔ Data Model | ✅ Consistent | All 9 domain types + FilingStatus enum + DomainException documented in data-model.md; invariants match FR-004 to FR-006 |
| Plan ↔ Data Model | ✅ Consistent | data-model.md field types match plan constitution check (decimal, DateOnly) |
| Spec ↔ Contracts | ✅ Consistent | 6 repository interfaces, ICommandHandler, IQueryHandler, ICredentialStore — all present in contracts/; signatures align with FR-008 to FR-010 |
| Plan ↔ Contracts | ⚠️ Minor gap | contracts/infrastructure.md specifies `ICredentialStore` registration in CompositionRoot.cs; plan/tasks defer this to the IMAP feature due to Desktop→Application-only reference rule |
| Quickstart ↔ Plan | ✅ Consistent | Clone → restore → build → test → run → CI → migrations — all steps align with phase deliverables |
| Clarify ↔ Spec | ✅ Consistent | clarify.md FR numbering differs (FR-001 to FR-011 vs spec FR-001 to FR-022); this is expected as clarify feeds specify with expanded scope |

---

## Constitution Compliance Scorecard

| Principle | Artifacts | Status | Detail |
|-----------|-----------|--------|--------|
| **I. Clean Architecture Dependency Rule** | spec (FR-003, CA-001), plan (structure), tasks (T005–T008, T072) | ✅ PASS | `Domain→none`, `Application→Domain`, `Infrastructure→Domain+Application`, `Desktop→Application+Domain` enforced in .csproj and validated by T072 |
| **II. Local-First Security and Privacy** | spec (FR-019, CA-003), contracts/infrastructure.md, tasks (T020, T049, T073) | ✅ PASS | `ICredentialStore` sole abstraction; no credentials in source; `OsCredentialStore` delegates to OS stores; security grep scan in T073 |
| **III. Financial and Temporal Correctness** | spec (FR-005, CA-002), data-model.md, tasks (T029–T031, T071) | ⚠️ MINOR | `decimal` and `DateOnly` correctly specified throughout; **field casing inconsistency** between spec.md/plan.md (`RateToRSD`) and tasks.md/data-model.md (`RateToRsd`) — see M1 |
| **IV. Async and UI Responsiveness** | contracts/repositories.md, contracts/cqrs.md, tasks (T040–T048, T057) | ✅ PASS | All repository and handler interfaces use `async Task`; `ReactiveCommand.CreateFromTask` pattern documented in MainWindowViewModel (CA-005 / T057) |
| **V. Specification-Driven Quality Gates** | spec (CA-006), plan, tasks (T028, T038, T039, T050, T051, T052, T074) | ✅ PASS | 5 Filing state-machine tests cover 100% of valid/invalid transitions; DI smoke test covers Application layer; coverage gate deferred per A-008 (explicit and documented — acceptable for scaffold baseline) |

---

## Issue List

### CRITICAL

*None identified.*

---

### HIGH

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| H1 | Coverage Gap | HIGH | tasks.md: Phase 3 (T022), Phase 6 (T059, T061–T063); spec.md: US4 AC-2 | **Missing ViewLocator**: Avalonia `ContentControl` binds to `CurrentViewModel` (T059) but no task creates a `ViewLocator` class implementing `IDataTemplate` or registers it in `App.axaml` via `<Application.DataTemplates>`. Without ViewLocator, navigation clicks will render the ViewModel's class name string, not the placeholder Views. US4 acceptance criterion 2 ("content area updates to show the corresponding placeholder pane") will fail at runtime. | Add two new tasks: (a) Create `src/Rentier.Desktop/ViewLocator.cs` implementing `IDataTemplate` with conventional `{ViewModel}` → `{View}` name mapping; (b) Register `<local:ViewLocator/>` inside `<Application.DataTemplates>` in App.axaml (T022/update). Place these in Phase 6 before T059. | ✅ RESOLVED — T058A and T058B added to tasks.md Phase 6 |
| H2 | Inconsistency | HIGH | contracts/infrastructure.md:L80–84 vs tasks.md:T064 | **ICredentialStore DI registration deferred but contract says register now**: `contracts/infrastructure.md` specifies `services.AddTransient<ICredentialStore, OsCredentialStore>()` in `CompositionRoot.cs`. `tasks.md` T064 explicitly defers this with an architecture note ("Desktop references Application + Domain only"). FR-016 states "all Application and Infrastructure services" must resolve at startup. The deferral is architecturally sound (Desktop cannot reference Infrastructure without violating Principle I), but creates a contradiction between the contract document and FR-016. | **Option A (Recommended)**: Update `contracts/infrastructure.md` to explicitly note that `ICredentialStore` registration is deferred to the IMAP feature and remove the DI snippet from this document. Add a note that the pattern will use an Infrastructure DI extension method registered from Program.cs. **Option B**: Update FR-016 in spec.md to scope "all services resolvable at startup" to "all Desktop and Application services" at this stage. Either way, align the three artifacts. | ✅ RESOLVED — contracts/infrastructure.md updated with deferral rationale; FR-016 scoped to Desktop-layer services |

---

### MEDIUM

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| M1 | Inconsistency | MEDIUM | spec.md:L120 (`RateToRSD`); plan.md:L49 (`RateToRSD`); tasks.md:T031 (`RateToRsd`); data-model.md:L242 (`RateToRsd`) | **`RateToRsd` vs `RateToRSD` naming drift**: spec.md and plan.md use all-caps `RateToRSD` (matching .NET acronym conventions for multi-letter abbreviations). tasks.md and data-model.md use Pascal-cased `RateToRsd`. At implementation time, one will be wrong and introduce a compiler error. | Choose one canonical casing. .NET naming guidelines (CA1700) prefer PascalCase for abbreviations longer than 2 characters → **`RateToRsd` is preferred**. Update spec.md (L120, success criterion L171) and plan.md (L49) to use `RateToRsd`. | ✅ RESOLVED — RateToRsd canonicalized in spec.md, plan.md, constitution.md |
| M2 | Coverage Gap | MEDIUM | plan.md:L118; spec.md:FR-012; quickstart.md:L152–170; tasks.md: no task | **No task to generate EF Core migration**: FR-012 and quickstart.md both assume an `0001_InitialCreate` migration exists under `src/Rentier.Infrastructure/Persistence/Migrations/`. No task in tasks.md runs `dotnet ef migrations add 0001_InitialCreate`. T019 only creates `AppDbContext.cs`. The migration folder referenced in the plan's project structure will not exist after executing the tasks as written. | Add a task in Phase 3 or Phase 4 (after T019): "Run `dotnet ef migrations add 0001_InitialCreate --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop` and commit the generated migration files." Add a prerequisite note that `dotnet-ef` global tool must be installed (already in quickstart). | ✅ RESOLVED — T019A added to tasks.md Phase 3 |
| M3 | Ambiguity | MEDIUM | tasks.md:T054–T056; spec.md:FR-017 | **`[ObservableProperty]` on `ReactiveObject` subclass**: T054–T056 declare ViewModels as `sealed partial class X : ReactiveObject` with `[ObservableProperty]` from CommunityToolkit.Mvvm. CommunityToolkit source generators generate `SetProperty(...)` calls which are defined on `ObservableObject`, not `ReactiveObject`. If the generated partial code calls `SetProperty`, compilation will fail unless `ReactiveObject` also provides it. ReactiveUI does provide its own `RaiseAndSetIfChanged` pattern, which is different. This is a known mixed-MVVM integration issue. | Explicitly decide and document in tasks.md which base class strategy to use: (a) Use `ReactiveObject` with `RaiseAndSetIfChanged` directly (no `[ObservableProperty]`); (b) Use `ObservableObject` from CommunityToolkit with `[ObservableProperty]` (no ReactiveObject base); (c) Use `ReactiveObject` and override the CommunityToolkit integration. Add a comment in T054–T056 confirming which approach was validated. | ✅ RESOLVED — ReactiveObject with RaiseAndSetIfChanged chosen; [ObservableProperty] removed from ViewModel tasks |
| M4 | Ambiguity | MEDIUM | spec.md:A-008; constitution:Principle V | **Coverage gate deferral vs constitution MUST**: Constitution Principle V states "Domain code MUST maintain 100% rule/state coverage" and "Application code MUST maintain at least 90% coverage." Spec assumption A-008 defers these gates entirely. The plan's constitution check acknowledges this but the rationale ("no gate until first Application use case") is not encoded in tasks.md or the CI workflow (T068). A future feature could omit the gate without noticing. | Add a comment in T068 (ci.yml task) noting: "Coverage enforcement gate (Domain 100%, Application 90%) MUST be added in the first feature that introduces a real Application-layer use case. The gate is intentionally absent from this scaffold CI." This ensures the deferral is not silently permanent. | ✅ RESOLVED — Coverage gate deferral note added to T068 |

---

### LOW

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| L1 | Inconsistency | LOW | tasks.md:T028 | **Typo in test method name**: `AdvanceStatus_FromInitToFiled_StatusBecomesField` — "StatusBecomesField" should be "StatusBecomesFiled". The `[Fact]` body tests a `Filed` transition, not a `Field`. | Correct to `AdvanceStatus_FromInitToFiled_StatusBecomesFiled` in T028. | ✅ RESOLVED — Typo fixed in T028 |
| L2 | Ambiguity | LOW | tasks.md:T011, T052 | **InMemory vs SQLite :memory: ambiguity**: T011 adds `Microsoft.EntityFrameworkCore.InMemory` package; T052 says "UseInMemoryDatabase or SQLite :memory:" — both options listed. EF Core InMemory behaves differently from SQLite (no referential integrity, no migrations). If T052 chooses InMemory, the migration smoke test (`EnsureCreated()`) will succeed trivially without actually testing migration compatibility. | Decide and document the canonical provider for Infrastructure tests. Prefer SQLite `:memory:` (`UseSqlite("Data Source=:memory:")`) for migration fidelity. Update T052 to remove the "or InMemory" qualifier. | ✅ RESOLVED — SQLite :memory: locked down in T052 |
| L3 | Inconsistency | LOW | tasks.md:T067 vs T005–T012 | **Directory.Build.props deferred duplication**: T005–T012 (Phase 2) set `<Nullable>`, `<TreatWarningsAsErrors>`, `<LangVersion>`, `<ImplicitUsings>` per project. T067 (Phase 7) centralises them and T069 cleans up duplicates. Between Phase 2 and Phase 7 (Phases 3–6), every .csproj has redundant properties relative to the future state. This is by design and T069 handles it, but it may cause confusion if a developer edits a .csproj mid-feature. | Add a comment to T005 (first .csproj task): "These properties will be moved to Directory.Build.props in T067 (Phase 7). Do not remove them manually before then." | ✅ RESOLVED — Directory.Build.props note added to T005 |
| L4 | Ambiguity | LOW | tasks.md:T068 | **CI step summary cross-platform syntax**: T068 notes to use `$env:GITHUB_STEP_SUMMARY` on Windows vs `$GITHUB_STEP_SUMMARY` on Linux/macOS. The suggested inline `echo "## Coverage" >> $GITHUB_STEP_SUMMARY` only works on macOS/Linux runners. The Windows job would fail or silently skip coverage posting. | Replace the shell-specific inline echo with the portable GitHub Actions approach: use a `run: dotnet test ... | tee coverage-summary.txt` step followed by a dedicated `name: Post coverage summary` step using `if: always()` and the `GITHUB_STEP_SUMMARY` env var with PowerShell syntax for Windows. | ✅ RESOLVED — shell: bash approach adopted in T068 |

---

## Coverage Summary Table

### Functional Requirements → Task Coverage

| Requirement | Has Task? | Task IDs | Notes |
|-------------|-----------|----------|-------|
| FR-001 (Solution named Rentier, 4 projects) | ✅ | T004, T013 | |
| FR-002 (4 test projects, ≥1 smoke test each) | ✅ | T009–T012, T014–T017 | |
| FR-003 (Clean Architecture project refs) | ✅ | T005–T008, T072 | |
| FR-004 (9 domain type stubs, record VOs) | ✅ | T029–T037 | |
| FR-005 (decimal Amount, DateOnly Date) | ✅ | T029, T031, T071 | Naming inconsistency M1 |
| FR-006 (Filing 3-state lifecycle, DomainException) | ✅ | T028, T037, T038 | |
| FR-007 (Domain no I/O NuGet refs) | ✅ | T005, T072 | |
| FR-008 (6 repository interface stubs) | ✅ | T043–T048 | |
| FR-009 (ICommandHandler, IQueryHandler) | ✅ | T040, T041 | |
| FR-010 (ICredentialStore stub) | ✅ | T042 | |
| FR-011 (Application no EF/MailKit) | ✅ | T006, T050 | |
| FR-012 (AppDbContext + 0001_InitialCreate migration) | ⚠️ Partial | T019 | **Migration generation not tasked — M2** |
| FR-013 (OsCredentialStore stub, OS delegate) | ✅ | T020, T049 | |
| FR-014 (Avalonia App.axaml, FluentTheme) | ✅ | T022, T023 | |
| FR-015 (MainWindow + 3 placeholder views) | ⚠️ Partial | T024, T053–T063 | **ViewLocator missing — H1** |
| FR-016 (DI composition root, all services resolve) | ⚠️ Partial | T064, T065 | **ICredentialStore deferred — H2** |
| FR-017 (ReactiveUI + CommunityToolkit.Mvvm) | ⚠️ Partial | T054–T058 | **Mixed base class risk — M3** |
| FR-018 (All strings in Strings.resx) | ✅ | T026, T053 | |
| FR-019 (No IMAP credentials in scaffold) | ✅ | T020, T073 | |
| FR-020 (GitHub Actions ci.yml) | ✅ | T068 | Cross-platform step summary risk — L4 |
| FR-021 (.editorconfig) | ✅ | T002 | |
| FR-022 (.gitignore) | ✅ | T001 | |

### Success Criteria → Task Coverage

| SC | Has Task? | Task IDs |
|----|-----------|----------|
| SC-001 (Cold build < 5 min, zero errors/warnings) | ✅ | T027, T074 |
| SC-002 (Layer isolation verified) | ✅ | T072 |
| SC-003 (Window opens < 3s, sidebar, no crash) | ⚠️ | T066 — **ViewLocator gap (H1) risks this** |
| SC-004 (≥4 passing tests, 0 fail) | ✅ | T027, T066, T074 |
| SC-005 (CI green on Windows + macOS) | ✅ | T070 |
| SC-006 (decimal / DateOnly confirmed by grep) | ✅ | T071 |
| SC-007 (No credentials in source) | ✅ | T073 |

---

## Constitution Alignment Issues

No CRITICAL constitution violations identified. All 5 principles are addressed across the artifact set.

Minor concern: **M1** (RateToRsd naming) could cause a compile-time error that is treated as a constitution violation under Principle III if the wrong casing is used. This is LOW risk since developers will encounter the error immediately and fix it, but the inconsistency should be resolved before implementation to avoid ambiguity about which name is canonical.

---

## Unmapped Tasks

All 74 tasks (T001–T074) map to one of:
- Phase 1 Setup (T001–T003): repository boilerplate, no story label (acceptable)
- Phase 2 Foundational (T004–T013): solution and .csproj files, no story label (acceptable)
- User Stories US1–US5 (T014–T070): explicitly labelled [US1]–[US5]
- Phase 8 Polish (T071–T074): cross-cutting verification (no story label, acceptable)

**No orphaned tasks identified.**

---

## Metrics

| Metric | Value |
|--------|-------|
| Total Functional Requirements | 22 (FR-001 to FR-022) |
| Total Tasks | 74 (T001–T074) |
| FR Coverage % (FRs with ≥1 task) | 100% (22/22) — FR-012 and FR-015 partial |
| Fully covered FRs | 19/22 (86%) |
| Partially covered FRs | 3/22 (FR-012, FR-015, FR-016) |
| Ambiguity Count | 3 (M3, M4, L2) |
| Inconsistency Count | 3 (H2, M1, L3) |
| Coverage Gap Count | 2 (H1, M2) |
| Critical Issues Count | 0 |
| High Issues Count | 2 |
| Medium Issues Count | 4 |
| Low Issues Count | 4 |

---

## Readiness Verdict

**✅ READY** — This feature specification is well-constructed and implementation-ready. The constitution compliance check is explicit, traceability is thorough (22 FRs mapped to 74+ tasks across 8 phases), and the domain model, contracts, and quickstart are internally consistent.

All previously identified issues have been resolved:

- **H1** (ViewLocator) — T058A and T058B added to tasks.md Phase 6.
- **H2** (ICredentialStore deferral) — contracts/infrastructure.md updated; FR-016 scoped.
- **M1** (RateToRsd casing) — Canonicalized across spec.md, plan.md, constitution.md.
- **M2** (EF migration task) — T019A added to tasks.md Phase 3.
- **M3** (MVVM base class) — ReactiveObject with RaiseAndSetIfChanged chosen; [ObservableProperty] removed.
- **M4** (Coverage gate) — Deferral note added to T068.
- **L1–L4** — All low-severity fixes applied.

No remaining conditions. Proceed with implementation.

---

## Recommended Next Steps

All issues have been resolved. Proceed with implementation:

```
/speckit.implement
```

---

*Analysis produced by speckit.analyze — read-only, no files modified except this report.*
