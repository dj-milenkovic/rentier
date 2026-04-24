# Data Model: Desktop–Infrastructure Decoupling

**Feature**: 037-desktop-infrastructure-decoupling
**Date**: 2026-04-24

## Overview

This feature introduces no domain entities, value objects, or database schema changes. The data model changes are limited to two new Application-layer interface contracts that abstract Infrastructure concerns from the Desktop layer.

## New Interfaces

### IInfrastructureRegistrar

**Layer**: Application (`Rentier.Application.Interfaces`)
**Purpose**: Abstracts the registration of all Infrastructure services into the DI container.

```csharp
namespace Rentier.Application.Interfaces;

/// <summary>
/// Contract for registering infrastructure-layer services into the DI container.
/// Implemented in Rentier.Infrastructure and discovered at runtime by the Desktop layer.
/// </summary>
public interface IInfrastructureRegistrar
{
    /// <summary>
    /// Registers all infrastructure services (repositories, DbContext, HTTP clients,
    /// credential store, parsers, serializers, sync services) into the service collection.
    /// </summary>
    /// <param name="services">The DI service collection to register into.</param>
    /// <param name="dbPath">Absolute path to the SQLite database file.</param>
    Task RegisterServicesAsync(IServiceCollection services, string dbPath);
}
```

**Fields/Parameters**:

| Parameter | Type | Description | Validation |
|-----------|------|-------------|------------|
| `services` | `IServiceCollection` | DI container builder | Not null |
| `dbPath` | `string` | Absolute path to SQLite database | Not null or empty |

**Relationships**: Implemented by `InfrastructureRegistrar` in Infrastructure layer.

---

### IDatabaseInitializer

**Layer**: Application (`Rentier.Application.Interfaces`)
**Purpose**: Abstracts database initialization (schema migration) so Desktop never references EF Core or `AppDbContext`.

```csharp
namespace Rentier.Application.Interfaces;

/// <summary>
/// Contract for initializing the database (applying migrations, creating schema).
/// Implemented in Rentier.Infrastructure and resolved from DI at startup.
/// </summary>
public interface IDatabaseInitializer
{
    /// <summary>
    /// Applies pending database migrations. Called once at application startup.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);
}
```

**Fields/Parameters**:

| Parameter | Type | Description | Validation |
|-----------|------|-------------|------------|
| `ct` | `CancellationToken` | Cancellation token for async operation | Optional (default) |

**Relationships**: Implemented by `DatabaseInitializer` in Infrastructure layer. Registered as `Transient` by `InfrastructureRegistrar`.

---

## New Implementation Classes

### InfrastructureRegistrar

**Layer**: Infrastructure (`Rentier.Infrastructure`)
**Purpose**: Implements `IInfrastructureRegistrar` by delegating to existing `AddInfrastructureServicesAsync`.

```csharp
namespace Rentier.Infrastructure;

public sealed class InfrastructureRegistrar : IInfrastructureRegistrar
{
    public async Task RegisterServicesAsync(IServiceCollection services, string dbPath)
    {
        await services.AddInfrastructureServicesAsync(dbPath);
        services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();
    }
}
```

**Relationships**: Implements `IInfrastructureRegistrar` (Application). Delegates to `InfrastructureServiceExtensions` (Infrastructure).

---

### DatabaseInitializer

**Layer**: Infrastructure (`Rentier.Infrastructure`)
**Purpose**: Implements `IDatabaseInitializer` by applying EF Core migrations.

```csharp
namespace Rentier.Infrastructure;

public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly AppDbContext _db;

    public DatabaseInitializer(AppDbContext db) => _db = db;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);
    }
}
```

**Relationships**: Implements `IDatabaseInitializer` (Application). Depends on `AppDbContext` (Infrastructure.Persistence).

---

## Modified Registrations

### InfrastructureServiceExtensions (modified)

**Change**: Add `UserPreferenceRepository` registration (moved from Desktop's `CompositionRoot.cs`).

```diff
  services.AddTransient<IFilingRepository, FilingRepository>();
+ services.AddTransient<IUserPreferenceRepository, UserPreferenceRepository>();
  services.AddTransient<IXmlFilingSerializer, PpOpoXmlSerializer>();
```

**Rationale**: Consolidates all 8 repository registrations in the single Infrastructure entry point.

---

## Entity/Schema Impact

| Area | Impact |
|------|--------|
| Domain entities | None — no new entities, no field changes |
| Value objects | None |
| Database schema | None — no new tables, columns, or migrations |
| EF Core configurations | None |
| DTOs | None |

## State Transitions

No state machines are introduced or modified by this feature.
