# Quickstart: Code Quality Improvements

**Feature**: 038-code-quality-improvements  
**Date**: 2025-07-24

## What This Feature Does

This is an internal code quality refactoring with four changes:
1. **Async fix** — Eliminates the last `.Result` anti-pattern in `MacOsCredentialStore`
2. **Handler helper** — Centralizes try-catch error handling across 12+ CQRS handlers
3. **Error code registry** — Creates a single `ErrorCodes` constants class for all ~37 error codes
4. **Pagination contract** — Extracts shared pagination validation from duplicated handler code

## Prerequisites

- .NET 8 SDK
- Rentier solution builds successfully (`dotnet build Rentier.slnx`)
- All existing tests pass (`dotnet test Rentier.slnx`)

## Implementation Order

### Story 1: Async Fix (P1 — do first, standalone)

**File to change**: `src/Rentier.Infrastructure/Security/MacOsCredentialStore.cs`

```csharp
// Line 107 — BEFORE:
return (process.ExitCode, stdoutTask.Result, stderrTask.Result);

// AFTER:
return (process.ExitCode, await stdoutTask, await stderrTask);
```

**Verify**: Search entire `src/` for `.Result` and `.Wait()` on task types — zero matches expected.

### Story 3: Error Code Registry (P3 — do before Story 2)

> **Why before Story 2?** The handler helper needs error code constants to exist first.

1. Create `src/Rentier.Application/Common/ErrorCodes.cs` with all constants
2. Update `Error.cs` factory methods to use `ErrorCodes.*` instead of inline strings
3. Update each handler to reference `ErrorCodes.*` instead of inline string literals
4. Write `ErrorCodesTests` to verify uniqueness and naming convention

### Story 4: Pagination Contract (P4 — do before Story 2)

> **Why before Story 2?** Pagination validation is used by handler helper's validation callback.

1. Create `src/Rentier.Application/Queries/IPaginatedQuery.cs`
2. Add `IPaginatedQuery` to `GetFilingsQuery` and `GetReportsQuery`
3. Create `src/Rentier.Application/Common/PaginationValidator.cs`
4. Replace inline pagination checks in `GetFilingsQueryHandler` and `GetReportsQueryHandler`
5. Write `PaginationValidatorTests`

### Story 2: Handler Error Helper (P2 — do last, depends on Stories 3 + 4)

1. Create `src/Rentier.Application/Common/HandlerHelper.cs`
2. Write `HandlerHelperTests` to verify all exception handling paths
3. Migrate handlers one by one (start with simplest Pattern B handlers)
4. Run full test suite after each handler migration
5. Leave `ProcessReportsCommandHandler` and other complex handlers with custom logic

## Key Files

| File | Purpose |
|---|---|
| `src/Rentier.Application/Common/ErrorCodes.cs` | **New** — Centralized error code constants |
| `src/Rentier.Application/Common/HandlerHelper.cs` | **New** — Shared try-catch helper |
| `src/Rentier.Application/Common/PaginationValidator.cs` | **New** — Shared pagination validation |
| `src/Rentier.Application/Queries/IPaginatedQuery.cs` | **New** — Pagination interface |
| `src/Rentier.Application/Common/Error.cs` | **Modified** — Use ErrorCodes constants |
| `src/Rentier.Infrastructure/Security/MacOsCredentialStore.cs` | **Modified** — Remove `.Result` |
| `src/Rentier.Application/Handlers/*.cs` | **Modified** — Use HandlerHelper + ErrorCodes |

## Testing Checklist

- [ ] `dotnet test Rentier.slnx` — all existing tests pass (no regressions)
- [ ] `ErrorCodesTests` — all codes unique, all SCREAMING_SNAKE_CASE
- [ ] `HandlerHelperTests` — cancellation rethrown, DomainException → domain failure, Exception → infrastructure failure
- [ ] `PaginationValidatorTests` — boundary values (0, 1, 100, 101) handled correctly
- [ ] Codebase search for `.Result` and `.Wait()` — zero production code matches
- [ ] Codebase search for inline error code strings in handlers — zero matches (all use ErrorCodes.*)

## Build & Test Commands

```bash
# Build
dotnet build Rentier.slnx

# Run all tests
dotnet test Rentier.slnx

# Run only unit tests
dotnet test tests/Rentier.UnitTests/Rentier.UnitTests.csproj

# Search for .Result anti-pattern (should return no production code matches)
# PowerShell:
Get-ChildItem src -Recurse -Include *.cs | Select-String '\.Result[^s]|\.Wait\(' | Where-Object { $_.Line -notmatch '//|///|^\s*\*' }
```
