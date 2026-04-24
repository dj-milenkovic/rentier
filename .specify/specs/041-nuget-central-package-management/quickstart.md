# Quickstart: NuGet Central Package Management

**Feature**: 041-nuget-central-package-management  
**Date**: 2025-07-15

## Prerequisites

- .NET 10 SDK installed (`dotnet --version` ≥ 10.0)
- Repository cloned at `F:\Projects\Rentier\rentier`
- Solution builds successfully before starting (`dotnet build Rentier.slnx`)

## Implementation Steps

### Step 1: Capture Current Resolved Versions

Before making any changes, record what versions the wildcards currently resolve to:

```powershell
cd F:\Projects\Rentier\rentier
dotnet restore Rentier.slnx
dotnet list Rentier.slnx package --format json > package-baseline.json
```

This baseline is used to verify the migration is a no-op.

### Step 2: Create Directory.Packages.props

Create the file at the repository root with all 31 distinct packages:

```powershell
# File: Directory.Packages.props (repo root)
# See data-model.md for the complete package inventory
```

Key rules:
- `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` must be set
- Every distinct package gets a `<PackageVersion Include="..." Version="X.Y.Z" />` entry
- Versions must be exact (no `*` wildcards)
- Pin to the latest stable version compatible with net10.0

### Step 3: Remove Version Attributes from .csproj Files

For each of the 8 projects with package references (skip `Rentier.Domain`):

**Before**:
```xml
<PackageReference Include="xunit" Version="2.*" />
```

**After**:
```xml
<PackageReference Include="xunit" />
```

Preserve all non-version attributes (`PrivateAssets`, `IncludeAssets`, `ExcludeAssets`).

### Step 4: Verify Build

```powershell
dotnet restore Rentier.slnx
dotnet build Rentier.slnx --no-restore
```

Both must complete with zero errors. `TreatWarningsAsErrors` is enabled, so any package-resolution warnings will surface as errors.

### Step 5: Run Full Test Suite

```powershell
dotnet test Rentier.slnx --no-build
```

All existing tests must pass with no regressions.

### Step 6: Update CI Cache Keys

Edit `.github/workflows/ci.yml` — update all 3 NuGet cache key entries:

**Before**:
```yaml
key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', 'Directory.Build.props') }}
```

**After**:
```yaml
key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', 'Directory.Build.props', 'Directory.Packages.props') }}
```

### Step 7: Verify No Wildcards Remain

```powershell
# Check csproj files for any remaining Version attributes with wildcards
Select-String -Path "src/**/*.csproj","tests/**/*.csproj" -Pattern 'Version="[^"]*\*' -Recurse
# Should return no matches

# Check Directory.Packages.props for wildcards
Select-String -Path "Directory.Packages.props" -Pattern '\*'
# Should return no matches
```

## Verification Checklist

- [ ] `Directory.Packages.props` exists at repo root
- [ ] All 31 packages listed with exact versions
- [ ] No `Version` attribute on any `<PackageReference>` in any .csproj
- [ ] `Directory.Build.props` is unmodified
- [ ] `dotnet build Rentier.slnx` succeeds with zero errors
- [ ] `dotnet test Rentier.slnx` — all tests pass
- [ ] CI cache keys updated in all 3 locations
- [ ] No wildcard `*` in any version string anywhere

## Common Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| `NETSDK1023: A PackageReference for 'X' was included in your project but has no version` | Missing `<PackageVersion>` in central file | Add the package to `Directory.Packages.props` |
| `NU1008: Projects that use central package version management should not define the version on the PackageReference items` | `Version` attribute not removed from csproj | Remove `Version="..."` from the `<PackageReference>` |
| Build warning about version conflict | Transitive dependency version mismatch | Check if `<CentralPackageTransitivePinningEnabled>` is needed |
| Cache not invalidated in CI | `Directory.Packages.props` not in `hashFiles()` | Update cache key in ci.yml |
