# Quickstart: Test Coverage Expansion

**Feature**: 042-test-coverage-expansion  
**Date**: 2025-07-15

## Prerequisites

- .NET 10.0 SDK installed
- Repository cloned and on the `042-test-coverage-expansion` branch
- `dotnet restore` completed (all NuGet packages already configured)

## Run All New Tests

```powershell
# From repository root
cd F:\Projects\Rentier\rentier

# Run entire test suite (includes all new tests)
dotnet test --no-build

# Run only property-based tests (FsCheck)
dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~Properties"

# Run only snapshot tests (Verify)
dotnet test tests/Rentier.Infrastructure.Tests --filter "FullyQualifiedName~Snapshot"

# Run only the new ViewModel tests
dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~HolidayEntryViewModel|FullyQualifiedName~SyncProgressEntryViewModel|FullyQualifiedName~ImporterItemViewModel"
```

## Verify Success Criteria

### SC-001: Property-based test count ≥ 10

```powershell
dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~Properties" --list-tests
# Expect: at least 10 tests listed (was 4 before this feature)
```

### SC-002: Snapshot test count ≥ 5

```powershell
dotnet test tests/Rentier.Infrastructure.Tests --filter "FullyQualifiedName~Snapshot" --list-tests
# Expect: at least 5 tests listed (was 1 before this feature)
```

### SC-003: All ViewModels have tests

```powershell
# Verify tests exist for all 3 previously-untested ViewModels
dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~HolidayEntryViewModelTests|FullyQualifiedName~SyncProgressEntryViewModelTests|FullyQualifiedName~ImporterItemViewModelTests"
# All 3 test classes must pass
```

### SC-007: CI time budget (≤15% increase)

```powershell
# Time the full suite before and after
Measure-Command { dotnet test --no-build 2>&1 | Out-Null }
```

## Updating Snapshot Baselines

When serialization logic intentionally changes:

```powershell
# Delete the existing verified file, run tests, then review and commit the new baseline
Remove-Item tests/Rentier.Infrastructure.Tests/Serialization/*.verified.*
dotnet test tests/Rentier.Infrastructure.Tests --filter "FullyQualifiedName~Snapshot"
# Review the new .verified.* files, then git add + commit
```

## Test Organization

| Category | Project | Directory | Framework |
|----------|---------|-----------|-----------|
| Property-based (domain invariants) | `Rentier.UnitTests` | `Domain/Properties/` | FsCheck.Xunit 3.x |
| Snapshot (serialization stability) | `Rentier.Infrastructure.Tests` | `Serialization/` | Verify.Xunit 28.x |
| ViewModel (data binding) | `Rentier.UnitTests` | `Desktop/ViewModels/` | xUnit + FluentAssertions |

## Naming Convention

All tests follow: `MethodName_StateUnderTest_ExpectedBehavior`

Examples:
- `AdvanceStatus_AllInvalidPairs_ThrowsDomainException`
- `Serialize_InterestFiling_MatchesSnapshot`
- `FromDto_ValidHolidayEntry_PropertiesMatchInput`
