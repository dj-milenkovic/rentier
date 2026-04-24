# Feature Specification: NuGet Central Package Management

**Feature Branch**: `041-nuget-central-package-management`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Implement NuGet Central Package Management (CPM) to address the wildcard version issue identified in the DevOps analysis. Currently, some packages use wildcard versions like 11.* and 20.*."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Centralize All Package Versions (Priority: P1)

As a developer, I want all NuGet package versions defined in a single central file so that every project in the solution uses the same version of each package, eliminating version drift and wildcard ambiguity.

**Why this priority**: This is the core deliverable — without centralized version management, the remaining stories have no foundation. It directly resolves the DevOps finding about wildcard versions.

**Independent Test**: Can be fully tested by building the entire solution after the migration and confirming that all package references resolve to exact versions with no wildcard patterns remaining.

**Acceptance Scenarios**:

1. **Given** the solution has no central package version file, **When** the migration is complete, **Then** a single central version file exists at the repository root defining every package version used across the solution.
2. **Given** 9 project files reference approximately 40 packages with wildcard versions (e.g., `11.*`, `20.*`, `10.0.*`), **When** the migration is complete, **Then** every package version is pinned to an exact stable version (e.g., `11.2.5`, `20.1.39`, `10.0.0`) with no wildcards remaining.
3. **Given** individual project files contain version attributes on package references, **When** the migration is complete, **Then** no project file contains a version attribute on any package reference — versions are managed exclusively by the central file.

---

### User Story 2 - Maintain Build Integrity (Priority: P1)

As a developer, I want the solution to build and all tests to pass identically after the migration so that I can be confident the refactoring introduced no behavioral changes.

**Why this priority**: Equal to P1 because a migration that breaks the build delivers negative value. Build integrity is the fundamental acceptance gate.

**Independent Test**: Can be fully tested by running a full solution build and executing the complete test suite, then comparing the results against a pre-migration baseline.

**Acceptance Scenarios**:

1. **Given** the solution builds successfully before migration, **When** the migration is complete, **Then** the solution still builds successfully with zero errors.
2. **Given** the test suite passes before migration, **When** the migration is complete, **Then** the entire test suite still passes with no new failures.
3. **Given** the solution targets net10.0 (and net10.0-windows for E2E tests), **When** the migration is complete, **Then** all target frameworks continue to resolve and compile correctly.

---

### User Story 3 - Simplify Future Package Updates (Priority: P2)

As a developer, I want to update a package version in one place and have it apply across all projects so that maintaining dependencies requires less effort and eliminates the risk of version inconsistencies between projects.

**Why this priority**: This is the ongoing maintenance benefit of CPM. While not as urgent as the initial migration, it is the primary long-term value proposition.

**Independent Test**: Can be tested by changing a single package version in the central file and verifying that all projects referencing that package now use the updated version after a restore.

**Acceptance Scenarios**:

1. **Given** a package version is defined centrally and referenced by multiple projects, **When** the version is updated in the central file, **Then** all referencing projects use the updated version after a package restore.
2. **Given** a new package needs to be added to a project, **When** the developer adds a package reference, **Then** the version must be defined in the central file rather than in the project file.

---

### Edge Cases

- What happens if a project needs a different version of a package than the centrally defined one? (CPM supports `VersionOverride` for exceptional cases — not expected to be needed in this migration.)
- What happens if a transitive dependency conflicts with a centrally pinned version? (NuGet's dependency resolution handles this; the build will surface warnings or errors if conflicts exist.)
- What happens if a package is referenced in a project but not listed in the central version file? (The build will fail with a clear error message indicating the missing central version definition.)
- How are tool-only packages handled (e.g., `coverlet.collector` marked as private assets)? (Private asset metadata stays on the project-level `<PackageReference>`; only the `Version` attribute moves to the central file.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A central package version file MUST be created at the repository root with centralized version management enabled.
- **FR-002**: The central file MUST contain an entry for every distinct NuGet package referenced across all 9 project files (4 source projects, 5 test projects).
- **FR-003**: Every package version in the central file MUST be pinned to an exact stable version with no wildcard patterns (no `*` characters in any version string).
- **FR-004**: The pinned version for each package MUST be the latest stable release available at the time of migration, replacing the previously used wildcard range.
- **FR-005**: All `Version` attributes MUST be removed from `<PackageReference>` elements in individual project files.
- **FR-006**: Package-level metadata (e.g., `PrivateAssets`, `IncludeAssets`, `ExcludeAssets`) MUST be preserved on the project-level `<PackageReference>` elements.
- **FR-007**: The complete solution MUST build successfully after migration with zero errors and zero new warnings related to package resolution.
- **FR-008**: The complete test suite MUST pass after migration with no new test failures.
- **FR-009**: The existing `Directory.Build.props` file MUST NOT be modified by this migration.
- **FR-010**: Packages referenced by multiple projects MUST resolve to the same version across the entire solution after migration.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: No application layers are impacted. This is a build-system-only change affecting project files and a new MSBuild props file. Clean Architecture boundaries are unaffected — no source code changes occur.
- **CA-002 (Money and Dates)**: Not applicable — no monetary or date fields are involved in this change.
- **CA-003 (Privacy and Security)**: Not applicable — no data storage or secrets are involved. Package versions are non-sensitive build metadata.
- **CA-004 (Network Scope)**: Not applicable — no outbound calls are added or modified. NuGet restore uses the same package sources as before.
- **CA-005 (Async and UI)**: Not applicable — no runtime code changes. This is a build-time-only refactoring.
- **CA-006 (Testing Impact)**: No new tests are required. The existing test suite serves as the regression gate. All existing tests (unit, integration, scenario, E2E) MUST continue to pass unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero wildcard version patterns (`*`) remain in any project file or the central version file across the entire solution.
- **SC-002**: 100% of NuGet packages (approximately 27 distinct packages across 9 projects) are defined in the central version file with exact versions.
- **SC-003**: The full solution build completes successfully with zero package-resolution errors or warnings.
- **SC-004**: 100% of existing tests pass after the migration with no regressions.
- **SC-005**: Package version updates require changing exactly 1 file (the central version file) instead of updating multiple project files individually.
- **SC-006**: No project file contains a `Version` attribute on any `<PackageReference>` element after migration.

## Assumptions

- The latest stable version of each package at migration time is compatible with the solution's target framework (net10.0) and with all other packages in the dependency graph.
- No project requires a different version of any package than the rest of the solution (no `VersionOverride` scenarios are expected).
- The existing `Directory.Build.props` at the repository root is the only MSBuild customization file; no other `Directory.Build.targets` or import chains will conflict with central package management.
- Wildcard versions (e.g., `11.*`) currently resolve to the latest stable patch within their major.minor range during NuGet restore; pinning to those same resolved versions introduces no behavioral change.
- All package sources (NuGet feeds) used by the solution are accessible and contain the required exact versions.
- The `Rentier.Domain` project has no NuGet package dependencies and requires no entries in the central version file.
