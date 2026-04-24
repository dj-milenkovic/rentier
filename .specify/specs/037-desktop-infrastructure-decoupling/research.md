# Research: Desktop–Infrastructure Decoupling

**Feature**: 037-desktop-infrastructure-decoupling
**Date**: 2026-04-24

## Research Questions

### R1: What indirection pattern best fits a .NET desktop app with async DI registration?

**Decision**: Application-layer interface (`IInfrastructureRegistrar`) implemented in Infrastructure, discovered at runtime via `Assembly.Load` + interface scanning in Desktop.

**Rationale**:
- Type-safe: Desktop resolves a known interface, not a magic string method name
- Async-compatible: interface method returns `Task`, matching the existing `AddInfrastructureServicesAsync` pattern
- No new dependencies: uses built-in `System.Reflection` and `System.Runtime.Loader`
- Fail-fast: if assembly or implementation is missing, throws immediately at startup
- Discoverable: developers searching for `IInfrastructureRegistrar` find both the contract and implementation

**Alternatives considered**:

| Alternative | Rejected Because |
|------------|-----------------|
| **Direct reflection on extension method** (`Assembly.Load` + `GetMethod("AddInfrastructureServicesAsync")`) | Not type-safe; magic string coupling; no compile-time contract; harder to test. |
| **`Microsoft.Extensions.Hosting`** host-builder pattern | Adds `Microsoft.Extensions.Hosting` NuGet dependency to Desktop; overkill for a desktop app that doesn't need generic host lifecycle. |
| **Assembly-level attribute** (`[assembly: ServiceRegistrar(typeof(...))]`) | More indirect; requires attribute scanning before type scanning; no compile-time interface guarantee. |
| **Convention-based scanning** (scan all assemblies for `IServiceCollection` extension methods) | Fragile; depends on naming conventions; no interface contract; hard to unit test. |
| **Compile-time source generator** | Excessive complexity for a 4-project solution; build-time tooling adds maintenance burden. |

---

### R2: How should database migration be abstracted from the Desktop layer?

**Decision**: New `IDatabaseInitializer` interface in Application, implemented by `DatabaseInitializer` in Infrastructure that wraps `AppDbContext.Database.MigrateAsync()`.

**Rationale**:
- Desktop currently calls `provider.GetRequiredService<AppDbContext>()` and `dbContext.Database.MigrateAsync()` directly — this is the second coupling point to Infrastructure
- Wrapping migration behind an Application-layer interface removes the `using Microsoft.EntityFrameworkCore` import from App.axaml.cs
- The `IDatabaseInitializer` is registered by `InfrastructureRegistrar` as part of the service registration, so Desktop resolves it from DI after registration
- Pattern is extensible: if seeding or other startup-time DB operations are added later, they go behind this interface

**Alternatives considered**:

| Alternative | Rejected Because |
|------------|-----------------|
| **Keep `MigrateAsync` in Desktop via EF Core package reference** | Still requires EF Core package in Desktop; partial decoupling only. |
| **Move migration to Infrastructure's `AddInfrastructureServicesAsync`** | Service registration should not perform I/O; registration and initialization are separate concerns. |
| **Use `IHostedService` for migration** | Requires `Microsoft.Extensions.Hosting`; overkill. |

---

### R3: Should `Microsoft.Extensions.DependencyInjection.Abstractions` be added to Application?

**Decision**: Yes. Add `Microsoft.Extensions.DependencyInjection.Abstractions` (not the full DI package) to `Rentier.Application.csproj`.

**Rationale**:
- Required for `IServiceCollection` parameter type in `IInfrastructureRegistrar`
- This is an **abstractions-only** package: contains interfaces and abstract classes, zero I/O, zero framework coupling
- Widely accepted in .NET Clean Architecture Application layers (used by MediatR, AutoMapper, and other clean architecture libraries)
- Application already depends on `Microsoft.Extensions.Logging.Abstractions` — same pattern

**Alternatives considered**:

| Alternative | Rejected Because |
|------------|-----------------|
| **Custom `IServiceDescriptor` abstraction** | Re-invents the wheel; creates mapping complexity; no ecosystem compatibility. |
| **Pass services as `object` and cast** | Not type-safe; error-prone; poor developer experience. |
| **Define interface without `IServiceCollection` param** | Then the registrar can't actually register services — defeats the purpose. |

---

### R4: What happens to `dotnet ef` migrations after removing EF Core from Desktop?

**Decision**: `dotnet ef` commands continue to work because Infrastructure has `AppDbContextDesignTimeFactory`.

**Rationale**:
- `dotnet ef` needs two things: (1) a design-time factory or startup project that can create `DbContext`, and (2) `Microsoft.EntityFrameworkCore.Design` package
- Infrastructure already has both: `AppDbContextDesignTimeFactory.cs` and `Microsoft.EntityFrameworkCore.Design` in its `.csproj`
- Migration commands will use: `dotnet ef migrations add MigrationName --project src/Rentier.Infrastructure --startup-project src/Rentier.Infrastructure`
- The `--startup-project` flag can point to Infrastructure instead of Desktop since the design-time factory is there

**Verification**: Run `dotnet ef migrations list --project src/Rentier.Infrastructure --startup-project src/Rentier.Infrastructure` after refactoring to confirm.

---

### R5: How to prevent future re-introduction of the Desktop → Infrastructure dependency?

**Decision**: Architectural fitness test in `Rentier.UnitTests` that runs in CI.

**Rationale**:
- Uses `Assembly.GetReferencedAssemblies()` on `Rentier.Desktop.dll` to assert no `Rentier.Infrastructure` reference
- Runs as a standard xUnit test — no special tooling needed
- Fails CI immediately if a developer adds the reference back
- Complements (but does not replace) code review

**Alternatives considered**:

| Alternative | Rejected Because |
|------------|-----------------|
| **MSBuild property to block reference** | No built-in MSBuild mechanism to prohibit a specific ProjectReference. Custom targets are fragile. |
| **NDepend or ArchUnitNET** | Third-party dependency; adds NuGet cost and maintenance. The codebase is small enough that a simple reflection test suffices. |
| **GitHub Actions grep check** | Fragile; depends on csproj format; test-based approach is more robust and runs locally too. |

---

### R6: Should `UserPreferenceRepository` registration simply move to `InfrastructureServiceExtensions`?

**Decision**: Yes. Add the registration line to the existing `AddInfrastructureServicesAsync` method in `InfrastructureServiceExtensions.cs`.

**Rationale**:
- All other repositories are already registered there (7 of 8)
- `UserPreferenceRepository` was added later (T030) and was mistakenly placed in Desktop's `CompositionRoot.cs`
- Moving it consolidates all Infrastructure registrations in one place (FR-004, FR-006)
- No behavioral change: same `AddTransient` lifetime, same interface → implementation mapping

**Verification**: `DiRegistrationSmokeTests` already tests `IUserPreferenceRepository` resolution with a stub — it will continue to pass.
