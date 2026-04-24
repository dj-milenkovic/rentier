# Research: NuGet Central Package Management

**Feature**: 041-nuget-central-package-management  
**Date**: 2025-07-15

## Research Questions & Findings

### RQ-001: How does NuGet Central Package Management (CPM) work?

**Decision**: Use `Directory.Packages.props` at the repository root with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` to centralize all package versions.

**Rationale**: CPM is the official NuGet mechanism (GA since .NET 6 SDK / NuGet 6.2) for centralizing version definitions. It uses a single `Directory.Packages.props` file where all package versions are declared via `<PackageVersion>` elements. Individual `.csproj` files retain `<PackageReference>` but drop the `Version` attribute. NuGet resolves versions from the central file at restore time.

**Alternatives considered**:
- **Directory.Build.props with version variables**: Requires manual `$(VersionVariable)` references in each csproj. More fragile, no tooling support, not a first-class NuGet feature.
- **Paket**: Third-party dependency manager. Adds tooling complexity, not aligned with standard .NET ecosystem.
- **Manual version pinning without CPM**: Pins versions in each csproj individually. Solves wildcards but doesn't centralize. Maintenance overhead remains.

---

### RQ-002: Where should Directory.Packages.props be placed?

**Decision**: Place `Directory.Packages.props` at the repository root (`F:\Projects\Rentier\rentier\Directory.Packages.props`), alongside the existing `Directory.Build.props`.

**Rationale**: NuGet walks up the directory tree from each project to find the nearest `Directory.Packages.props`. Placing it at the repo root ensures all 9 projects (4 source + 5 test) are covered by a single file. This is the standard convention and matches the existing `Directory.Build.props` placement.

**Alternatives considered**:
- **Per-folder props files**: Would allow different version policies per folder. Unnecessary complexity — the spec explicitly states no `VersionOverride` scenarios are expected.

---

### RQ-003: How to resolve wildcard versions to exact stable versions?

**Decision**: Use `dotnet list package` to capture the currently resolved versions from the lock, then pin those exact versions in `Directory.Packages.props`. For packages where the resolved version is behind the latest stable, prefer the latest stable that is compatible with net10.0.

**Rationale**: Running `dotnet list package --format json` (or parsing table output) on the current solution reveals what wildcards actually resolve to today. This ensures the migration is a no-op in terms of actual package versions used, minimizing risk. Per FR-004, versions should be pinned to the latest stable at migration time.

**Alternatives considered**:
- **Manually looking up each version on nuget.org**: Time-consuming for 31 packages. Automated resolution is more reliable.
- **Using `dotnet outdated` tool**: Useful for finding latest versions but adds a tool dependency. Standard `dotnet list package --outdated` is sufficient.

---

### RQ-004: How to handle package metadata (PrivateAssets, IncludeAssets)?

**Decision**: Package-level metadata (`PrivateAssets`, `IncludeAssets`, `ExcludeAssets`) stays on the `<PackageReference>` in individual `.csproj` files. Only the `Version` attribute moves to the central file.

**Rationale**: CPM by design separates version management (central) from usage metadata (project-level). The `<PackageReference>` element remains in project files but without `Version`. Attributes like `PrivateAssets="All"` (used on `coverlet.collector`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.Tools`) are project-specific concerns and must remain local.

**Packages with metadata to preserve**:
- `coverlet.collector` — `PrivateAssets="All"` + `IncludeAssets` in test projects
- `Microsoft.EntityFrameworkCore.Design` — likely has `PrivateAssets="All"`
- `Microsoft.EntityFrameworkCore.Tools` — likely has `PrivateAssets="All"`

**Alternatives considered**: None — CPM design mandates this separation.

---

### RQ-005: What CI/CD changes are required?

**Decision**: Update the NuGet cache key in `.github/workflows/ci.yml` to include `Directory.Packages.props` in the `hashFiles()` glob.

**Rationale**: The current cache key is:
```yaml
key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', 'Directory.Build.props') }}
```
This must become:
```yaml
key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', 'Directory.Build.props', 'Directory.Packages.props') }}
```
Without this change, modifying package versions in the central file would not invalidate the NuGet cache, potentially causing stale dependency resolution in CI. This appears in 3 locations in `ci.yml` (lines 38, 84, 291).

**Alternatives considered**:
- **Using `**/*.props` glob**: Would capture all props files including any future additions. However, it's less explicit and could cause unnecessary cache invalidation from unrelated props file changes.

---

### RQ-006: Does Rentier.Domain need any entries?

**Decision**: No entries needed for `Rentier.Domain` in `Directory.Packages.props`.

**Rationale**: Audit confirms `Rentier.Domain` has zero `<PackageReference>` elements. Per the constitution, Domain MUST NOT reference any NuGet package that performs I/O, and the project correctly has no dependencies. No central version entry is needed for packages that aren't referenced.

---

### RQ-007: Is VersionOverride needed for any project?

**Decision**: No `VersionOverride` is needed.

**Rationale**: The spec assumes (and the audit confirms) that no project requires a different version of any shared package. All projects sharing a package (e.g., `xunit` in 5 test projects) can use the same version. If a future need arises, CPM supports `<PackageReference Include="X" VersionOverride="Y.Z.W" />` on individual references.

---

### RQ-008: What is the migration order to minimize risk?

**Decision**: Migrate in this order:
1. Create `Directory.Packages.props` with all 31 packages and exact versions
2. Remove `Version` attributes from all `.csproj` files (8 projects, skip Domain)
3. Verify build (`dotnet build Rentier.slnx`)
4. Run full test suite (`dotnet test Rentier.slnx`)
5. Update CI cache keys
6. Verify no wildcards remain (automated check)

**Rationale**: Creating the central file first and then removing versions from csproj files is the standard migration path. NuGet will error if a `<PackageReference>` has no version and no central definition exists, providing a safety net. Building and testing after the migration confirms no behavioral change.

**Alternatives considered**:
- **Project-by-project migration**: Migrate one csproj at a time. Safer for very large solutions but unnecessary for 9 projects. Adds complexity with partial migration state.
- **Using `dotnet nuget centralize` tool**: Community tool that automates the migration. Could be used but adds a tool dependency and obscures what changes are being made.

---

## Inventory Summary

| Metric | Value |
|--------|-------|
| Total .csproj files | 9 |
| Projects with packages | 8 (Domain excluded) |
| Distinct NuGet packages | 31 |
| Package references with wildcards | 50 of 51 (98%) |
| Only non-wildcard | `Ace4896.DBus.Services.Secrets` at `1.5.0` |
| CI cache locations to update | 3 |
| Directory.Build.props modification | None (FR-009) |

All NEEDS CLARIFICATION items are resolved.
