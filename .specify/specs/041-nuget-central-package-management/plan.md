# Implementation Plan: NuGet Central Package Management

**Branch**: `041-nuget-central-package-management` | **Date**: 2025-07-15 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `.specify/specs/041-nuget-central-package-management/spec.md`

## Summary

Migrate the Rentier solution from per-project wildcard NuGet versions to NuGet Central Package Management (CPM). Currently, 50 of 51 package references across 8 projects use wildcard versions (e.g., `11.*`, `20.*`, `10.0.*`). This plan introduces a single `Directory.Packages.props` file at the repository root that pins all 31 distinct packages to exact stable versions, removes `Version` attributes from all `.csproj` files, and updates CI cache keys to include the new file.

## Technical Context

**Language/Version**: C# 14 / .NET 10.0  
**Primary Dependencies**: NuGet SDK (built into .NET SDK) — no additional tooling required  
**Storage**: N/A — build-system-only change, no runtime data storage affected  
**Testing**: xUnit + FluentAssertions + NSubstitute (existing suite serves as regression gate)  
**Target Platform**: Windows, macOS, Linux (cross-platform desktop via Avalonia)  
**Project Type**: Desktop application (Clean Architecture, 4 source + 5 test projects)  
**Performance Goals**: N/A — no runtime impact  
**Constraints**: `Directory.Build.props` must not be modified (FR-009). Zero build errors/warnings post-migration.  
**Scale/Scope**: 9 projects, 31 distinct packages, 51 total package references, 3 CI cache locations

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - ✅ No source code changes. No project references added or removed. Layer boundaries unaffected.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - ✅ Not applicable — no runtime code changes.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - ✅ Not applicable — no runtime code changes.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - ✅ Not applicable — package versions are non-sensitive build metadata.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - ✅ Not applicable — NuGet restore uses existing package sources, no new endpoints.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - ✅ Not applicable — no runtime code changes.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - ✅ No new tests required. Existing test suite is the regression gate (FR-008). No coverage impact.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - ✅ Spec exists at `.specify/specs/041-nuget-central-package-management/spec.md`.

**Gate result: PASS** — All checks satisfied. This is a build-system-only refactoring with zero runtime impact.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/041-nuget-central-package-management/
├── spec.md               # Feature specification
├── plan.md               # This file
├── research.md           # Phase 0: CPM research & decisions
├── data-model.md         # Phase 1: MSBuild file model & package inventory
├── quickstart.md         # Phase 1: Step-by-step implementation guide
├── contracts/
│   └── README.md         # Phase 1: No external contracts (build-system only)
├── checklists/
│   └── requirements.md   # Spec quality checklist
└── tasks.md              # Phase 2: Task breakdown (created by /speckit.tasks)
```

### Source Code (repository root)

```text
# Files CREATED by this feature
Directory.Packages.props          # NEW — central package version file

# Files MODIFIED by this feature (Version attributes removed)
src/Rentier.Application/Rentier.Application.csproj
src/Rentier.Desktop/Rentier.Desktop.csproj
src/Rentier.Infrastructure/Rentier.Infrastructure.csproj
tests/Rentier.E2E.Tests/Rentier.E2E.Tests.csproj
tests/Rentier.Infrastructure.Tests/Rentier.Infrastructure.Tests.csproj
tests/Rentier.Scenarios.Tests/Rentier.Scenarios.Tests.csproj
tests/Rentier.Tests.Common/Rentier.Tests.Common.csproj
tests/Rentier.UnitTests/Rentier.UnitTests.csproj

# Files MODIFIED for CI (cache key update)
.github/workflows/ci.yml          # 3 cache key locations

# Files NOT MODIFIED
Directory.Build.props              # FR-009: Must not be modified
src/Rentier.Domain/Rentier.Domain.csproj  # No package references
```

**Structure Decision**: No new source directories. One new MSBuild file at repo root. Eight existing `.csproj` files modified (attribute removal only). One CI workflow file updated.

## Design Decisions

### D-001: Central File Location

**Choice**: Repository root (alongside `Directory.Build.props`)  
**Rationale**: NuGet walks up the directory tree. Root placement covers all projects. Standard convention.

### D-002: Version Pinning Strategy

**Choice**: Pin to latest stable version at migration time (FR-004)  
**Rationale**: Resolves wildcards to deterministic versions. Latest stable ensures security patches and bug fixes are current.

### D-003: No VersionOverride

**Choice**: No `VersionOverride` attribute on any `<PackageReference>`  
**Rationale**: Audit confirms no project needs a different version of any shared package. Keeps the model simple.

### D-004: No Transitive Pinning

**Choice**: Do not enable `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>` initially  
**Rationale**: Transitive pinning is an advanced feature that can cause unexpected version conflicts. The current solution builds without it. Can be added later if transitive dependency issues arise.

### D-005: Package Grouping in Central File

**Choice**: Group packages by category with XML comments (Avalonia, EF Core, Testing, etc.)  
**Rationale**: Improves readability and maintenance. When updating Avalonia, all Avalonia packages are visually grouped together.

### D-006: CI Cache Key Update

**Choice**: Add `Directory.Packages.props` to existing `hashFiles()` calls  
**Rationale**: Version changes in the central file must invalidate the NuGet cache. Without this, CI could use stale cached packages.

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Version incompatibility between pinned packages | Low | High (build failure) | Build + test verification before merge |
| Transitive dependency conflict | Low | Medium (build warning → error due to TreatWarningsAsErrors) | Review warnings carefully; add transitive pinning if needed |
| CI cache stale after migration | Medium | Low (slower CI, not incorrect) | Update cache keys in same PR |
| Missing package in central file | Low | High (build failure) | NuGet surfaces clear error message; easy to fix |
| Future contributors add Version to csproj | Medium | Low (NuGet errors on restore) | NuGet NU1008 error catches this automatically |

## Complexity Tracking

> No Constitution Check violations exist. No complexity justification needed.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| None | — | — |

## Implementation Phases

### Phase A: Create Central Version File
1. Resolve current wildcard versions to exact latest stable versions for all 31 packages
2. Create `Directory.Packages.props` at repo root with `ManagePackageVersionsCentrally` enabled
3. Add all 31 `<PackageVersion>` entries grouped by category

### Phase B: Migrate Project Files
1. Remove `Version` attribute from all `<PackageReference>` elements in 8 project files
2. Preserve `PrivateAssets`, `IncludeAssets`, `ExcludeAssets` attributes
3. Skip `Rentier.Domain` (no package references)

### Phase C: Verify & Update CI
1. `dotnet restore Rentier.slnx` — must succeed
2. `dotnet build Rentier.slnx` — zero errors, zero new warnings
3. `dotnet test Rentier.slnx` — all tests pass
4. Update 3 NuGet cache keys in `.github/workflows/ci.yml`
5. Automated wildcard check: no `*` in any version string

### Phase D: Validation
1. Verify SC-001 through SC-006 (success criteria from spec)
2. Confirm `Directory.Build.props` is unmodified (FR-009)
3. Confirm `Rentier.Domain.csproj` is unmodified
