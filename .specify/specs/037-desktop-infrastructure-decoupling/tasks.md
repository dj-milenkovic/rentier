# Tasks: Desktop–Infrastructure Decoupling

**Input**: `.specify/specs/037-desktop-infrastructure-decoupling/`
**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ data-model.md ✅ quickstart.md ✅

**Tests**: Architectural fitness test (xUnit) included per CA-006. No behavioral logic to unit-test — this is a pure structural refactoring. Existing `DiRegistrationSmokeTests` serve as regression guard for service registration.

**Organization**: Tasks are grouped by user story. **Note on phase order**: US3 (P3) is implemented before US1 (P1) and US2 (P2) because it introduces the indirection mechanism that makes US1's compile-time removal possible. Implementing US1 first would break the build. The phase order reflects execution sequence; priority labels ([US1], [US2], [US3]) preserve spec traceability.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies within the same phase)
- **[Story]**: Which user story this task belongs to (US1 = P1, US2 = P2, US3 = P3)

---

## Phase 1: Setup

**Purpose**: Add the one missing package that Application needs to define `IInfrastructureRegistrar` with an `IServiceCollection` parameter.

- [X] T001 Add `<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.*" />` to `src/Rentier.Application/Rentier.Application.csproj`

**Checkpoint**: `dotnet build src/Rentier.Application` passes before proceeding.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Define Application-layer contracts that Infrastructure implements and Desktop resolves. These interfaces are the shared vocabulary that decouples the two layers — every subsequent phase depends on them existing.

**⚠️ CRITICAL**: No user-story work can begin until these two interfaces compile successfully.

- [X] T002 [P] Create `IInfrastructureRegistrar` in `src/Rentier.Application/Interfaces/IInfrastructureRegistrar.cs` with single method `Task RegisterServicesAsync(IServiceCollection services, string dbPath)` and XML doc comments as specified in data-model.md
- [X] T003 [P] Create `IDatabaseInitializer` in `src/Rentier.Application/Interfaces/IDatabaseInitializer.cs` with single method `Task InitializeAsync(CancellationToken ct = default)` and XML doc comments as specified in data-model.md

**Checkpoint**: `dotnet build src/Rentier.Application` compiles both new interfaces with zero errors. All user-story phases may now proceed.

---

## Phase 3: User Story 3 — Wire Infrastructure via Indirection (Priority: P3) 🔑 Enabler

**Goal**: Create the Infrastructure implementations of the two Application contracts and rewrite the Desktop startup sequence to use reflection-based discovery instead of direct Infrastructure calls. This phase is the technical prerequisite for US1's ProjectReference removal.

**Why implemented first**: US3 introduces the mechanism that makes the compile-time boundary possible. Removing the `ProjectReference` (US1) without this wiring in place would break runtime assembly resolution.

**Independent Test**: Start the application after Phase 3 completes (while the Infrastructure `ProjectReference` is still present). Confirm all services resolve, migrations run, and all user workflows (filings, sync, settings, dashboard) work identically to pre-refactoring behavior.

### Implementation for User Story 3

- [X] T004 [P] [US3] Create `InfrastructureRegistrar` class in `src/Rentier.Infrastructure/InfrastructureRegistrar.cs` — `sealed` class implementing `IInfrastructureRegistrar`, delegating to `AddInfrastructureServicesAsync(dbPath)` and registering `IDatabaseInitializer → DatabaseInitializer` as `Transient`. See data-model.md for exact code.
- [X] T005 [P] [US3] Create `DatabaseInitializer` class in `src/Rentier.Infrastructure/DatabaseInitializer.cs` — `sealed` class implementing `IDatabaseInitializer`, constructor-injecting `AppDbContext`, calling `_db.Database.MigrateAsync(ct)` in `InitializeAsync`. See data-model.md for exact code.
- [X] T006 [US3] Rewrite the Infrastructure-wiring block in `src/Rentier.Desktop/App.axaml.cs` — replace the direct `await services.AddInfrastructureServicesAsync(dbPath)` call and the direct `provider.GetRequiredService<AppDbContext>()` + `MigrateAsync()` block with the reflection-based startup flow from plan.md (Assembly.Load → scan for `IInfrastructureRegistrar` → `RegisterServicesAsync` → `IDatabaseInitializer.InitializeAsync`). Remove `using Rentier.Infrastructure;`, `using Rentier.Infrastructure.Persistence;`, and `using Microsoft.EntityFrameworkCore;` directives. Add fail-fast `InvalidOperationException` if assembly or implementation is missing.

**Checkpoint**: Build and run the full application. All existing user workflows (viewing filings, syncing mailbox, managing taxpayer profile, exporting XML, configuring settings, viewing dashboard) work identically to pre-refactoring behavior. The Infrastructure `ProjectReference` is still present in Desktop.csproj at this point — that is expected and correct.

---

## Phase 4: User Story 2 — Consolidate Infrastructure Service Registration (Priority: P2)

**Goal**: Move the misplaced `UserPreferenceRepository` registration from Desktop's `CompositionRoot.cs` into Infrastructure's `InfrastructureServiceExtensions.cs`, making the Infrastructure extension method the single source of truth for all repository registrations.

**Independent Test**: After Phase 4, inspect `src/Rentier.Desktop/Composition/CompositionRoot.cs` — zero references to any `Rentier.Infrastructure` type. Inspect `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` — `IUserPreferenceRepository` → `UserPreferenceRepository` registration is present. Run `dotnet test` — existing `DiRegistrationSmokeTests` pass, confirming `IUserPreferenceRepository` still resolves correctly.

### Implementation for User Story 2

- [X] T007 [US2] Add `services.AddTransient<IUserPreferenceRepository, UserPreferenceRepository>();` to `AddInfrastructureServicesAsync` in `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` — insert after the `IFilingRepository` line to keep repositories grouped together (see data-model.md diff)
- [X] T008 [US2] Remove the `IUserPreferenceRepository` registration line and the `using Rentier.Infrastructure.Repositories;` directive from `src/Rentier.Desktop/Composition/CompositionRoot.cs`

**Checkpoint**: `dotnet build` passes. `dotnet test` — all existing `DiRegistrationSmokeTests` pass with zero failures. CompositionRoot.cs contains zero `Rentier.Infrastructure` references.

---

## Phase 5: User Story 1 — Remove Desktop Compile-Time Dependency on Infrastructure (Priority: P1) 🎯 MVP Completion

**Goal**: Remove the `ProjectReference` and `Microsoft.EntityFrameworkCore.Design` package from `Rentier.Desktop.csproj`, completing the architectural decoupling at the compiler level. After this phase, the Clean Architecture dependency rule is fully enforced: Desktop → Application + Domain only.

**Independent Test**: Build `src/Rentier.Desktop/Rentier.Desktop.csproj` in isolation. Zero build errors. Text-search all Desktop source files for `Rentier.Infrastructure` — zero matches. The project compiles successfully with no Infrastructure project loaded.

### Implementation for User Story 1

- [X] T009 [US1] Remove `<ProjectReference Include="..\Rentier.Infrastructure\Rentier.Infrastructure.csproj" />` and the `<PackageReference Include="Microsoft.EntityFrameworkCore.Design" .../>` block from `src/Rentier.Desktop/Rentier.Desktop.csproj`
- [X] T010 [US1] Verify `dotnet build src/Rentier.Desktop/Rentier.Desktop.csproj` compiles with zero errors after the ProjectReference removal — if any residual Infrastructure type references remain in Desktop source files, fix them now
- [X] T011 [US1] Run a text search across `src/Rentier.Desktop/` for the string `Rentier.Infrastructure` — confirm zero matches (SC-002 success criterion)

**Checkpoint**: Desktop compiles in isolation. Zero Infrastructure namespace references in any Desktop source file. Clean Architecture dependency rule is now enforced at the compiler level.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Architectural fitness test to prevent regression; runtime and migration verification; full CI validation.

- [X] T012 [P] Create architectural fitness test in `tests/Rentier.UnitTests/Architecture/LayerDependencyTests.cs` — xUnit `[Fact]` using `typeof(Rentier.Desktop.App).Assembly.GetReferencedAssemblies()` to assert no referenced assembly named `"Rentier.Infrastructure"` exists. Use FluentAssertions. See quickstart.md for exact test code.
- [X] T013 [P] Verify `dotnet ef` migration commands still work targeting Infrastructure: run `dotnet ef migrations list --project src/Rentier.Infrastructure --startup-project src/Rentier.Infrastructure` and confirm existing migrations are listed without errors
- [X] T014 Run the full solution build: `dotnet build` from repository root — zero errors, zero warnings introduced by this feature
- [X] T015 Run all tests: `dotnet test` — all existing tests pass (SC-003); new `LayerDependencyTests` passes
- [ ] T016 Run the application and exercise all user workflows: view filings, sync mailbox, view dashboard, manage taxpayer profile, manage mailboxes, export XML, configure settings, change language — confirm identical behavior to pre-refactoring (SC-004, FR-008)
- [X] T017 Run quickstart.md validation — confirm the post-refactoring developer onboarding guide is accurate for `dotnet ef` migration commands

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user-story phases
- **US3 (Phase 3)**: Depends on Phase 2 — implement first; provides the indirection mechanism
- **US2 (Phase 4)**: Depends on Phase 2 — can begin once interfaces exist; safe to run in parallel with Phase 3
- **US1 (Phase 5)**: Depends on Phase 3 (T006 must be complete) and Phase 4 (T008 must be complete) — ProjectReference removal only compiles cleanly when both are done
- **Polish (Phase 6)**: Depends on Phase 5 — run after all user-story phases are complete

### User Story Dependencies (Implementation Order)

```
Phase 1 (Setup)
    └─► Phase 2 (Foundational — Application interfaces)
            ├─► Phase 3 (US3 — InfrastructureRegistrar + DatabaseInitializer + App.axaml.cs rewrite)
            │       └─► Phase 5 (US1 — ProjectReference removal)
            └─► Phase 4 (US2 — consolidate UserPreferenceRepository)
                    └─► Phase 5 (US1 — ProjectReference removal)
```

**Key constraint**: Phase 5 (US1 completion) requires both Phase 3 AND Phase 4 to be complete. Removing the `ProjectReference` before T006 (App.axaml.cs rewrite) causes a runtime failure; removing it before T008 (CompositionRoot cleanup) causes a compile error.

### Within Each Phase

- [P] tasks within a phase operate on different files — safe to parallelize
- Non-[P] tasks depend on earlier tasks in the same phase
- All phases within US3 build on T002/T003 (Application interfaces)

### Parallel Opportunities

```bash
# Phase 2: Both interfaces are independent files
Task T002: Create IInfrastructureRegistrar.cs
Task T003: Create IDatabaseInitializer.cs

# Phase 3: Both Infrastructure classes are independent files
Task T004: Create InfrastructureRegistrar.cs
Task T005: Create DatabaseInitializer.cs

# Phase 6 (Polish): Fitness test and migration check are independent
Task T012: Create LayerDependencyTests.cs
Task T013: Verify dotnet ef migrations list
```

---

## Parallel Example: Phase 2 (Foundational)

```bash
# Launch both interface tasks together — different files, no dependencies between them:
Task: "Create IInfrastructureRegistrar in src/Rentier.Application/Interfaces/IInfrastructureRegistrar.cs"
Task: "Create IDatabaseInitializer in src/Rentier.Application/Interfaces/IDatabaseInitializer.cs"
```

---

## Implementation Strategy

### MVP — Restore Clean Architecture (All 3 Stories Required)

Unlike typical feature delivery where US1 alone delivers value, this refactoring requires all three stories to achieve a valid state:

1. Complete Phase 1 + Phase 2 (Setup + Foundational)
2. Complete Phase 3 (US3 — Indirection mechanism)
3. Complete Phase 4 (US2 — Registration consolidation)
4. Complete Phase 5 (US1 — Remove ProjectReference)
5. **VALIDATE**: Build compiles, app runs, all workflows pass
6. Complete Phase 6 (Polish — Fitness test, CI validation)

### Incremental Checkpoints

- After Phase 2: Both interfaces compile → foundation ready
- After Phase 3: App works with reflection-based wiring (Infrastructure ref still present) → mechanism validated
- After Phase 4: UserPreferenceRepository consolidated → single registration source confirmed
- After Phase 5: Desktop compiles without Infrastructure reference → architectural violation resolved
- After Phase 6: CI guard in place → regression impossible

### File Change Summary

| File | Action | Phase |
|------|--------|-------|
| `src/Rentier.Application/Rentier.Application.csproj` | Edit — add DI Abstractions package | Phase 1 |
| `src/Rentier.Application/Interfaces/IInfrastructureRegistrar.cs` | New | Phase 2 |
| `src/Rentier.Application/Interfaces/IDatabaseInitializer.cs` | New | Phase 2 |
| `src/Rentier.Infrastructure/InfrastructureRegistrar.cs` | New | Phase 3 (US3) |
| `src/Rentier.Infrastructure/DatabaseInitializer.cs` | New | Phase 3 (US3) |
| `src/Rentier.Desktop/App.axaml.cs` | Edit — reflection wiring + remove Infrastructure usings | Phase 3 (US3) |
| `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` | Edit — add UserPreferenceRepository | Phase 4 (US2) |
| `src/Rentier.Desktop/Composition/CompositionRoot.cs` | Edit — remove UserPreferenceRepository + Infrastructure using | Phase 4 (US2) |
| `src/Rentier.Desktop/Rentier.Desktop.csproj` | Edit — remove Infrastructure ProjectReference + EF Design | Phase 5 (US1) |
| `tests/Rentier.UnitTests/Architecture/LayerDependencyTests.cs` | New — architectural fitness test | Phase 6 |

---

## Notes

- [P] tasks = different files, no inter-task dependencies within the same phase
- [Story] label maps to spec.md user stories: [US1] = P1, [US2] = P2, [US3] = P3
- Phase ordering intentionally differs from priority order — US3 is the technical foundation that enables US1
- Zero behavioral changes throughout — all modifications are structural only (FR-008)
- `DiRegistrationSmokeTests` serve as the regression guard for service registration correctness
- After Phase 5, `dotnet ef` commands must use `--startup-project src/Rentier.Infrastructure` (see quickstart.md)
- Architectural fitness test (T012) prevents future re-introduction of the prohibited dependency in CI
