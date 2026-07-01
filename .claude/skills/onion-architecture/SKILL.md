---
name: onion-architecture
description: >
  Explains and enforces the Onion (Ports & Adapters) architecture used in PredictiveAnalyticsManager.
  Use this skill whenever the user asks where to put new code, which layer something belongs to,
  why a dependency is not allowed, how to avoid coupling between layers, what base classes to extend,
  how to structure a new feature folder, or any question about separation of concerns — even if they
  don't use the word "architecture". Also reference this skill proactively when reviewing code that
  may be putting logic in the wrong layer (e.g. business rules in a controller, DB queries in a use
  case, domain logic in infrastructure).
---

# Onion Architecture Guide

PAM is structured as four concentric layers. The fundamental rule is **dependencies only point inward**:
outer layers may reference inner layers, but inner layers must never reference outer ones.

```
┌─────────────────────────────────────────────────────────────────┐
│  API / Presentation  (Controllers, Validators, DI wiring)        │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │  Infrastructure  (Repositories, DB models, AWS clients)   │   │
│  │  ┌─────────────────────────────────────────────────────┐  │   │
│  │  │  Application  (Use cases, Ports/interfaces, DTOs)   │  │   │
│  │  │  ┌───────────────────────────────────────────────┐  │  │   │
│  │  │  │  Domain  (Entities, business rules, enums)    │  │  │   │
│  │  │  └───────────────────────────────────────────────┘  │  │   │
│  │  └─────────────────────────────────────────────────────┘  │   │
│  └───────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

These rules are enforced automatically by the ArchUnit tests in `PredictiveAnalyticsManager.ArchUnitTests`.
If you violate a rule, a test will fail — treat it as a hard constraint, not a style suggestion.

---

## Layer 1: Domain

**Assembly:** `PredictiveAnalyticsManager.Domain`  
**Namespace prefix:** `Prophet.SaaS.PredictiveAnalyticsManager.Domain`

This is the innermost layer and has **no dependencies on any other PAM assembly**. It is the source
of truth for what the system's concepts *are* and what rules govern them.

### What belongs here

- **Entities** — classes extending `BaseModel` (full entity with audit fields) or referencing `BaseLiteModel`
  (lightweight projection for list queries).
- **Business rules and invariants** — validation that is part of the domain's identity, e.g. "a project
  name must be unique", "a model cannot be activated while in draft status". These live inside entity
  methods or companion classes, not in use cases or controllers.
- **Aggregate guard classes** — a plural companion class (e.g. `Projects.cs`) that enforces cross-entity
  rules within the same aggregate.
- **Enums** — status, type, or category enums that describe domain concepts.
- **Domain exceptions** — thrown when a domain invariant is violated.

### Base classes to extend

| Class | Use when |
|---|---|
| `BaseModel` | Full entity with `Id`, `CreatedBy`, `CreatedAt`, `ModifiedBy`, `ModifiedAt` |
| `BaseLiteModel` | Lightweight read projection for list/lite queries |

### What does NOT belong here

- Any reference to `IServiceCollection`, AutoMapper, or any framework type
- Repository calls or data access of any kind
- DTO types

### Feature folder layout

```
Domain/
  <Feature>/
    Feature.cs          ← Entity (extends BaseModel)
    FeatureLite.cs      ← Optional lite projection (extends BaseLiteModel)
    Features.cs         ← Aggregate guard (optional, for cross-entity rules)
    FeatureStatus.cs    ← Enum
```

### Checklist

- [ ] Entity inherits from `BaseModel` or `BaseLiteModel`
- [ ] All invariants enforced in constructor or entity methods
- [ ] Properties immutable (private setters) or mutated through explicit methods
- [ ] Business validation throws domain exceptions, not generic ones
- [ ] No reference to Application, Infrastructure, or any framework type

---

## Layer 2: Application

**Assembly:** `PredictiveAnalyticsManager.Application`  
**Namespace prefix:** `Prophet.SaaS.PredictiveAnalyticsManager.Application`

**Depends on:** Domain only.

This layer orchestrates the domain. A use case answers the question "what does the system *do*?"
It coordinates domain objects and calls ports (interfaces), but it does not know *how* those ports
are implemented — that is Infrastructure's job.

### What belongs here

- **DTOs** — data shapes that cross the API boundary (`CreateFeatureDto`, `FeatureDto`). All must extend
  `ResourceObject` and be decorated with `[ApiResourceObject]`.
- **Port interfaces (repository interfaces)** — the contracts that Infrastructure must implement,
  placed in `<Feature>/Ports/`. They extend base port interfaces from `Application.Base.Ports`:
  - `IBaseRepository<TEntity, TFilter>` — full CRUD
  - `IBaseReadRepository<TEntity, TLite, TFilter>` — reads + lite projections
  - Add feature-specific query methods as additional interface members.
- **Use case interfaces** — one per verb, placed in `<Feature>/UseCases/`.
- **Use case implementations** — extend the appropriate base class (see table below), placed in
  `<Feature>/UseCases/Implementations/`.
- **Filter/query models** — classes that carry filtering parameters from the controller through
  to the repository, placed in `<Feature>/Filters/` or `Application.Base.Filters`.

### Base use case classes to extend

| Base class | Use when |
|---|---|
| `WriteUseCase<TEntity, TCreate, TResponse, TFilter, TException>` | Create only (no update) |
| `CreateOrUpdateUseCase<TEntity, TCreate, TResponse, TUpdate, TFilter, TException>` | Create **and** Update (PATCH/PUT) |
| `GetLiteUseCase<TEntity, TLite, TDto, TFilter>` | Retrieving a paged/filtered list |
| `GetUseCase<TEntity, TDto, TFilter>` | Retrieving a single entity by ID |
| Custom class | Delete or any non-standard operation |

Override `ValidateBeforeCreate(TCreateDto, TEntity)` when uniqueness or business rule checks
require a repository read — this is the right place for that, not in the domain entity itself or
in the controller.

### DI registration

Use cases are registered in `Application/Configurations/ServiceCollectionExtensions.cs`
inside the `AddUseCases()` method using `AddTransient`.

### What does NOT belong here

- Any reference to `Entity Framework`, `Dapper`, `Amazon.SQS`, `AWSSDK.*`, or any concrete
  infrastructure technology
- The `ServiceCollectionExtensions` in Application should only register use cases — not repositories
  or infrastructure concerns
- Direct HTTP client calls

### Exception handling

Let domain exceptions propagate unchanged — they carry business meaning and callers need to handle
them selectively. Only catch infrastructure exceptions (DB failures, AWS timeouts), log them, and
rethrow as a meaningful application-level exception:

```csharp
catch (EntityConflictException) { throw; }  // domain exception — propagate
catch (JobLaunchFailedException ex)
{
    _logger.LogError(ex, "Job launch failed for {Id}", id);
    throw new RunProcessFailedException("Process launch failed.");
}
```

### Feature folder layout

```
Application/
  <Feature>/
    Dtos/
      CreateFeatureDto.cs
      FeatureDto.cs
    Filters/
      FeatureFilterQueryModel.cs    ← optional, if custom filtering needed
    Ports/
      IFeatureRepository.cs
    UseCases/
      ICreateOrUpdateFeatureUseCase.cs
      IGetFeaturesUseCase.cs
      IDeleteFeatureUseCase.cs
      Implementations/
        CreateOrUpdateFeatureUseCase.cs
        GetFeaturesUseCase.cs
        DeleteFeatureUseCase.cs
```

### Checklist

- [ ] Each use case implements a corresponding interface
- [ ] All infrastructure interactions go through port interfaces
- [ ] Business logic delegated to domain entities — not written in use cases
- [ ] Domain exceptions propagate; infrastructure exceptions are caught, logged, and rethrown
- [ ] Results mapped to DTOs before returning to the caller

---

## Layer 3: Infrastructure

**Assembly:** `PredictiveAnalyticsManager.Infrastructure`  
**Namespace prefix:** `Prophet.SaaS.PredictiveAnalyticsManager.Infrastructure`

**Depends on:** Application + Domain.

This layer contains everything that touches the outside world: databases, AWS services, file systems.
Its job is to implement the port interfaces declared in Application — it adapts external systems to
the contracts the application expects.

### What belongs here

- **Repository implementations** — implement `IFeatureRepository` from Application.
- **DB models** — POCOs that map to database tables (`FeatureDbModel.cs`). These are purely DB concerns
  and must not leak into the Application or Domain layers.
- **Data access handlers** — query/command handlers for Dapper or other DB access, in `<Feature>/DataAccess/`.
- **AWS client wrappers** — S3, SQS, Batch, STS integrations (see AWS SDK skill).
- **AutoMapper mappings** — profiles that translate between Domain entities and DB models or DTOs,
  in `Configurations/InfrastructureMappingProfile.cs`.
- **Sort query support** — `IFeatureSortQuerySupport` implementations in `<Feature>/`.

### DI registration

Repositories and sort support are registered in `Infrastructure/Configurations/ServiceCollectionExtensions.cs`.
Use `AddScoped` for repositories.

### What does NOT belong here

- Business logic — it may seem convenient to put a guard inside a repository, but rules belong in the domain.
- Direct controller dependencies — Infrastructure should never know about HTTP concepts.

### Feature folder layout

```
Infrastructure/
  <Feature>/
    FeatureDbModel.cs
    FeatureRepository.cs
    IFeatureSortQuerySupport.cs    ← optional
    FeatureSortQuerySupport.cs     ← optional
    DataAccess/
      IFeatureDbContext.cs         ← interface extending IDbContext<FeatureDbModel, FeatureLite>
      FeatureDbContext.cs          ← delegates to FeatureDbSource
      FeatureDbSource.cs           ← wraps each operation in a transaction; delegates to QueryExecutor
      Handler/
        FeatureDbReader.cs         ← maps DbDataReader columns → FeatureDbModel / FeatureLite
        FeatureQueryExecutor.cs    ← SQL constants + InsertAsync / UpdateAsync / GetByIdQuery / FindQuery
```

**Data access call chain (read or write):**

```
Repository.UpdateAsync(domainEntity)
  → MapToDbModel(entity)           ← AutoMapper: domain → DB model
  → DbContext.UpdateAsync(dbModel)
    → DbSource.UpdateAsync(dbModel, isAsync)
      → ExecuteInTransaction(...)
        → QueryExecutor.UpdateAsync(dbFactory, dbModel, transaction)
          → ExecuteNonQuery(UPDATE_SQL, params, transaction)
```
### Checklist

- [ ] Repository implements the matching Application port interface
- [ ] DB models used only inside Infrastructure — never exposed to Application or Domain
- [ ] Domain entities (not DB models) returned from repository methods
- [ ] No business logic inside repositories
- [ ] Proper exception handling for all external system calls

---

## Layer 4: API / Presentation

**Assembly:** `PredictiveAnalyticsManager.API`  
**Namespace prefix:** `Prophet.SaaS.PredictiveAnalyticsManager.API`

**Depends on:** Application (via use case interfaces) — it should never directly reference Infrastructure
types in controller logic (only in DI wiring in `ServiceFactory.cs` / extensions).

### What belongs here

- **Controllers** — translate HTTP requests into use case calls and format responses. No business
  logic, no repository calls. Choose the right base controller from `Controllers/Base/`.
- **Validators** — FluentValidation validators for incoming DTOs, placed in `API/Validators/`.
  Registered automatically via assembly scanning.
- **Background workers** — `BackgroundWorkers/` for hosted services like the `SQSWorker`.
- **DI orchestration** — `ServiceFactory.cs` and `Extensions/` glue all layers together.

### What does NOT belong here

- Business rules — even simple ones like "if name is empty, return a domain error" belong in the
  domain or use case, not in the controller action.
- Direct repository or DB access of any kind.

### HTTP status code rules

| Code | When to use |
|---|---|
| 200 OK | Successful GET |
| 201 Created | Successful POST (create) |
| 204 No Content | Successful DELETE |
| 400 Bad Request | Validation error (FluentValidation failure) |
| 404 Not Found | Entity not found |
| 409 Conflict | Duplicate or state conflict |

### Checklist

- [ ] Controller inherits from the correct base (`CrudController`, `ReadController`, etc.)
- [ ] All logic delegated to use cases — no business logic in controller
- [ ] All action methods declare `[ProducesResponseType]` for every possible status code
- [ ] All inputs validated with FluentValidation before calling the use case
- [ ] `Guid` parameters validated with `IValidator<Guid>` before use

---

## Dependency rule violations — what to look for

These are the most common mistakes. The ArchUnit tests will catch them, but it's better to
avoid them upfront:

| Violation | Why it's wrong | Correct approach |
|---|---|---|
| Application references Infrastructure type | Creates a circular dependency; Application should not know how ports are implemented | Introduce a port interface in Application; implement in Infrastructure |
| Controller calls `IRepository` directly | Skips the use case layer, bypasses business logic | Controller calls a use case; use case calls the repository |
| Business rule in a use case | Use cases orchestrate, not validate domain state | Move invariant to domain entity or aggregate guard |
| Domain entity imports a DTO | Domain should not know about API shapes | Map in Application using AutoMapper |
| Infrastructure service has HTTP logic | Coupling external service to HTTP semantics | Abstract behind a port interface |

---

## Decision guide: where does this code go?

| If you're writing... | It belongs in |
|---|---|
| "A project name must be unique across a tenant" | Domain (`Projects.cs` guard) |
| "Fetch all active projects from the database" | Application port interface + Infrastructure implementation |
| "Map an HTTP POST body to a domain entity and save it" | Application use case |
| "Connect to AWS S3 and upload a file" | Infrastructure (AWS skill covers this) |
| "Return 400 if the request body is malformed" | API validator (FluentValidation) |
| "Return 404 if the entity doesn't exist" | Controller base class handles this via use case result |
| "Register this service for DI" | Use cases → Application `ServiceCollectionExtensions`; repos → Infrastructure `ServiceCollectionExtensions` |
| "AutoMapper: domain entity → DTO" | Application `MapperProfile` |
| "AutoMapper: domain entity → DB model" | Infrastructure `InfrastructureMappingProfile` |

---

## Enforced by ArchUnit

The tests in `PredictiveAnalyticsManager.ArchUnitTests` automatically verify:
- `RestLayer` does not directly depend on `DataAccessLayer` types outside of DI wiring
- `ServiceLayer` (Application) does not depend on `DataAccessLayer` (Infrastructure)
- `DomainLayer` has no outward dependencies

When adding new code, run the ArchUnit tests to confirm you haven't introduced a forbidden dependency:
```
dotnet test --filter FullyQualifiedName~ArchUnitTests
```

---

## Implementation patterns

For concrete code examples per layer (rich domain models with state transitions, domain services,
use case exception handling, full controller wiring), read:

`references/implementation-patterns.md`
