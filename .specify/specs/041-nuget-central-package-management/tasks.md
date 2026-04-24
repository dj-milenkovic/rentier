# Tasks: NuGet Central Package Management

**Feature**: `041-nuget-central-package-management`  
**Input**: Design documents from `.specify/specs/041-nuget-central-package-management/`  
**Branch**: `041-nuget-central-package-management`  
**Tests**: No new test tasks — existing test suite is the regression gate (CA-006, FR-008)

**Organization**: Tasks follow the migration order from research.md (RQ-008) and are tagged by user story to enable independent verification at each checkpoint.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on each other)
- **[Story]**: User story this task belongs to (US1, US2, US3)
- Exact file paths are included in every task description

---

## Phase 1: Setup — Capture Baseline

**Purpose**: Record current resolved package versions before making any changes, so the migration can be verified as a no-op in terms of actual resolved versions.

- [ ] T001 Run `dotnet restore Rentier.slnx` then `dotnet list Rentier.slnx package --format json` and record the output as the pre-migration package baseline for comparison after migration

**Checkpoint**: Baseline captured — safe to begin central file creation.

---

## Phase 2: User Story 1 — Centralize All Package Versions (Priority: P1) 🎯 MVP

**Goal**: Create `Directory.Packages.props` at the repository root and remove all `Version` attributes from the 8 project files that contain package references.

**Independent Test**: Run `dotnet restore Rentier.slnx && dotnet build Rentier.slnx` — must succeed with zero errors. Then run `Select-String -Path "src/**/*.csproj","tests/**/*.csproj" -Pattern 'Version="[^"]*\*' -Recurse` — must return no matches.

### Create the Central Package Version File

- [ ] T002 [US1] Create `Directory.Packages.props` at the repository root with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and all 31 distinct packages listed under grouped XML comments (Avalonia group, EF Core group, Extensions group, Source packages group, Test-only packages group), each with an exact pinned stable version compatible with net10.0 — pin to the latest stable version available at migration time using `dotnet list Rentier.slnx package --outdated` or nuget.org lookup. Packages to include:
  - **Avalonia group**: `Avalonia`, `Avalonia.Controls.DataGrid`, `Avalonia.Desktop`, `Avalonia.Fonts.Inter`, `Avalonia.Headless`, `Avalonia.Headless.XUnit`, `Avalonia.ReactiveUI`, `Avalonia.Themes.Fluent`
  - **EF Core group**: `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.InMemory`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Tools`
  - **Extensions group**: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Logging.Abstractions`
  - **Source packages group**: `Ace4896.DBus.Services.Secrets`, `AngleSharp`, `CommunityToolkit.Mvvm`, `CsvHelper`, `MailKit`, `ReactiveUI`
  - **Test-only packages group**: `coverlet.collector`, `FlaUI.Core`, `FlaUI.UIA3`, `FluentAssertions`, `FsCheck.Xunit`, `Microsoft.NET.Test.Sdk`, `NSubstitute`, `Verify.Xunit`, `xunit`, `xunit.runner.visualstudio`

### Remove Version Attributes from .csproj Files

All 8 tasks below are [P] — they modify different files with no shared state.

- [ ] T003 [P] [US1] Remove all `Version="..."` attributes from `<PackageReference>` elements in `src/Rentier.Application/Rentier.Application.csproj`; preserve `PrivateAssets`, `IncludeAssets`, and `ExcludeAssets` attributes unchanged
- [ ] T004 [P] [US1] Remove all `Version="..."` attributes from `<PackageReference>` elements in `src/Rentier.Desktop/Rentier.Desktop.csproj`; preserve `PrivateAssets`, `IncludeAssets`, and `ExcludeAssets` attributes unchanged
- [ ] T005 [P] [US1] Remove all `Version="..."` attributes from `<PackageReference>` elements in `src/Rentier.Infrastructure/Rentier.Infrastructure.csproj`; preserve `PrivateAssets`, `IncludeAssets`, and `ExcludeAssets` attributes unchanged (notably `Microsoft.EntityFrameworkCore.Design` and `Microsoft.EntityFrameworkCore.Tools` have `PrivateAssets="All"`)
- [ ] T006 [P] [US1] Remove all `Version="..."` attributes from `<PackageReference>` elements in `tests/Rentier.E2E.Tests/Rentier.E2E.Tests.csproj`; preserve `PrivateAssets`, `IncludeAssets`, and `ExcludeAssets` attributes on `coverlet.collector` unchanged
- [ ] T007 [P] [US1] Remove all `Version="..."` attributes from `<PackageReference>` elements in `tests/Rentier.Infrastructure.Tests/Rentier.Infrastructure.Tests.csproj`; preserve `PrivateAssets`, `IncludeAssets`, and `ExcludeAssets` attributes on `coverlet.collector` unchanged
- [ ] T008 [P] [US1] Remove all `Version="..."` attributes from `<PackageReference>` elements in `tests/Rentier.Scenarios.Tests/Rentier.Scenarios.Tests.csproj`; preserve `PrivateAssets`, `IncludeAssets`, and `ExcludeAssets` attributes on `coverlet.collector` unchanged
- [ ] T009 [P] [US1] Remove all `Version="..."` attributes from `<PackageReference>` elements in `tests/Rentier.Tests.Common/Rentier.Tests.Common.csproj`; preserve all metadata attributes unchanged
- [ ] T010 [P] [US1] Remove all `Version="..."` attributes from `<PackageReference>` elements in `tests/Rentier.UnitTests/Rentier.UnitTests.csproj`; preserve `PrivateAssets`, `IncludeAssets`, and `ExcludeAssets` attributes on `coverlet.collector` unchanged

**Checkpoint**: US1 complete — `Directory.Packages.props` created, all 8 project files migrated. Proceed to build verification (US2).

---

## Phase 3: User Story 2 — Maintain Build Integrity (Priority: P1)

**Goal**: Confirm the solution builds and the full test suite passes after the migration, verifying zero behavioral regressions.

**Independent Test**: Run `dotnet restore Rentier.slnx`, then `dotnet build Rentier.slnx --no-restore`, then `dotnet test Rentier.slnx --no-build` — all three must succeed with zero errors and zero new failures. Compare resolved package versions against the baseline from T001 to confirm versions are equivalent.

- [ ] T011 [US2] Run `dotnet restore Rentier.slnx` and confirm it succeeds with zero errors; if NuGet reports `NETSDK1023` (missing central version) for any package, add the missing package to `Directory.Packages.props` (file: `Directory.Packages.props`)
- [ ] T012 [US2] Run `dotnet build Rentier.slnx --no-restore` and confirm it succeeds with zero errors and zero new warnings; `TreatWarningsAsErrors` is enabled so any package-resolution warning surfaces as an error — resolve all issues before proceeding (files: any `.csproj` or `Directory.Packages.props` as needed)
- [ ] T013 [US2] Run `dotnet test Rentier.slnx --no-build` and confirm the full test suite passes with no new failures; compare pass/fail counts against the pre-migration baseline to confirm no regressions

**Checkpoint**: US2 complete — build succeeds, all tests pass. Migration is verified as a no-op. Proceed to CI update (US3).

---

## Phase 4: User Story 3 — Simplify Future Package Updates (Priority: P2)

**Goal**: Update the CI NuGet cache keys so that future changes to `Directory.Packages.props` correctly invalidate the cache, ensuring the CI pipeline stays consistent with the central version file.

**Independent Test**: Inspect `.github/workflows/ci.yml` and confirm that all 3 NuGet cache key `hashFiles()` expressions include `'Directory.Packages.props'`. No CI run is needed locally — the change can be verified by static inspection.

- [ ] T014 [US3] Update all 3 NuGet cache key `hashFiles()` expressions in `.github/workflows/ci.yml` (at lines ~38, ~84, and ~291) from `hashFiles('**/*.csproj', 'Directory.Build.props')` to `hashFiles('**/*.csproj', 'Directory.Build.props', 'Directory.Packages.props')` — do not change any other lines in the file

**Checkpoint**: US3 complete — CI cache keys updated. Future package version changes in the central file will correctly invalidate the NuGet cache.

---

## Phase 5: Polish & Validation

**Purpose**: Automated verification that all success criteria (SC-001 through SC-006) are met and the constraint files are unmodified.

- [ ] T015 Run `Select-String -Path "src/**/*.csproj","tests/**/*.csproj" -Pattern 'Version="[^"]*\*' -Recurse` and confirm zero matches (SC-001: no wildcard versions remain in project files)
- [ ] T016 Run `Select-String -Path "Directory.Packages.props" -Pattern '\*'` and confirm zero matches (SC-001: no wildcard versions in central file)
- [ ] T017 Run `Select-String -Path "src/**/*.csproj","tests/**/*.csproj" -Pattern 'Version=' -Recurse` and confirm zero matches (SC-006: no `Version` attribute on any `<PackageReference>`)
- [ ] T018 Run `(Get-Content "Directory.Packages.props" | Select-String '<PackageVersion').Count` and confirm the count equals 31 (SC-002: all 31 distinct packages are defined centrally)
- [ ] T019 [P] Verify `Directory.Build.props` is unmodified by comparing its hash or content against the pre-migration state (FR-009 constraint: must not be modified)
- [ ] T020 [P] Verify `src/Rentier.Domain/Rentier.Domain.csproj` is unmodified and still contains zero `<PackageReference>` elements (Domain has no package dependencies)
- [ ] T021 Run `dotnet list Rentier.slnx package --format json` and compare resolved versions against the baseline captured in T001; confirm all packages resolve to equivalent or newer stable versions with no unexpected version changes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — run immediately
- **Phase 2 (US1)**: Depends on Phase 1 (T001 baseline); T002 must complete before T003–T010 (central file must exist before removing versions from csproj files)
- **Phase 3 (US2)**: Depends on Phase 2 completion (all csproj files migrated + central file created)
- **Phase 4 (US3)**: Depends on Phase 3 completion (build + tests confirmed passing); can technically be done in parallel with Phase 3 since it modifies a different file (`ci.yml`), but sequencing it after confirms full integrity first
- **Phase 5 (Polish)**: Depends on all preceding phases completing

### Within Phase 2 (US1)

- **T002 first**: Create `Directory.Packages.props` before removing versions from any csproj — NuGet will error on restore if versions are removed without the central file existing
- **T003–T010 in parallel**: All 8 csproj edits are independent (different files, no cross-dependencies)

### Parallel Opportunities

```powershell
# Phase 2 — after T002 completes, all csproj edits can run simultaneously:
Task T003: Remove versions from src/Rentier.Application/Rentier.Application.csproj
Task T004: Remove versions from src/Rentier.Desktop/Rentier.Desktop.csproj
Task T005: Remove versions from src/Rentier.Infrastructure/Rentier.Infrastructure.csproj
Task T006: Remove versions from tests/Rentier.E2E.Tests/Rentier.E2E.Tests.csproj
Task T007: Remove versions from tests/Rentier.Infrastructure.Tests/Rentier.Infrastructure.Tests.csproj
Task T008: Remove versions from tests/Rentier.Scenarios.Tests/Rentier.Scenarios.Tests.csproj
Task T009: Remove versions from tests/Rentier.Tests.Common/Rentier.Tests.Common.csproj
Task T010: Remove versions from tests/Rentier.UnitTests/Rentier.UnitTests.csproj

# Phase 5 — T019 and T020 can run in parallel:
Task T019: Verify Directory.Build.props unmodified
Task T020: Verify Rentier.Domain.csproj unmodified
```

---

## Implementation Strategy

### MVP (US1 + US2 only — minimum to close the DevOps finding)

1. Complete Phase 1: Capture baseline (T001)
2. Complete Phase 2: Create central file + migrate all csproj files (T002–T010)
3. Complete Phase 3: Verify build and tests (T011–T013)
4. **STOP and VALIDATE**: `dotnet build Rentier.slnx` succeeds, `dotnet test Rentier.slnx` passes
5. Open PR — the DevOps wildcard-version finding is resolved

### Full Delivery (all stories + polish)

1. MVP steps above
2. Add Phase 4: Update CI cache keys (T014) — ensures CI correctness going forward
3. Run Phase 5: All validation tasks (T015–T021) — confirms every success criterion
4. Merge — solution is fully migrated and CI is consistent

### Notes

- **No `VersionOverride` expected**: Audit confirms all projects can share the same version for every package. If the build surfaces a version conflict, check if transitive pinning (`<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>`) is needed.
- **Error guide**: `NETSDK1023` = package missing from central file → add to `Directory.Packages.props`. `NU1008` = `Version` attribute still present in a csproj → remove it.
- **`Directory.Build.props` must not be touched** — FR-009 is a hard constraint.
- **`Rentier.Domain` is skipped** — it has zero package references and needs no changes.

---

## Summary

| Metric | Value |
|--------|-------|
| Total tasks | 21 |
| Phase 1 (Setup) | 1 task |
| Phase 2 — US1 (Centralize versions) | 9 tasks (1 sequential + 8 parallel) |
| Phase 3 — US2 (Build integrity) | 3 tasks |
| Phase 4 — US3 (CI update) | 1 task |
| Phase 5 (Polish/validation) | 7 tasks |
| Parallelizable tasks | 10 (T003–T010 [P], T019–T020 [P]) |
| Files created | 1 (`Directory.Packages.props`) |
| Files modified | 9 (8 `.csproj` + `ci.yml`) |
| Files that must NOT be modified | 2 (`Directory.Build.props`, `Rentier.Domain.csproj`) |
| MVP scope | Phases 1–3 (US1 + US2) |
