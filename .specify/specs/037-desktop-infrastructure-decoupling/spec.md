# Feature Specification: Desktop–Infrastructure Decoupling

**Feature Branch**: `037-desktop-infrastructure-decoupling`  
**Created**: 2026-04-24  
**Status**: Draft  
**Input**: User description: "Fix the critical Desktop → Infrastructure layer violation identified in the code analysis. Currently: Desktop.csproj has a direct ProjectReference to Infrastructure, App.axaml.cs has using Rentier.Infrastructure imports, CompositionRoot.cs references Infrastructure repositories directly. The Desktop layer should reference ONLY Application + Domain. Infrastructure should be wired up via an AddInfrastructureServices(IServiceCollection) extension method. Desktop calls this extension method through a delegate or host builder pattern, so Desktop has no compile-time dependency on Infrastructure."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Remove Desktop Compile-Time Dependency on Infrastructure (Priority: P1)

As a **developer maintaining the Rentier codebase**, I need the Desktop project to have zero compile-time references to the Infrastructure project, so that changes to data-access implementations, external-service adapters, or persistence technology never require recompiling or modifying the Desktop layer.

**Why this priority**: This is the core architectural violation. The Desktop project currently imports Infrastructure namespaces and registers Infrastructure types directly. Removing the compile-time dependency is the single most impactful change — it enforces the Clean Architecture dependency rule at the compiler level, making illegal cross-layer access impossible.

**Independent Test**: Can be fully verified by removing the Infrastructure ProjectReference from Desktop.csproj and confirming the project compiles successfully with zero errors related to missing Infrastructure types.

**Acceptance Scenarios**:

1. **Given** the Desktop project file (Desktop.csproj), **When** the project is built, **Then** there is no ProjectReference to the Infrastructure project.
2. **Given** any source file in the Desktop project, **When** its imports are inspected, **Then** no `Rentier.Infrastructure` or `Rentier.Infrastructure.*` namespace references exist.
3. **Given** the Desktop project, **When** it is compiled in isolation (without the Infrastructure project loaded), **Then** it compiles successfully with zero errors.

---

### User Story 2 — Consolidate Infrastructure Service Registration (Priority: P2)

As a **developer adding new infrastructure services**, I need all Infrastructure-layer service registrations to live in a single, well-known entry point inside the Infrastructure project, so that the Desktop composition root never needs to know about concrete Infrastructure types.

**Why this priority**: Currently, the `UserPreferenceRepository` is registered directly in Desktop's `CompositionRoot.cs` — bypassing the Infrastructure extension method that already exists. Consolidating all registrations into one place prevents future "registration drift" where developers add Infrastructure types in the wrong layer.

**Independent Test**: Can be verified by checking that the Infrastructure extension method registers all repository implementations (including `UserPreferenceRepository`) and that Desktop's composition root contains no direct references to any Infrastructure concrete type.

**Acceptance Scenarios**:

1. **Given** the Infrastructure service registration entry point, **When** it is invoked during application startup, **Then** all repository implementations (including `UserPreferenceRepository`) are registered against their Application-layer interfaces.
2. **Given** the Desktop composition root, **When** its source code is inspected, **Then** it contains zero references to any concrete type from the Infrastructure project.
3. **Given** a new Infrastructure service is needed in the future, **When** a developer adds it, **Then** it is registered only in the Infrastructure extension method — not in Desktop.

---

### User Story 3 — Wire Infrastructure via Indirection Pattern (Priority: P3)

As a **developer working on application startup**, I need the Desktop layer to invoke Infrastructure registration without a direct project reference, so that the composition root remains clean and the wiring mechanism is explicit and discoverable.

**Why this priority**: Simply removing the ProjectReference without providing an alternative wiring mechanism would break the application at runtime. A delegate or host-builder pattern allows Desktop to trigger Infrastructure registration at startup while keeping the compile-time boundary intact.

**Independent Test**: Can be verified by starting the application and confirming that all services resolve correctly (no missing-service exceptions) despite Desktop having no compile-time reference to Infrastructure.

**Acceptance Scenarios**:

1. **Given** the application starts up, **When** the dependency injection container is built, **Then** all Infrastructure services are available for injection into Application-layer handlers and Desktop ViewModels.
2. **Given** the Desktop composition root, **When** it configures Infrastructure services, **Then** it does so through an indirection mechanism (delegate, interface, or host-builder pattern) that requires no compile-time knowledge of Infrastructure types.
3. **Given** the application is running after the decoupling, **When** any user workflow that accesses data is exercised (e.g., viewing filings, syncing mailbox, viewing taxpayer profile), **Then** all operations succeed identically to the pre-decoupling behavior.

---

### Edge Cases

- What happens if the Infrastructure assembly is missing at runtime? The application should fail fast at startup with a clear error indicating that the Infrastructure services could not be loaded, rather than failing silently at first use.
- What happens if a developer accidentally adds an Infrastructure reference back to Desktop? The build or CI pipeline should detect and reject the prohibited dependency.
- What happens when new repository interfaces are added to the Application layer? The Infrastructure extension method must be the single place where their implementations are registered — no Desktop modifications needed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Desktop project MUST NOT contain any compile-time project reference to the Infrastructure project.
- **FR-002**: The Desktop project source files MUST NOT contain any `using` directives referencing `Rentier.Infrastructure` or any of its sub-namespaces (e.g., `Rentier.Infrastructure.Persistence`, `Rentier.Infrastructure.Repositories`).
- **FR-003**: The Desktop composition root MUST NOT directly instantiate, reference, or register any concrete type defined in the Infrastructure project.
- **FR-004**: The Infrastructure project MUST provide a single, consolidated entry point for registering all of its services (repositories, database context, HTTP clients, credential stores, and external-service adapters) into the dependency injection container.
- **FR-005**: The Desktop layer MUST invoke Infrastructure service registration through an indirection mechanism (such as a delegate, interface, or host-builder pattern) that does not require compile-time knowledge of Infrastructure types.
- **FR-006**: All repository implementations currently registered in the Desktop composition root (specifically `UserPreferenceRepository`) MUST be moved to the Infrastructure service registration entry point.
- **FR-007**: The application MUST start successfully and all services MUST resolve correctly after the decoupling — zero runtime service-resolution failures.
- **FR-008**: All existing user-facing functionality MUST remain identical after the refactoring — this is a structural change with no behavioral changes.
- **FR-009**: The application MUST fail fast at startup with a descriptive error if the Infrastructure services are not properly wired, rather than failing at first use.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature directly restores compliance with Constitution Principle I (Clean Architecture Dependency Rule). The Desktop layer currently violates the rule by referencing Infrastructure. After this change, Desktop will reference only Application + Domain — the exact boundary specified in the constitution. All four layers are impacted: Desktop (remove references), Infrastructure (consolidate registrations), Application (unchanged, but its interfaces become the sole contract surface), Domain (unaffected).
- **CA-002 (Money and Dates)**: No monetary or date fields are introduced or modified. This is a structural refactoring only. Existing `decimal` and `DateOnly` usage is unaffected.
- **CA-003 (Privacy and Security)**: No changes to data storage, credential handling, or security boundaries. The credential-store registration (already in Infrastructure's extension method) remains in place. Local-first data storage is unaffected.
- **CA-004 (Network Scope)**: No changes to outbound network calls. IMAP and NBS endpoints remain the only external connections. Their registrations stay within Infrastructure.
- **CA-005 (Async and UI)**: No changes to async patterns. The existing `async` Infrastructure registration method is preserved. No blocking calls are introduced.
- **CA-006 (Testing Impact)**: Desktop tests should verify that no Infrastructure namespace references exist. Infrastructure tests should verify that the consolidated extension method registers all expected services. An architectural fitness test (or CI rule) should prevent future Desktop → Infrastructure references.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The Desktop project builds successfully with zero references to the Infrastructure project — verified by inspecting the project file and confirming no Infrastructure ProjectReference exists.
- **SC-002**: A text search across all Desktop source files for `Rentier.Infrastructure` returns zero matches.
- **SC-003**: All existing automated tests pass without modification (green CI), confirming zero behavioral regression.
- **SC-004**: The application starts and all user workflows (viewing filings, syncing mailbox, managing taxpayer profile, exporting XML, configuring settings) work identically to pre-refactoring behavior.
- **SC-005**: The Infrastructure service registration entry point registers 100% of the repository implementations that were previously registered in Desktop or were already registered there — no "registration gaps."
- **SC-006**: Future developers can add new Infrastructure services by modifying only the Infrastructure project — Desktop requires zero changes for new service additions.

## Assumptions

- The existing `AddInfrastructureServicesAsync` extension method in the Infrastructure project is the correct consolidation point for all Infrastructure registrations — it already registers most services and only needs `UserPreferenceRepository` added.
- The indirection mechanism (delegate, interface, or host-builder pattern) will use the standard dependency injection container (`Microsoft.Extensions.DependencyInjection`) already in use across the solution.
- Runtime assembly loading (for the indirection pattern) is supported in the target deployment environments (Windows and macOS desktop) — this is standard behavior for .NET desktop applications.
- The database path (`dbPath`) parameter currently passed from Desktop to `AddInfrastructureServicesAsync` can be conveyed through the indirection mechanism without requiring Infrastructure-specific types.
- No new features or services need to be added concurrently with this refactoring — the scope is limited to restructuring existing registrations.
- The `AppDbContext` migration call currently in `App.axaml.cs` can be moved behind the indirection boundary or accessed through an Application-layer abstraction (e.g., a "database initialization" interface).
