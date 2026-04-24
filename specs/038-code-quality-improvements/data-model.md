# Data Model: Code Quality Improvements

**Feature**: 038-code-quality-improvements  
**Date**: 2025-07-24

## Overview

This feature is a purely internal refactoring — no database schema changes, no new entities, and no user-facing behavior changes. The "data model" for this feature describes the new **code constructs** introduced in the Application layer.

## New Types

### 1. ErrorCodes (Static Constants Class)

**Location**: `src/Rentier.Application/Common/ErrorCodes.cs`  
**Kind**: Static class with `public const string` fields  
**Layer**: Application

| Field | Value | Category |
|---|---|---|
| `DOMAIN_ERROR` | `"DOMAIN_ERROR"` | Generic |
| `NOT_FOUND` | `"NOT_FOUND"` | Generic |
| `INFRASTRUCTURE_ERROR` | `"INFRASTRUCTURE_ERROR"` | Generic |
| `CREDENTIAL_NOT_FOUND` | `"CREDENTIAL_NOT_FOUND"` | Credential |
| `CREDENTIAL_WRITE_FAILED` | `"CREDENTIAL_WRITE_FAILED"` | Credential |
| `CREDENTIAL_READ_FAILED` | `"CREDENTIAL_READ_FAILED"` | Credential |
| `CREDENTIAL_DELETE_FAILED` | `"CREDENTIAL_DELETE_FAILED"` | Credential |
| `PROVIDER_UNAVAILABLE` | `"PROVIDER_UNAVAILABLE"` | Credential |
| `UNSUPPORTED_PLATFORM` | `"UNSUPPORTED_PLATFORM"` | Credential |
| `FILING_BULK_DELETE_FAILED` | `"FILING_BULK_DELETE_FAILED"` | Filing |
| `FILING_BULK_DELETE_INVALID` | `"FILING_BULK_DELETE_INVALID"` | Filing |
| `FILING_CREATE_DUPLICATE` | `"FILING_CREATE_DUPLICATE"` | Filing |
| `REPORT_BULK_DELETE_FAILED` | `"REPORT_BULK_DELETE_FAILED"` | Report |
| `REPORT_BULK_DELETE_INVALID` | `"REPORT_BULK_DELETE_INVALID"` | Report |
| `REPORT_DELETE_FAILED` | `"REPORT_DELETE_FAILED"` | Report |
| `REPORT_QUERY_FAILED` | `"REPORT_QUERY_FAILED"` | Report |
| `REPORT_IMPORT_FAILED` | `"REPORT_IMPORT_FAILED"` | Report |
| `REPORT_IMPORT_DUPLICATE` | `"REPORT_IMPORT_DUPLICATE"` | Report |
| `REPORT_IMPORT_INVALID_CSV` | `"REPORT_IMPORT_INVALID_CSV"` | Report |
| `REPORT_PROCESS_NO_ATTACHMENT` | `"REPORT_PROCESS_NO_ATTACHMENT"` | Report |
| `REPORT_PROCESS_NO_TAXPAYER` | `"REPORT_PROCESS_NO_TAXPAYER"` | Report |
| `REPORT_PROCESS_PARSE_FAILED` | `"REPORT_PROCESS_PARSE_FAILED"` | Report |
| `DASHBOARD_QUERY_FAILED` | `"DASHBOARD_QUERY_FAILED"` | Dashboard |
| `IMPORTER_NOT_FOUND` | `"IMPORTER_NOT_FOUND"` | Importer |
| `IMPORTER_VALIDATION_INVALID_REGEX` | `"IMPORTER_VALIDATION_INVALID_REGEX"` | Importer |
| `MAILBOX_VALIDATION_FAILED` | `"MAILBOX_VALIDATION_FAILED"` | Mailbox |
| `HOLIDAY_SAVE_DUPLICATE_DATES` | `"HOLIDAY_SAVE_DUPLICATE_DATES"` | Holiday |
| `HOLIDAY_SAVE_INVALID_YEAR_RANGE` | `"HOLIDAY_SAVE_INVALID_YEAR_RANGE"` | Holiday |
| `HOLIDAY_FETCH_ALL_FAILED` | `"HOLIDAY_FETCH_ALL_FAILED"` | Holiday |
| `PAGINATION_VALIDATION_FAILED` | `"PAGINATION_VALIDATION_FAILED"` | Validation |
| `DATE_REQUIRED` | `"DATE_REQUIRED"` | Calculator |
| `GROSS_REQUIRED` | `"GROSS_REQUIRED"` | Calculator |
| `TICKER_REQUIRED` | `"TICKER_REQUIRED"` | Calculator |
| `NET_EXCEEDS_GROSS` | `"NET_EXCEEDS_GROSS"` | Calculator |
| `NET_NEGATIVE` | `"NET_NEGATIVE"` | Calculator |
| `NETWORK_FAILURE` | `"NETWORK_FAILURE"` | Calculator |
| `RATE_NOT_FOUND` | `"RATE_NOT_FOUND"` | Calculator |

**Validation Rules**:
- All values MUST be unique (enforced by unit test)
- All values MUST follow `SCREAMING_SNAKE_CASE` format
- Entity-specific codes MUST follow `ENTITY_ACTION_REASON` pattern

---

### 2. HandlerHelper (Static Helper Class)

**Location**: `src/Rentier.Application/Common/HandlerHelper.cs`  
**Kind**: Static class  
**Layer**: Application

```text
HandlerHelper
├── ExecuteAsync<TValue>(operation, errorCode, logger?, caller?)
│   → Wraps operation in try-catch:
│     ├── OperationCanceledException → rethrow
│     ├── DomainException → Result.Failure(Error.Domain(ex.Message))
│     └── Exception → Result.Failure(new Error(errorCode, ex.Message)) + log
│
└── ExecuteWithValidationAsync<TValue>(validation, operation, errorCode, logger?, caller?)
    → Runs validation first; if failure returned, short-circuits
    → Otherwise delegates to ExecuteAsync
```

**Parameters**:
| Parameter | Type | Description |
|---|---|---|
| `operation` | `Func<Task<Result<TValue, Error>>>` | The handler's core logic |
| `errorCode` | `string` | Error code constant for unexpected failures |
| `logger` | `ILogger?` | Optional logger for exception logging |
| `caller` | `string?` | Auto-filled caller member name |
| `validation` | `Func<Result<TValue, Error>?>` | Optional pre-validation returning null (valid) or failure |

---

### 3. IPaginatedQuery (Interface)

**Location**: `src/Rentier.Application/Queries/IPaginatedQuery.cs`  
**Kind**: Interface  
**Layer**: Application

```text
IPaginatedQuery
├── int Page { get; }       — Page number (must be >= 1)
└── int PageSize { get; }   — Items per page (must be 1–100)
```

**Implementors**:
- `GetFilingsQuery` (existing — add interface)
- `GetReportsQuery` (existing — add interface)

---

### 4. PaginationValidator (Static Helper Class)

**Location**: `src/Rentier.Application/Common/PaginationValidator.cs`  
**Kind**: Static class  
**Layer**: Application

```text
PaginationValidator
└── Validate<TValue>(IPaginatedQuery query) → Result<TValue, Error>?
    ├── query.Page < 1 → Failure(PAGINATION_VALIDATION_FAILED, "Page must be >= 1.")
    ├── query.PageSize < 1 || > 100 → Failure(PAGINATION_VALIDATION_FAILED, "PageSize must be between 1 and 100.")
    └── valid → null (caller proceeds)
```

---

## Modified Types

### Error Record (Updated)

**Location**: `src/Rentier.Application/Common/Error.cs`  
**Change**: Update static factory methods to reference `ErrorCodes` constants instead of inline strings

```text
Error (sealed record)
├── Code: string
├── Message: string
├── Domain(message) → ErrorCodes.DOMAIN_ERROR
├── NotFound(message) → ErrorCodes.NOT_FOUND
├── Infrastructure(message) → ErrorCodes.INFRASTRUCTURE_ERROR
├── CredentialNotFound(key) → ErrorCodes.CREDENTIAL_NOT_FOUND
├── CredentialWriteFailed(message) → ErrorCodes.CREDENTIAL_WRITE_FAILED
├── CredentialReadFailed(message) → ErrorCodes.CREDENTIAL_READ_FAILED
├── CredentialDeleteFailed(message) → ErrorCodes.CREDENTIAL_DELETE_FAILED
├── ProviderUnavailable(reason) → ErrorCodes.PROVIDER_UNAVAILABLE
└── UnsupportedPlatform(os) → ErrorCodes.UNSUPPORTED_PLATFORM
```

---

## Relationships

```text
ErrorCodes ──referenced by──▶ Error (factory methods)
ErrorCodes ──referenced by──▶ All Handlers (error code parameters)
ErrorCodes ──referenced by──▶ PaginationValidator

HandlerHelper ──uses──▶ Result<TValue, Error>
HandlerHelper ──catches──▶ DomainException (from Domain layer)
HandlerHelper ──catches──▶ OperationCanceledException

IPaginatedQuery ──implemented by──▶ GetFilingsQuery
IPaginatedQuery ──implemented by──▶ GetReportsQuery
IPaginatedQuery ──consumed by──▶ PaginationValidator

PaginationValidator ──produces──▶ Result<TValue, Error>
PaginationValidator ──uses──▶ ErrorCodes.PAGINATION_VALIDATION_FAILED
```

---

## Database Impact

**None.** This feature does not modify the database schema, EF Core model, migrations, or persisted data.

## State Transitions

**None.** No new domain state machines are introduced. The existing Filing status machine is unaffected.
