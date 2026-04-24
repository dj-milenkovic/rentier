# Feature Specification: Publish Configuration

**Feature Branch**: `040-publish-configuration`  
**Created**: 2025-07-17  
**Status**: Draft  
**Input**: User description: "Add self-contained single-file publish configuration to Rentier.Desktop.csproj for Release builds"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consistent Release Builds (Priority: P1)

As a release engineer, I want the desktop project file to declare all publish settings for Release builds so that every `dotnet publish -c Release` invocation produces a self-contained single-file executable without requiring extra command-line flags.

**Why this priority**: This is the core value of the feature — consolidating scattered publish flags into a single source of truth in the project file ensures consistent, reproducible builds regardless of whether they run locally or in CI.

**Independent Test**: Can be fully tested by running `dotnet publish -c Release -r win-x64 -o ./publish/test` on the desktop project and verifying the output is a single self-contained executable with embedded debug symbols.

**Acceptance Scenarios**:

1. **Given** the desktop project file contains the Release publish configuration, **When** a developer runs `dotnet publish -c Release -r win-x64`, **Then** the output directory contains a single self-contained executable with no separate PDB files.
2. **Given** the desktop project file contains the Release publish configuration, **When** a developer runs `dotnet publish -c Release -r osx-arm64`, **Then** the output is a self-contained single-file binary without ReadyToRun compilation (since ReadyToRun is Windows-only).
3. **Given** the desktop project file contains the Release publish configuration, **When** a developer runs `dotnet publish -c Release -r linux-x64`, **Then** the output is a self-contained single-file binary without ReadyToRun compilation.

---

### User Story 2 - Debug Builds Unaffected (Priority: P1)

As a developer, I want the publish settings to apply only to Release builds so that my day-to-day Debug workflow remains fast and unchanged.

**Why this priority**: Equally critical to P1 — if Debug builds are accidentally affected, developer productivity suffers with longer build times and unnecessary single-file bundling during development.

**Independent Test**: Can be fully tested by running `dotnet build -c Debug` and `dotnet publish -c Debug -r win-x64` on the desktop project and verifying that none of the Release-only publish settings are active.

**Acceptance Scenarios**:

1. **Given** the desktop project file contains the Release publish configuration, **When** a developer runs `dotnet build -c Debug`, **Then** the build completes without applying self-contained, single-file, or ReadyToRun settings.
2. **Given** the desktop project file contains the Release publish configuration, **When** a developer runs `dotnet publish -c Debug -r win-x64`, **Then** the output is a standard framework-dependent publish (not self-contained, not single-file).

---

### User Story 3 - Simplified CI Pipeline (Priority: P2)

As a CI maintainer, I want the release workflow to rely on the project file's publish settings so that the workflow YAML can be simplified by removing redundant `-p:` flags that are now declared in the project file.

**Why this priority**: Reducing duplication between the project file and CI workflow prevents configuration drift and makes maintenance easier, but is secondary to getting the project file settings correct first.

**Independent Test**: Can be tested by verifying that the CI release workflow produces identical artifacts before and after removing the redundant `-p:` flags, since the project file now supplies those values.

**Acceptance Scenarios**:

1. **Given** the project file declares all publish settings for Release, **When** the CI workflow runs `dotnet publish -c Release -r <rid>` without explicit `-p:PublishSingleFile`, `-p:DebugType`, or `-p:PublishReadyToRun` flags, **Then** the output artifact is identical in structure to the current release output.
2. **Given** the project file declares `PublishReadyToRun` conditionally for Windows, **When** the CI workflow publishes for `osx-arm64` or `linux-x64`, **Then** ReadyToRun is not applied and the build succeeds without warnings.

---

### Edge Cases

- What happens when a developer runs `dotnet publish` without specifying a configuration? The default configuration is `Debug`, so publish settings should not apply.
- What happens when `PublishReadyToRun` is evaluated on a macOS or Linux build agent? The MSBuild OS platform condition must correctly evaluate to `false`, skipping ReadyToRun.
- What happens when native libraries (e.g., SQLite native bindings) are present? `IncludeNativeLibrariesForSelfExtract` ensures they are embedded in the single-file bundle rather than extracted to disk as sidecar files.
- What happens when the single-file executable is significantly larger due to compression being enabled? `EnableCompressionInSingleFile` trades slightly longer startup time for a smaller distributable — this is acceptable for a desktop application distributed as an installer.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The desktop project file MUST contain a property group conditioned on Release configuration that activates self-contained publishing.
- **FR-002**: Release publishes MUST produce a single-file executable (all managed assemblies bundled into one file).
- **FR-003**: Release publishes on Windows MUST enable ReadyToRun ahead-of-time compilation for faster startup.
- **FR-004**: Release publishes on non-Windows platforms (macOS, Linux) MUST NOT enable ReadyToRun.
- **FR-005**: Release publishes MUST embed debug symbols directly into the executable (no separate `.pdb` files).
- **FR-006**: Release publishes MUST include native libraries inside the single-file bundle (no sidecar extraction).
- **FR-007**: Release publishes MUST compress the single-file bundle to reduce distributable size.
- **FR-008**: Debug builds and Debug publishes MUST NOT be affected by any of the Release publish settings.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Only the outermost layer (`Rentier.Desktop`) is affected. This is a build configuration change to the project file — no source code or dependency changes. Clean Architecture boundaries are unaffected.
- **CA-002 (Money and Dates)**: Not applicable — no monetary or date fields are involved in build configuration.
- **CA-003 (Privacy and Security)**: Not applicable — no data storage or credential handling is involved. The self-contained publish model does not change the application's local-first data handling.
- **CA-004 (Network Scope)**: Not applicable — no outbound network calls are added or changed.
- **CA-005 (Async and UI)**: Not applicable — no runtime code changes. ReadyToRun may improve startup time but does not alter async/UI patterns.
- **CA-006 (Testing Impact)**: No domain, application, or infrastructure test changes required. Verification consists of build-level validation: confirming that Release publish produces correct single-file output on all target platforms (win-x64, osx-x64, osx-arm64, linux-x64).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Running a Release publish for any supported platform produces exactly one executable file (plus any platform-required companion files like `.app` bundles on macOS) with no separate `.pdb` files in the output directory.
- **SC-002**: Running a Release publish for Windows produces a ReadyToRun-compiled executable; running the same for macOS or Linux does not.
- **SC-003**: The Release-published single-file executable starts and runs correctly on all three target platforms (Windows, macOS, Linux) without missing dependency errors.
- **SC-004**: Debug builds complete in the same time (±5%) as before the change, confirming no performance regression in the development workflow.
- **SC-005**: The CI release workflow can remove at least 3 redundant `-p:` property flags per platform publish step while producing identical release artifacts.

## Assumptions

- The current release workflow in `.github/workflows/release.yml` already passes these properties via command-line `-p:` flags; this feature consolidates them into the project file for consistency and maintainability.
- The `--self-contained` CLI flag and `<SelfContained>true</SelfContained>` in the project file are functionally equivalent; the project-file approach is preferred because it serves as a single source of truth.
- ReadyToRun is intentionally Windows-only because it provides the most measurable startup benefit on Windows and is not supported or beneficial on all macOS/Linux runtime configurations.
- `PublishTrimmed` is intentionally excluded from this feature's scope — trimming is controlled separately by the CI workflow based on release versioning strategy and remains a CLI-only flag.
- The existing `Directory.Build.props` sets `TargetFramework`, `Nullable`, `TreatWarningsAsErrors`, and other global properties; this feature adds only to the desktop project's `.csproj` and does not modify shared build properties.
