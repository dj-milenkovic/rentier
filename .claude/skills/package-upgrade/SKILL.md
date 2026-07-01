---
name: package-upgrade
description: Upgrade NuGet packages to latest versions with semantic versioning awareness (major, minor, patch). Use this skill when upgrading dependencies, addressing security vulnerabilities, or modernizing package versions while managing breaking changes.
---

# NuGet Package Upgrade

This skill guides safe and strategic NuGet package upgrades following semantic versioning principles.

## Apply Foundation Skills
- #file:..\coding-standards\SKILL.md

## Critical Rules ??

1. **NEVER upgrade to pre-release versions** (alpha, beta, rc) without explicit approval
2. **NEVER upgrade major versions without user confirmation**
3. **ALWAYS present breaking changes and new features before major upgrades**
4. **ALWAYS categorize upgrades by risk level (patch/minor/major)**

## When to Use This Skill

- Upgrading NuGet packages to latest versions
- Addressing security vulnerabilities in dependencies
- Modernizing outdated packages
- Resolving version conflicts
- Preparing for .NET version upgrades

## Semantic Versioning (SemVer) Strategy

### Version Number Format
```
MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]
  |     |      |
  |     |      ?? Bug fixes (backward compatible)
  |     ????????? New features (backward compatible)
  ??????????????? Breaking changes (NOT backward compatible)
```

### Upgrade Risk Levels

| Version Change | Risk | Strategy | User Confirmation Required |
|----------------|------|----------|----------------------------|
| **Patch** (1.2.3 ? 1.2.4) | ? Low | Auto-upgrade | ? No |
| **Minor** (1.2.3 ? 1.3.0) | ?? Medium | Review changelog | ? No |
| **Major** (1.2.3 ? 2.0.0) | ?? High | Present breaking changes | ? **YES - REQUIRED** |
| **Pre-release** (1.2.3 ? 2.0.0-beta) | ?? **AVOID** | Do not upgrade | ? **YES - EXPLICIT APPROVAL** |

## Package Upgrade Process

### Step 1: Identify Outdated Packages

List all packages and their current versions:
```bash
dotnet list package --outdated
```

Example output:
```
Project `PredictiveAnalyticsManager.API` has the following updates:
   [net8.0]:
   Top-level Package      Requested   Resolved    Latest
   > AutoMapper           12.0.0      12.0.0      13.0.1
   > AWSSDK.S3            3.7.100     3.7.100     3.7.305.2
   > Npgsql               8.0.0       8.0.0       8.0.5
```

### Step 2: Categorize and Filter Upgrades

**Filter out pre-release versions:**
```bash
# Only consider stable releases
# Skip versions like: 13.0.1-beta, 2.0.0-rc1, 1.5.0-alpha
```

**Group by version change type:**

**Patch Upgrades (Safe - Auto-upgrade):**
- `Npgsql 8.0.0 ? 8.0.5` (bug fixes only)
- `AWSSDK.Batch 3.7.300.1 ? 3.7.300.5`

**Minor Upgrades (Review changelog):**
- `AutoMapper 12.0.0 ? 12.1.0` (new features, backward compatible)
- `FluentValidation 11.5.0 ? 11.9.0`

**Major Upgrades (? STOP - Require confirmation):**
- `AutoMapper 12.0.0 ? 13.0.1` (breaking changes expected)
- `Npgsql 7.0.0 ? 8.0.0` (API changes possible)

### Step 3: Present Major Upgrades for Approval

**Before upgrading major versions, ALWAYS present this information:**

```
## Major Version Upgrades Detected

The following packages have major version upgrades available. Major upgrades may contain breaking changes that require code modifications.

### AutoMapper: 12.0.0 ? 13.0.1

**What's New in v13:**
- Improved performance for large object graphs
- New projection features
- Enhanced nullable reference type support
- Better integration with dependency injection

**Potential Breaking Changes:**
- ? `ForMember` API syntax changed
- ? Profile configuration API updated
- ? Some obsolete methods removed
- ? Constructor resolution behavior changed

**Migration Guide:** https://docs.automapper.org/en/stable/13.0-Upgrade-Guide.html

**Affected Files (estimated):**
- `src/PredictiveAnalyticsManager.Application/Projects/MapperProfile.cs`
- `src/PredictiveAnalyticsManager.Application/Data/MapperProfile.cs`
- `src/PredictiveAnalyticsManager.Application/Calculations/MapperProfile.cs`

**Testing Required:**
- ? All unit tests
- ? All component tests
- ? Manual verification of mapping functionality

### Npgsql: 7.0.0 ? 8.0.0

**What's New in v8:**
- .NET 8 optimizations
- Improved JSON support
- Better connection pooling
- Performance improvements

**Potential Breaking Changes:**
- ? Default connection timeout changed
- ? Type mapping changes for UUID/JSON
- ? Connection string format updates
- ? Some deprecated APIs removed

**Migration Guide:** https://www.npgsql.org/doc/release-notes/8.0.html

**Affected Files (estimated):**
- `src/PredictiveAnalyticsManager.Infrastructure/*/Repositories/*.cs`
- `src/PredictiveAnalyticsManager.Infrastructure/*/DbContexts/*.cs`

**Testing Required:**
- ? All repository tests
- ? All component tests (database integration)
- ? Database migration verification

**Do you want to proceed with these major upgrades? (yes/no)**
**Or specify which packages to upgrade: (e.g., "only AutoMapper")**
```

### Step 4: Execute Upgrades (After Confirmation)

#### Patch Upgrades (Auto-execute)
```bash
# Update all patch versions at once
dotnet add package Npgsql --version 8.0.5
dotnet add package AWSSDK.S3 --version 3.7.305.2
```

**Testing:** Run unit tests only
```bash
dotnet test src/PredictiveAnalyticsManager.UnitTests
```

#### Minor Upgrades (Auto-execute with changelog review)
```bash
# Update one package
dotnet add package AutoMapper --version 12.1.0

# Review changelog
# Check release notes at: https://github.com/AutoMapper/AutoMapper/releases

# Test thoroughly
dotnet test
```

**Testing:** Run unit + component tests

#### Major Upgrades (Only after user confirmation)
```bash
# ?? WAIT FOR USER CONFIRMATION BEFORE EXECUTING

# Create a feature branch
git checkout -b upgrade/automapper-13

# Update package
dotnet add package AutoMapper --version 13.0.1

# Review breaking changes
# Check migration guide: https://docs.automapper.org/en/stable/

# Fix compilation errors
dotnet build

# Update code for breaking changes
# (See Breaking Changes section below)

# Test extensively
dotnet test
```

**Testing:** Full regression testing (unit + component + functional tests)

## Pre-Release Version Handling

### Rule: Avoid Pre-Release Versions

**Pre-release indicators:**
- `-alpha` (e.g., `2.0.0-alpha.1`)
- `-beta` (e.g., `2.0.0-beta.3`)
- `-rc` (e.g., `2.0.0-rc.1`)
- `-preview` (e.g., `2.0.0-preview.5`)

**Default behavior:**
```bash
# ? SKIP pre-release versions
AutoMapper 12.0.0 ? 13.0.0-beta.1 (SKIP - pre-release)

# ? Only suggest stable releases
AutoMapper 12.0.0 ? 13.0.1 (OK - stable release)
```

**When pre-release is the only option:**
```
?? Warning: Package X only has pre-release versions available (X.Y.Z-beta).

Pre-release versions are not recommended for production environments.

Options:
1. Stay on current stable version (recommended)
2. Upgrade to pre-release (requires explicit approval)
3. Consider alternative packages

Do you want to proceed with pre-release upgrade? (yes/no)
```

## Major Version Upgrade Approval Process

### Step 1: Analyze Breaking Changes

For each major version upgrade, research and present:

1. **Changelog review** - Read GitHub releases or official documentation
2. **Breaking changes list** - Identify specific breaking changes
3. **Migration guide** - Find official migration documentation
4. **Affected code** - Estimate which files will be affected
5. **New features** - Highlight valuable new capabilities

### Step 2: Present to User

**Template for major upgrade presentation:**

```markdown
## Major Upgrade: [Package Name] [Old Version] ? [New Version]

### ?? Package Information
- **Current Version:** X.Y.Z
- **Latest Version:** A.B.C
- **Release Date:** [Date]
- **Stability:** Stable / Pre-release

### ? What's New
- [List new features from changelog]
- [Performance improvements]
- [New capabilities]

### ?? Breaking Changes
- ? [Breaking change 1 - description]
- ? [Breaking change 2 - description]
- ? [Breaking change 3 - description]

### ?? Migration Effort
- **Estimated Files Affected:** [Number]
- **Complexity:** Low / Medium / High
- **Code Changes Required:** Yes / No

### ?? Resources
- Release Notes: [URL]
- Migration Guide: [URL]
- GitHub Issues: [URL]

### ?? Testing Requirements
- [ ] Unit tests
- [ ] Component tests
- [ ] Functional tests
- [ ] Manual testing on staging

### ?? Recommendation
[Upgrade now / Delay until [reason] / Skip this version]

**Proceed with this upgrade? (yes/no)**
```

### Step 3: Wait for Confirmation

**DO NOT execute major upgrades without explicit user approval.**

**User responses:**
- `yes` / `proceed` / `upgrade` ? Execute upgrade
- `no` / `skip` ? Skip this package
- `only [package]` ? Upgrade only specified package
- `more info` ? Provide additional details

## Handling Breaking Changes

### Step 1: Identify Breaking Changes
After upgrading, compile the project:
```bash
dotnet build
```

Common breaking change indicators:
- ? Compilation errors
- ? Obsolete warnings
- ? Changed method signatures
- ? Removed APIs

### Step 2: Review Migration Guide
Check package documentation:
- GitHub releases page
- Official documentation
- Migration guides
- Breaking changes document

### Step 3: Update Code

#### Example: AutoMapper 12 ? 13 Breaking Changes

**Breaking Change:** `ForMember` syntax changed

**Before (v12):**
```csharp
public class ProjectMappingProfile : Profile
{
    public ProjectMappingProfile()
    {
        CreateMap<Project, ProjectDto>()
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy));
    }
}
```

**After (v13):**
```csharp
public class ProjectMappingProfile : Profile
{
    public ProjectMappingProfile()
    {
        CreateMap<Project, ProjectDto>()
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy)); // May require adjustment
    }
}
```

Check AutoMapper v13 migration guide for specific changes.

### Step 4: Verify Tests Pass
```bash
# Run all tests
dotnet test

# If tests fail, fix one layer at a time:
dotnet test src/PredictiveAnalyticsManager.UnitTests
dotnet test src/PredictiveAnalyticsManager.ComponentTests
dotnet test src/PredictiveAnalyticsManager.FunctionalTests
```

## AWS SDK Package Upgrades

### Special Considerations for AWS SDK

AWS SDK packages are released frequently. Follow these rules:

**AWS SDK Versioning:**
- `AWSSDK.S3 3.7.305.2` ? `3.7.305.5` (Patch - safe)
- `AWSSDK.S3 3.7.305.2` ? `3.7.310.0` (Minor - review)
- `AWSSDK.S3 3.7.305.2` ? `4.0.0.0` (Major - **REQUIRES CONFIRMATION**)

**AWS SDK Upgrade Pattern:**
```bash
# Check outdated AWS packages
dotnet list package --outdated | Select-String "AWSSDK"

# Patch/Minor: Upgrade AWS SDK packages together (they share version compatibility)
dotnet add package AWSSDK.S3 --version 3.7.310.0
dotnet add package AWSSDK.Batch --version 3.7.310.0
dotnet add package AWSSDK.SecurityToken --version 3.7.310.0
```

**AWS SDK Major Version (v3 ? v4) - REQUIRES CONFIRMATION:**

```markdown
## Major Upgrade: AWS SDK v3 ? v4

### ?? Breaking Changes
- ? All methods are async by default (no more sync overloads)
- ? Client factory pattern changed
- ? Credential handling updated
- ? Request/response model changes
- ? Different dependency injection setup

### Migration Effort
- **Estimated Files Affected:** 15+ (all AWS service usages)
- **Complexity:** High
- **Estimated Time:** 2-3 days

### Recommendation
?? **Major effort required.** Consider delaying until dedicated sprint.

**Proceed with AWS SDK v4 upgrade? (yes/no)**
```

## Upgrade Workflow by Type

### Patch Upgrades (Auto-execute)

**Execution:**
```bash
# Safe to auto-upgrade
dotnet add package Npgsql --version 8.0.5
dotnet add package AWSSDK.S3 --version 3.7.305.2

# Minimal testing
dotnet test src/PredictiveAnalyticsManager.UnitTests
```

**Report to user:**
```
? Patch upgrades completed successfully:
- Npgsql: 8.0.0 ? 8.0.5 (bug fixes)
- AWSSDK.S3: 3.7.100 ? 3.7.305.2 (improvements)

Testing: Unit tests passed ?
```

### Minor Upgrades (Auto-execute with info)

**Execution:**
```bash
# Safe to auto-upgrade
dotnet add package FluentValidation --version 11.9.0

# Thorough testing
dotnet test src/PredictiveAnalyticsManager.UnitTests
dotnet test src/PredictiveAnalyticsManager.ComponentTests
```

**Report to user:**
```
? Minor upgrades completed:
- FluentValidation: 11.5.0 ? 11.9.0

New features in 11.9.0:
- Improved async validation support
- New built-in validators
- Better error messages

Testing: Unit + component tests passed ?
```

### Major Upgrades (Present info, wait for confirmation)

**STOP and present analysis:**

```markdown
## ?? Major Version Upgrades Available

The following packages have major version updates. These may contain breaking changes.

### 1. AutoMapper: 12.0.0 ? 13.0.1

**Release Date:** December 2024
**Stability:** ? Stable

#### ? What's New in v13
- Improved performance for large object graphs (30% faster)
- Enhanced nullable support
- C# 12 features

#### ?? Breaking Changes
1. **ForMember API updated**
   - Impact: All mapping profiles may need updates
   - Example: `opt.MapFrom()` syntax changed
   
2. **Profile registration changed**
   - Impact: Dependency injection setup may need adjustment
   - Example: `AddAutoMapper()` configuration different

3. **Removed obsolete APIs**
   - `CreateMap<T>().ConvertUsing()` ? Use `ConstructUsing()`
   - `ResolveUsing()` ? Use `MapFrom()`

#### ?? Affected Files (Estimated)
- `src/PredictiveAnalyticsManager.Application/Projects/MapperProfile.cs`
- `src/PredictiveAnalyticsManager.Application/Data/MapperProfile.cs`
- `src/PredictiveAnalyticsManager.Application/Calculations/MapperProfile.cs`
- `src/PredictiveAnalyticsManager.Application/Models/MapperProfile.cs`
- **Total:** ~4-6 files

#### ?? Migration Effort
- **Complexity:** Medium
- **Estimated Time:** 2-4 hours
- **Code Changes:** Update mapping profiles, fix compilation errors
- **Testing:** Full regression testing required

#### ?? Resources
- Release Notes: https://github.com/AutoMapper/AutoMapper/releases/tag/v13.0.1
- Migration Guide: https://docs.automapper.org/en/stable/13.0-Upgrade-Guide.html
- Breaking Changes: https://github.com/AutoMapper/AutoMapper/blob/master/docs/13.0-Upgrade-Guide.md

#### ?? Recommendation
? **Recommended** - Stable release with performance improvements. Breaking changes are manageable.

### 2. Npgsql: 7.0.6 ? 8.0.5

**Release Date:** November 2024
**Stability:** ? Stable

#### ? What's New in v8
- .NET 8 optimizations and performance improvements
- Improved JSON support (System.Text.Json)
- Enhanced connection pooling
- Better async performance
- Native AOT support

#### ?? Breaking Changes
1. **Type mapping changes**
   - Impact: UUID and JSON queries may need adjustments
   - Example: Explicit type casting may be required

2. **Connection pooling behavior**
   - Impact: Connection string parameters changed
   - Example: Some pooling settings renamed

3. **Default timeout values**
   - Impact: Some operations may timeout differently
   - Example: CommandTimeout default changed from 30s to 20s

#### ?? Affected Files (Estimated)
- All repository files: `src/PredictiveAnalyticsManager.Infrastructure/*/Repositories/*.cs`
- Database contexts: `src/PredictiveAnalyticsManager.Infrastructure/*/DbContexts/*.cs`
- **Total:** ~15-20 files

#### ?? Migration Effort
- **Complexity:** Medium-High
- **Estimated Time:** 4-6 hours
- **Code Changes:** Update type mappings, connection strings, query syntax
- **Testing:** Full database integration testing required

#### ?? Resources
- Release Notes: https://www.npgsql.org/doc/release-notes/8.0.html
- Migration Guide: https://www.npgsql.org/doc/migration/8.0.html

#### ?? Recommendation
?? **Consider carefully** - Significant changes to database layer. Recommend scheduling in dedicated sprint.

**Which major upgrades do you want to proceed with?**
Type: 'all' to upgrade all packages
Type: package names (e.g., 'AutoMapper') to upgrade specific packages
Type: 'skip' to skip all major upgrades for now
```

## Common Package Upgrade Scenarios

### Scenario 1: Security Vulnerability Fix (Bypass confirmation if critical)

**Context:** Security scan reports critical CVE vulnerability

```bash
# Example: Npgsql has critical CVE vulnerability
# Current: Npgsql 8.0.0
# Fixed in: Npgsql 8.0.3 (patch version)

# ? Auto-upgrade immediately (patch version - no confirmation needed)
dotnet add package Npgsql --version 8.0.3

# Test and deploy ASAP
dotnet test
```

**If fix is in major version:**
```markdown
?? SECURITY ALERT: Critical CVE in Npgsql 7.0.6

**Vulnerability:** CVE-2024-XXXXX (SQL Injection)
**Severity:** Critical (CVSS 9.8)
**Fixed in:** Npgsql 8.0.0 (MAJOR version upgrade)

**Immediate Action Required:**
This is a critical security vulnerability. Despite being a major version upgrade, immediate action is recommended.

Breaking changes in v8.0.0:
- [List breaking changes]

Proceed with emergency upgrade? (yes/no)
```

### Scenario 2: Feature Enhancement

**Context:** Team needs new AutoMapper feature

```bash
# Current: AutoMapper 12.0.0
# Feature available in: AutoMapper 12.1.0 (minor upgrade - no confirmation needed)

# ? Auto-upgrade to minor version
dotnet add package AutoMapper --version 12.1.0

# Review changelog for new features
# https://github.com/AutoMapper/AutoMapper/releases/tag/v12.1.0

# Test thoroughly
dotnet test
```

**If feature is in major version:**
```markdown
## Feature Request: [Feature Name]

The requested feature is only available in AutoMapper 13.0.0 (major version).

Current version: 12.0.0
Feature available in: 13.0.0

This is a major version upgrade with potential breaking changes:
[Present breaking changes as shown above]

Proceed with major upgrade to get this feature? (yes/no)

Alternative: Consider implementing custom solution without upgrade.
```

### Scenario 3: Routine Maintenance

**Context:** Monthly dependency updates

```bash
# Step 1: Identify all outdated packages
dotnet list package --outdated

# Step 2: Auto-upgrade patch versions (no confirmation)
# Update all patch versions

# Step 3: Auto-upgrade minor versions (no confirmation)
# Update all minor versions

# Step 4: Present major versions for approval
# Show breaking changes and wait for confirmation

# Step 5: Skip pre-release versions automatically
