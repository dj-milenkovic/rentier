# Data Model: NuGet Central Package Management

**Feature**: 041-nuget-central-package-management  
**Date**: 2025-07-15

## Overview

This feature is a build-system refactoring with **no runtime data model changes**. No entities, value objects, database tables, or domain state transitions are affected. The "data model" for this feature is the MSBuild file structure.

## MSBuild File Model

### New File: `Directory.Packages.props`

**Location**: Repository root (`F:\Projects\Rentier\rentier\Directory.Packages.props`)  
**Purpose**: Single source of truth for all NuGet package versions  
**Format**: MSBuild XML props file

**Structure**:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- One <PackageVersion> per distinct package -->
    <PackageVersion Include="{PackageName}" Version="{ExactVersion}" />
  </ItemGroup>
</Project>
```

**Fields per entry**:

| Attribute | Type | Description | Validation |
|-----------|------|-------------|------------|
| `Include` | string | NuGet package ID (case-insensitive) | Must match a `<PackageReference>` in at least one .csproj |
| `Version` | string | Exact semantic version | Must be `X.Y.Z` or `X.Y.Z.W` — no wildcards (`*`) allowed |

### Modified Files: `.csproj` (8 of 9)

**Change**: Remove `Version` attribute from all `<PackageReference>` elements.

**Before**:
```xml
<PackageReference Include="xunit" Version="2.*" />
```

**After**:
```xml
<PackageReference Include="xunit" />
```

**Preserved attributes** (remain on `<PackageReference>`):
- `PrivateAssets`
- `IncludeAssets`
- `ExcludeAssets`

### Unmodified Files

| File | Reason |
|------|--------|
| `Directory.Build.props` | FR-009: Must not be modified |
| `src/Rentier.Domain/Rentier.Domain.csproj` | Has zero package references |

## Package Inventory (31 distinct packages)

### Source Project Packages

| Package | Used By |
|---------|---------|
| Ace4896.DBus.Services.Secrets | Infrastructure |
| AngleSharp | Infrastructure |
| Avalonia | Desktop, UnitTests |
| Avalonia.Controls.DataGrid | Desktop |
| Avalonia.Desktop | Desktop |
| Avalonia.Fonts.Inter | Desktop |
| Avalonia.Headless | UnitTests |
| Avalonia.Headless.XUnit | UnitTests |
| Avalonia.ReactiveUI | Desktop, UnitTests |
| Avalonia.Themes.Fluent | Desktop |
| CommunityToolkit.Mvvm | Desktop |
| CsvHelper | Infrastructure |
| MailKit | Infrastructure |
| Microsoft.EntityFrameworkCore.Design | Infrastructure, Desktop |
| Microsoft.EntityFrameworkCore.Sqlite | Infrastructure, Scenarios.Tests, Infrastructure.Tests |
| Microsoft.EntityFrameworkCore.Tools | Infrastructure |
| Microsoft.Extensions.DependencyInjection | Desktop, Scenarios.Tests, UnitTests |
| Microsoft.Extensions.Http | Infrastructure |
| Microsoft.Extensions.Logging.Abstractions | Application |
| ReactiveUI | Desktop |

### Test-Only Packages

| Package | Used By |
|---------|---------|
| coverlet.collector | E2E.Tests, Scenarios.Tests, Infrastructure.Tests, UnitTests |
| FlaUI.Core | E2E.Tests |
| FlaUI.UIA3 | E2E.Tests |
| FluentAssertions | E2E.Tests, Scenarios.Tests, Tests.Common, Infrastructure.Tests, UnitTests |
| FsCheck.Xunit | UnitTests |
| Microsoft.EntityFrameworkCore.InMemory | Infrastructure.Tests |
| Microsoft.NET.Test.Sdk | E2E.Tests, Scenarios.Tests, Infrastructure.Tests, UnitTests |
| NSubstitute | Scenarios.Tests, Tests.Common, Infrastructure.Tests, UnitTests |
| Verify.Xunit | Infrastructure.Tests |
| xunit | E2E.Tests, Scenarios.Tests, Tests.Common, Infrastructure.Tests, UnitTests |
| xunit.runner.visualstudio | E2E.Tests, Scenarios.Tests, Infrastructure.Tests, UnitTests |

## Relationships & Constraints

- **One-to-many**: Each `<PackageVersion>` in `Directory.Packages.props` → multiple `<PackageReference>` in .csproj files
- **Constraint**: Every `<PackageReference>` in any .csproj MUST have a corresponding `<PackageVersion>` in `Directory.Packages.props`
- **Constraint**: No `<PackageReference>` may contain a `Version` attribute (unless using `VersionOverride`, which is not expected)
- **Constraint**: No version string in `Directory.Packages.props` may contain `*`

## State Transitions

Not applicable — this is a one-time migration with no ongoing state machine. The migration is either complete (all versions centralized) or incomplete (partial migration — invalid state that would cause build errors).
