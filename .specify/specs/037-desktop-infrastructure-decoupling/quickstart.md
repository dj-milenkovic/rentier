# Quickstart: Desktop–Infrastructure Decoupling

**Feature**: 037-desktop-infrastructure-decoupling
**Date**: 2026-04-24

## What This Feature Does

This is a **structural refactoring** that removes the illegal compile-time dependency from `Rentier.Desktop` to `Rentier.Infrastructure`. After this change, Desktop references only `Rentier.Application` and `Rentier.Domain` — restoring full compliance with the Clean Architecture dependency rule.

**No user-visible behavior changes.** The application works identically before and after.

## Key Concepts

### The Problem

```text
BEFORE (violates Clean Architecture):
Desktop.csproj → ProjectReference → Infrastructure.csproj  ← ILLEGAL
App.axaml.cs   → using Rentier.Infrastructure              ← ILLEGAL
App.axaml.cs   → using Rentier.Infrastructure.Persistence   ← ILLEGAL
CompositionRoot → using Rentier.Infrastructure.Repositories  ← ILLEGAL
```

### The Solution

```text
AFTER (compliant):
Desktop.csproj → ProjectReference → Application.csproj + Domain.csproj  ✓
App.axaml.cs   → Assembly.Load("Rentier.Infrastructure") at runtime     ✓
                  IInfrastructureRegistrar (Application-layer contract)  ✓
                  IDatabaseInitializer (Application-layer contract)      ✓
```

## How to Add a New Infrastructure Service (Post-Refactoring)

**Step 1**: Define the interface in `Rentier.Application` (as before).

```csharp
// src/Rentier.Application/Interfaces/IMyNewService.cs
public interface IMyNewService { ... }
```

**Step 2**: Implement in `Rentier.Infrastructure` (as before).

```csharp
// src/Rentier.Infrastructure/MyNewService.cs
public sealed class MyNewService : IMyNewService { ... }
```

**Step 3**: Register in `InfrastructureServiceExtensions.AddInfrastructureServicesAsync` (single location).

```csharp
// src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs
services.AddTransient<IMyNewService, MyNewService>();
```

**That's it.** Desktop requires zero changes. The service is automatically available via DI.

## How to Run EF Core Migrations (Post-Refactoring)

Since Desktop no longer references EF Core, migration commands must target Infrastructure:

```bash
# Add a new migration
dotnet ef migrations add MyMigration \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Infrastructure

# List migrations
dotnet ef migrations list \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Infrastructure

# Update database manually (not usually needed — app migrates on startup)
dotnet ef database update \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Infrastructure
```

The `AppDbContextDesignTimeFactory` in Infrastructure handles design-time `DbContext` creation.

## Architecture Fitness Test

An automated test prevents re-introduction of the violation:

```csharp
// tests/Rentier.UnitTests/Architecture/LayerDependencyTests.cs
[Fact]
public void Desktop_MustNot_Reference_Infrastructure()
{
    var desktopAssembly = typeof(Rentier.Desktop.App).Assembly;
    var referencedNames = desktopAssembly
        .GetReferencedAssemblies()
        .Select(a => a.Name);
    
    referencedNames.Should().NotContain("Rentier.Infrastructure");
}
```

This runs in CI on every PR. If someone accidentally adds the reference back, the build fails.

## Files Changed (Summary)

| File | Action | What Changed |
|------|--------|-------------|
| `src/Rentier.Application/Interfaces/IInfrastructureRegistrar.cs` | **NEW** | Service registration contract |
| `src/Rentier.Application/Interfaces/IDatabaseInitializer.cs` | **NEW** | Migration abstraction |
| `src/Rentier.Application/Rentier.Application.csproj` | **EDIT** | Add DI Abstractions package |
| `src/Rentier.Infrastructure/InfrastructureRegistrar.cs` | **NEW** | Implements IInfrastructureRegistrar |
| `src/Rentier.Infrastructure/DatabaseInitializer.cs` | **NEW** | Implements IDatabaseInitializer |
| `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` | **EDIT** | Add UserPreferenceRepository |
| `src/Rentier.Desktop/Rentier.Desktop.csproj` | **EDIT** | Remove Infrastructure ref |
| `src/Rentier.Desktop/App.axaml.cs` | **EDIT** | Replace Infrastructure calls with reflection |
| `src/Rentier.Desktop/Composition/CompositionRoot.cs` | **EDIT** | Remove Infrastructure import |
| `tests/Rentier.UnitTests/Architecture/LayerDependencyTests.cs` | **NEW** | Architectural fitness test |
