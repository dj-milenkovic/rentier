# Research: Code Quality Improvements

**Feature**: 038-code-quality-improvements  
**Date**: 2025-07-24  
**Status**: Complete

## Research Task 1: Async Anti-Pattern in MacOsCredentialStore

### Decision
Replace `stdoutTask.Result` and `stderrTask.Result` with `await` expressions on the already-completed tasks.

### Rationale
Line 107 of `MacOsCredentialStore.cs` accesses `.Result` on two `Task<string>` objects after `Task.WhenAll` has completed them. While functionally safe (the tasks are already completed), this violates Constitution Principle IV ("MUST NOT use `.Result` or `.Wait()`") and prevents blanket static analysis enforcement. The fix is a single-line change replacing `.Result` with `await`.

### Alternatives Considered
1. **Keep `.Result` with suppression comment** — Rejected because it creates an exception to the rule that makes static analysis enforcement impossible.
2. **Deconstruct `Task.WhenAll` result** — `Task.WhenAll` doesn't directly return individual results; using `await` on each completed task is idiomatic.
3. **Use `ValueTask` or custom awaitable** — Over-engineered for this scenario.

### Findings
- **Scope**: Exactly 1 file, 1 line, 2 occurrences of `.Result`
- **File**: `src/Rentier.Infrastructure/Security/MacOsCredentialStore.cs:107`
- **Current code**: `return (process.ExitCode, stdoutTask.Result, stderrTask.Result);`
- **Fix**: `return (process.ExitCode, await stdoutTask, await stderrTask);`
- **Risk**: Minimal — tasks are already completed via `Task.WhenAll`. Behavior is identical.
- **Codebase search**: No other `.Result` or `.Wait()` calls exist in production `src/` code.

---

## Research Task 2: Handler Error Handling Centralization

### Decision
Create a static `HandlerHelper.ExecuteAsync<TResult>()` method in `Rentier.Application/Common/` that encapsulates the standard try-catch pattern with configurable domain exception mapping.

### Rationale
16 of 33 handlers implement try-catch blocks following 6 distinct patterns. The most common patterns are:
- **Pattern A** (8 handlers): Catch `DomainException` → return domain failure
- **Pattern B** (7 handlers): Catch `OperationCanceledException` (rethrow) + catch `Exception` → return infrastructure failure
- **Pattern C** (1 handler): Catch `DomainException` → domain failure, catch `Exception` → infrastructure failure

A shared helper can unify patterns A, B, and C into a single call. Pattern A is a subset of C, and pattern B is C without the domain exception catch.

### Alternatives Considered
1. **Abstract base handler class** — Rejected because it forces inheritance on 33 handlers, many of which have no try-catch and don't need it. Composition via a static helper is less invasive.
2. **Middleware/decorator pattern (pipeline behaviors)** — Rejected because the project doesn't use MediatR or a pipeline infrastructure. Adding one solely for error handling is over-engineered.
3. **Extension method on `Result<T, Error>`** — Rejected because the wrapping logic needs to produce a `Result`, not transform one.

### Findings
- **Handler inventory**: 33 total handlers (25 command, 8 query)
- **Handlers with try-catch**: 16 (48%)
- **Handlers without try-catch**: 17 (52%) — these don't need the helper
- **Distinct patterns**: 6 variations across the 16 handlers
- **ProcessReportsCommandHandler**: Most complex handler with 3-level nested try-catch; this handler will retain custom logic and is NOT a candidate for the shared helper
- **Regex validation handlers** (AddImporterCommandHandler, UpdateImporterCommandHandler): Have a separate pre-operation try-catch for `ArgumentException` on regex validation — the helper must support pre-validation callbacks
- **Migration scope**: 12–14 handlers are candidates for the shared helper; 2–4 will retain custom logic

### Proposed API
```csharp
public static class HandlerHelper
{
    public static async Task<Result<TValue, Error>> ExecuteAsync<TValue>(
        Func<Task<Result<TValue, Error>>> operation,
        string errorCode,
        ILogger? logger = null,
        [CallerMemberName] string? caller = null);
}
```

---

## Research Task 3: Error Code Standardization

### Decision
Create an `ErrorCodes` static class in `Rentier.Application/Common/` containing string constants for all handler error codes, following the `ENTITY_ACTION_REASON` naming convention.

### Rationale
30+ unique error codes are scattered across handlers as inline string literals. The existing `Error.cs` provides factory methods for 8 common codes but the remaining 22+ are hardcoded. A centralized constants class makes codes discoverable, prevents duplicates, and enables compile-time references.

### Alternatives Considered
1. **Enum-based error codes** — Rejected because the `Error` record uses `string Code`, and changing to an enum would require modifying the `Error` type and all consumers. String constants are backward-compatible.
2. **Nested classes per domain area** — Considered but rejected for initial implementation. A flat constants class is simpler; nesting can be added later if the count grows significantly.
3. **Source-generated codes** — Over-engineered; the count (~35 codes) doesn't warrant generation.

### Findings — Complete Error Code Inventory

| Current Code | Handler(s) | Proposed Standard Code | Change? |
|---|---|---|---|
| `BULK_DELETE_FILINGS_FAILED` | BulkDeleteFilingsCommandHandler | `FILING_BULK_DELETE_FAILED` | Rename |
| `BULK_DELETE_FILINGS_INVALID` | BulkDeleteFilingsCommandHandler | `FILING_BULK_DELETE_INVALID` | Rename |
| `BULK_DELETE_REPORTS_FAILED` | BulkDeleteReportsCommandHandler | `REPORT_BULK_DELETE_FAILED` | Rename |
| `BULK_DELETE_REPORTS_INVALID` | BulkDeleteReportsCommandHandler | `REPORT_BULK_DELETE_INVALID` | Rename |
| `CREDENTIAL_DELETE_FAILED` | Error.cs factory | Keep | No |
| `CREDENTIAL_NOT_FOUND` | Error.cs factory + DeleteMailboxCommandHandler | Keep | No |
| `CREDENTIAL_READ_FAILED` | Error.cs factory | Keep | No |
| `CREDENTIAL_WRITE_FAILED` | Error.cs factory | Keep | No |
| `DASHBOARD_ERROR` | GetDashboardQueryHandler | `DASHBOARD_QUERY_FAILED` | Rename |
| `DATE_REQUIRED` | ManualFilingCalculator | Keep | No |
| `DELETE_REPORT_FAILED` | DeleteReportCommandHandler | `REPORT_DELETE_FAILED` | Rename |
| `DOMAIN_ERROR` | Error.cs factory + multiple handlers | Keep (generic) | No |
| `DOMAIN_VALIDATION` | AddMailboxCommandHandler, UpdateMailboxCommandHandler | `MAILBOX_VALIDATION_FAILED` | Rename |
| `DUPLICATE_DATES` | SaveHolidayConfCommandHandler | `HOLIDAY_SAVE_DUPLICATE_DATES` | Rename |
| `DUPLICATE_FILING` | CreateManualFilingCommandHandler | `FILING_CREATE_DUPLICATE` | Rename |
| `DUPLICATE_REPORT` | ImportReportCommandHandler | `REPORT_IMPORT_DUPLICATE` | Rename |
| `GET_REPORTS_FAILED` | GetReportsQueryHandler | `REPORT_QUERY_FAILED` | Rename |
| `GROSS_REQUIRED` | ManualFilingCalculator | Keep | No |
| `HOLIDAY_FETCH_ALL_FAILED` | FetchHolidaysFromWebCommandHandler | Keep | No |
| `IMPORT_FAILED` | ImportReportCommandHandler | `REPORT_IMPORT_FAILED` | Rename |
| `IMPORTER_NOT_FOUND` | ProcessReportsCommandHandler, UpdateImporterCommandHandler | Keep | No |
| `INFRASTRUCTURE_ERROR` | Error.cs factory | Keep (generic) | No |
| `INVALID_CSV` | ImportReportCommandHandler | `REPORT_IMPORT_INVALID_CSV` | Rename |
| `INVALID_REGEX` | AddImporterCommandHandler, UpdateImporterCommandHandler | `IMPORTER_VALIDATION_INVALID_REGEX` | Rename |
| `INVALID_YEAR_RANGE` | SaveHolidayConfCommandHandler | `HOLIDAY_SAVE_INVALID_YEAR_RANGE` | Rename |
| `NET_EXCEEDS_GROSS` | ManualFilingCalculator | Keep | No |
| `NET_NEGATIVE` | ManualFilingCalculator | Keep | No |
| `NETWORK_FAILURE` | ManualFilingCalculator | Keep | No |
| `NOT_FOUND` | Error.cs factory + UpdateMailboxCommandHandler | Keep (generic) | No |
| `NO_ATTACHMENT` | ProcessReportsCommandHandler | `REPORT_PROCESS_NO_ATTACHMENT` | Rename |
| `NO_TAXPAYER_PROFILE` | ProcessReportsCommandHandler | `REPORT_PROCESS_NO_TAXPAYER` | Rename |
| `PARSE_FAILED` | ProcessReportsCommandHandler | `REPORT_PROCESS_PARSE_FAILED` | Rename |
| `PROVIDER_UNAVAILABLE` | Error.cs factory | Keep | No |
| `RATE_NOT_FOUND` | ManualFilingCalculator, ExchangeRateResolver | Keep | No |
| `TICKER_REQUIRED` | ManualFilingCalculator | Keep | No |
| `UNSUPPORTED_PLATFORM` | Error.cs factory | Keep | No |
| `VALIDATION_ERROR` | GetReportsQueryHandler, GetFilingsQueryHandler | `PAGINATION_VALIDATION_FAILED` | Rename |

- **Total codes**: ~37
- **Codes requiring rename**: ~17
- **Codes already well-named**: ~20
- **Backward compatibility**: Error codes are matched by string in `DeleteMailboxCommandHandler` (checks `CREDENTIAL_NOT_FOUND`). Any renamed codes used in UI/presentation matching must be updated simultaneously.

---

## Research Task 4: Pagination Validation Extraction

### Decision
Create an `IPaginatedQuery` interface in `Rentier.Application/Queries/` and a `PaginationValidator` static helper in `Rentier.Application/Common/` that validates any `IPaginatedQuery`.

### Rationale
Two query handlers (`GetFilingsQueryHandler`, `GetReportsQueryHandler`) duplicate identical validation: `Page >= 1` and `1 <= PageSize <= 100`. Both use the same error code (`"VALIDATION_ERROR"`) and the same error messages. A shared interface + validator eliminates the duplication and provides a reusable contract for future paginated queries.

### Alternatives Considered
1. **Base record for paginated queries** — Rejected because C# records don't support partial inheritance well, and both queries have very different additional fields. An interface is more flexible.
2. **FluentValidation** — Rejected because the project doesn't use FluentValidation and adding it for 2 handlers is over-engineered.
3. **Validation in the query constructor** — Rejected because query records are DTOs and should not throw exceptions.

### Findings
- **Affected handlers**: 2 (GetFilingsQueryHandler, GetReportsQueryHandler)
- **Current query records**:
  - `GetFilingsQuery(FilingFilterMode, int Page, int PageSize, Guid?, FilingSortColumn, bool)`
  - `GetReportsQuery(int Page, int PageSize, bool SortDescending)`
- **Validation rules**: `Page >= 1`, `1 <= PageSize <= 100`
- **Default values**: Both default to `Page = 1, PageSize = 30`
- **Error code**: Both use `"VALIDATION_ERROR"` — will be standardized to `ErrorCodes.PAGINATION_VALIDATION_FAILED`
- **Composition**: GetFilingsQueryHandler has additional sort column validation that must remain handler-specific

### Proposed API
```csharp
public interface IPaginatedQuery
{
    int Page { get; }
    int PageSize { get; }
}

public static class PaginationValidator
{
    public static Result<TValue, Error>? Validate<TValue>(IPaginatedQuery query);
    // Returns null if valid, Result.Failure if invalid
}
```

---

## Research Task 5: Existing Test Coverage

### Decision
Leverage existing test projects without creating new test projects. Unit tests go in `Rentier.UnitTests`, infrastructure tests in `Rentier.Infrastructure.Tests`.

### Rationale
The test infrastructure already supports all needed test types. The async fix is in Infrastructure (platform-specific, may need manual verification). Error handler, error codes, and pagination are in Application (unit-testable with mocks).

### Findings
- **Test projects**: `Rentier.UnitTests`, `Rentier.Infrastructure.Tests`, `Rentier.Scenarios.Tests`, `Rentier.E2E.Tests`, `Rentier.Tests.Common`
- **Existing handler tests**: Located in `Rentier.UnitTests` — all existing tests must continue passing after refactoring
- **New tests needed**:
  - `HandlerHelperTests` — verify the shared error handling helper behavior
  - `ErrorCodesTests` — verify uniqueness and naming convention of all codes
  - `PaginationValidatorTests` — verify boundary values and error responses
