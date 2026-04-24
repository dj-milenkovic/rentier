# Implementation Plan: Desktop–Infrastructure Decoupling

**Branch**: `037-desktop-infrastructure-decoupling` | **Date**: 2026-04-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `.specify/specs/037-desktop-infrastructure-decoupling/spec.md`

## Summary

Remove the compile-time `ProjectReference` from `Rentier.Desktop` to `Rentier.Infrastructure` and replace it with a runtime indirection mechanism. The approach introduces two Application-layer abstractions — `IInfrastructureRegistrar` (service registration delegate) and `IDatabaseInitializer` (migration abstraction) — implemented in Infrastructure and discovered by Desktop via reflection at startup. The misplaced `UserPreferenceRepository` registration is consolidated into Infrastructure's existing `AddInfrastructureServicesAsync` entry point.

## Technical Context

**Language/Version**: C# 12 / .NET 10
**Primary Dependencies**: Avalonia UI 11, ReactiveUI, Microsoft.Extensions.DependencyInjection, EF Core 10 (SQLite)
**Storage**: SQLite via EF Core (local-first, file-based)
**Testing**: xUnit + FluentAssertions + NSubstitute
**Target Platform**: Windows and macOS desktop (cross-platform via Avalonia)
**Project Type**: Desktop application (Clean Architecture, 4-project solution)
**Performance Goals**: Startup ≤ 3 seconds (no measurable regression from reflection-based discovery)
**Constraints**: Zero behavioral changes; local-first; no new NuGet packages beyond DI abstractions
**Scale/Scope**: 4 source projects, 5 test projects; 2 files to modify in Desktop, 2 new interfaces in Application, 1 new class + 1 modified class in Infrastructure

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - **This feature directly restores compliance.** After implementation, Desktop.csproj references only Application + Domain. Infrastructure implements the new `IInfrastructureRegistrar` and `IDatabaseInitializer` contracts defined in Application.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - No monetary values introduced or modified. Structural refactoring only.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - No date fields introduced or modified.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - Credential store registration remains in Infrastructure's `AddInfrastructureServicesAsync`. No changes to security boundaries.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - No network changes. IMAP and NBS registrations stay in Infrastructure.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - `IInfrastructureRegistrar.RegisterServicesAsync` and `IDatabaseInitializer.InitializeAsync` are both async. No blocking calls introduced.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - Domain: unaffected. Application: new interfaces have no logic to test. Infrastructure: existing extension method gains one additional registration (UserPreferenceRepository). Desktop/Architecture: new fitness test validates no Infrastructure reference. Scenario tests: existing ScenarioFixture may gain `IDatabaseInitializer` usage.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - Will be mapped when tasks.md is generated.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/037-desktop-infrastructure-decoupling/
├── plan.md              # This file
├── research.md          # Phase 0 output — design decisions and rationale
├── data-model.md        # Phase 1 output — new interfaces and contracts
├── quickstart.md        # Phase 1 output — developer onboarding guide
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Rentier.Application/
│   ├── Interfaces/
│   │   ├── IInfrastructureRegistrar.cs    # NEW — service registration contract
│   │   └── IDatabaseInitializer.cs        # NEW — migration abstraction
│   └── Rentier.Application.csproj         # MODIFIED — add DI Abstractions package
│
├── Rentier.Infrastructure/
│   ├── InfrastructureRegistrar.cs         # NEW — implements IInfrastructureRegistrar
│   ├── DatabaseInitializer.cs             # NEW — implements IDatabaseInitializer
│   └── InfrastructureServiceExtensions.cs # MODIFIED — add UserPreferenceRepository
│
├── Rentier.Desktop/
│   ├── App.axaml.cs                       # MODIFIED — replace direct Infrastructure calls
│   ├── Composition/CompositionRoot.cs     # MODIFIED — remove Infrastructure import + registration
│   └── Rentier.Desktop.csproj             # MODIFIED — remove Infrastructure ProjectReference
│
└── Rentier.Domain/                        # UNMODIFIED

tests/
├── Rentier.UnitTests/
│   └── Architecture/
│       └── LayerDependencyTests.cs        # NEW — architectural fitness test
└── [other test projects unchanged]
```

**Structure Decision**: The existing 4-project Clean Architecture structure is preserved. No new projects are introduced. Changes are scoped to Application (2 new interfaces, 1 csproj edit), Infrastructure (2 new classes, 1 modified class), Desktop (3 file edits), and a new architectural test.

## Complexity Tracking

> No Constitution violations requiring justification.

| Decision | Rationale | Alternative Rejected |
|----------|-----------|---------------------|
| Add `Microsoft.Extensions.DependencyInjection.Abstractions` to Application | Required for `IInfrastructureRegistrar` to accept `IServiceCollection` parameter. This is an abstractions-only package with no I/O, widely accepted in Clean Architecture Application layers. | Defining a custom service-descriptor model: excessive complexity for no architectural benefit. |
| Reflection-based discovery in Desktop | Standard .NET pattern (`Assembly.Load` + interface scanning) for runtime-only coupling. No third-party IoC container needed. | Compile-time delegate: still requires a shared reference. Host-builder pattern: adds `Microsoft.Extensions.Hosting` dependency, overkill for desktop app. |

---

## Design: Indirection Mechanism

### Pattern: Application-Layer Interface + Reflection Discovery

```text
┌────────────────────┐     defines      ┌─────────────────────────────┐
│  Rentier.Application│ ──────────────►  │ IInfrastructureRegistrar    │
│                     │                  │ IDatabaseInitializer        │
└────────────────────┘                  └─────────────────────────────┘
         ▲                                          ▲
         │ compile-time                             │ implements (compile-time)
         │                                          │
┌────────────────────┐                  ┌─────────────────────────────┐
│  Rentier.Desktop   │                  │ Rentier.Infrastructure      │
│  (App.axaml.cs)    │ ─── runtime ──►  │ InfrastructureRegistrar     │
│                    │   Assembly.Load   │ DatabaseInitializer         │
└────────────────────┘                  └─────────────────────────────┘
```

### Startup Flow (After Refactoring)

```text
1. App.OnFrameworkInitializationCompleted()
2.   → Compute dbPath
3.   → Assembly.Load("Rentier.Infrastructure")
4.   → Scan for IInfrastructureRegistrar implementation
5.   → registrar.RegisterServicesAsync(services, dbPath)
6.   → services.AddDesktopServices()
7.   → provider = services.BuildServiceProvider()
8.   → provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync()
9.   → Continue with seeding, theme, localization, main window
```

### Fail-Fast Behavior

If `Rentier.Infrastructure.dll` is missing at runtime or contains no `IInfrastructureRegistrar` implementation, Desktop throws a descriptive `InvalidOperationException` at step 3–4 before the DI container is built. The existing error-window catch block in `App.axaml.cs` displays this to the user (FR-009).

---

## Changes by Layer

### 1. Application Layer (2 new files, 1 csproj edit)

**New**: `IInfrastructureRegistrar` in `Interfaces/`
- Single method: `Task RegisterServicesAsync(IServiceCollection services, string dbPath)`
- Purpose: abstracts the Infrastructure service registration behind an Application-layer contract

**New**: `IDatabaseInitializer` in `Interfaces/`
- Single method: `Task InitializeAsync(CancellationToken ct = default)`
- Purpose: abstracts EF Core `MigrateAsync()` so Desktop never references `AppDbContext` or `Microsoft.EntityFrameworkCore`

**Modified**: `Rentier.Application.csproj`
- Add `<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.*" />`
- Required for `IServiceCollection` parameter type in `IInfrastructureRegistrar`

### 2. Infrastructure Layer (2 new files, 1 modified file)

**New**: `InfrastructureRegistrar : IInfrastructureRegistrar`
- Wraps the existing `InfrastructureServiceExtensions.AddInfrastructureServicesAsync` call
- Also registers `IDatabaseInitializer` → `DatabaseInitializer` in the container

**New**: `DatabaseInitializer : IDatabaseInitializer`
- Constructor-injected `AppDbContext`
- `InitializeAsync()` calls `_db.Database.MigrateAsync(ct)`

**Modified**: `InfrastructureServiceExtensions.AddInfrastructureServicesAsync`
- Add: `services.AddTransient<IUserPreferenceRepository, UserPreferenceRepository>();` (moved from Desktop's CompositionRoot.cs)
- This consolidates all Infrastructure registrations in one place (FR-004, FR-006)

### 3. Desktop Layer (3 file edits)

**Modified**: `Rentier.Desktop.csproj`
- Remove: `<ProjectReference Include="..\Rentier.Infrastructure\Rentier.Infrastructure.csproj" />`
- Remove: `<PackageReference Include="Microsoft.EntityFrameworkCore.Design" ... />` (no longer needed; Infrastructure has its own `AppDbContextDesignTimeFactory`)
- Result: Desktop references only Application + Domain (FR-001)

**Modified**: `App.axaml.cs`
- Remove: `using Rentier.Infrastructure;` and `using Rentier.Infrastructure.Persistence;`
- Remove: `using Microsoft.EntityFrameworkCore;`
- Remove: direct `services.AddInfrastructureServicesAsync(dbPath)` call
- Remove: direct `provider.GetRequiredService<AppDbContext>()` + `MigrateAsync()` call
- Add: Reflection-based `IInfrastructureRegistrar` discovery and invocation
- Add: `provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync()` for migrations
- Result: zero Infrastructure namespace references in App.axaml.cs (FR-002, FR-003, FR-005)

**Modified**: `Composition/CompositionRoot.cs`
- Remove: `using Rentier.Infrastructure.Repositories;`
- Remove: `services.AddTransient<IUserPreferenceRepository, UserPreferenceRepository>();`
- Result: zero Infrastructure concrete types in CompositionRoot (FR-003, FR-006)

### 4. Test Layer (1 new file)

**New**: `Rentier.UnitTests/Architecture/LayerDependencyTests.cs`
- Architectural fitness test that loads `Rentier.Desktop.dll` via reflection
- Asserts: no referenced assembly named `Rentier.Infrastructure`
- Asserts: no type in Desktop references any `Rentier.Infrastructure` namespace
- Purpose: prevents future re-introduction of the prohibited dependency (CA-006)

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Runtime assembly load failure in published app | Low | High | Fail-fast with descriptive error; existing error-window handler shows message. Integration test verifies assembly loads. |
| `dotnet ef` commands stop working | Medium | Medium | Infrastructure already has `AppDbContextDesignTimeFactory`. Verify `dotnet ef migrations add` still works by running it post-refactoring. |
| Missing service registration after consolidation | Low | High | Existing `DiRegistrationSmokeTests` catch missing registrations. Add `IUserPreferenceRepository` to the smoke test if not already covered. |
| Developer accidentally re-adds Infrastructure reference | Medium | Low | Architectural fitness test in CI catches this immediately. |
| Reflection adds startup latency | Very Low | Low | `Assembly.Load` + single interface scan is sub-millisecond. No measurable impact. |

---

## Post-Design Constitution Re-Check

- [x] **Principle I (Clean Architecture)**: Desktop → Application + Domain only. Infrastructure implements Application contracts. ✅ Fully restored.
- [x] **Principle II (Local-First)**: No changes to data storage or credential handling. ✅
- [x] **Principle III (Financial Correctness)**: No monetary or date changes. ✅
- [x] **Principle IV (Async/UI)**: All new methods are async. No blocking calls. ✅
- [x] **Principle V (Quality Gates)**: Architectural fitness test added to CI. ✅
