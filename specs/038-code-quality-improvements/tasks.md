---
description: "Task list for 038-code-quality-improvements"
---

# Tasks: Code Quality Improvements

**Input**: `specs/038-code-quality-improvements/` (spec.md, plan.md, research.md, data-model.md, quickstart.md)  
**Feature Branch**: `038-code-quality-improvements`  
**Tests**: Included — Application layer new code requires unit tests (HandlerHelper, ErrorCodes, PaginationValidator).

**Organization**: Tasks are organized by **actual execution order** (not spec priority). User Story 2 (P2) depends on User Stories 3 and 4 being complete first (error code constants and pagination validator must exist before the handler helper can reference them). See plan.md D3 for rationale.

> **⚠️ Execution Order Note**: Although spec priorities are P1→P2→P3→P4, the implementation order is:
> **US1 (async fix) → US3 (error codes) → US4 (pagination) → US2 (handler helper)**
> This is because the HandlerHelper (US2) references `ErrorCodes.*` constants (US3) and composes with `PaginationValidator` (US4).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete sibling tasks)
- **[Story]**: Which user story this task belongs to
- Each task includes the exact file path to edit or create

---

## Phase 1: Baseline Verification

**Purpose**: Confirm the green baseline before any refactoring begins. Every subsequent phase must leave all tests green.

- [X] T001 Verify solution builds and all tests pass: `dotnet build Rentier.slnx && dotnet test Rentier.slnx`

**Checkpoint**: Green baseline confirmed — refactoring may now begin.

---

## Phase 2: User Story 1 — Eliminate Blocking Async Anti-Pattern (Priority: P1)

**Goal**: Replace the two `.Result` accesses on already-completed tasks in `MacOsCredentialStore.cs` with `await`, achieving full Constitution Principle IV compliance (zero `.Result`/`.Wait()` in production code).

**Independent Test**: Search `src/` for `.Result` and `.Wait()` — zero matches in production code.

- [X] T002 [US1] Replace `stdoutTask.Result` and `stderrTask.Result` with `await stdoutTask` and `await stderrTask` on line 107 of `src/Rentier.Infrastructure/Security/MacOsCredentialStore.cs`
- [X] T003 [US1] Verify zero `.Result` / `.Wait()` calls remain in `src/`: run `Get-ChildItem src -Recurse -Include *.cs | Select-String '\.Result[^s]|\.Wait\(' | Where-Object { $_.Line -notmatch '//|///|^\s*\*' }`

**Checkpoint**: SC-001 satisfied — zero `.Result`/`.Wait()` in production code.

---

## Phase 3: User Story 3 — Standardize Error Codes (Priority: P3)

> **⚠️ Must complete before Phase 5 (US2)** — `HandlerHelper` references `ErrorCodes.*` constants.

**Goal**: Create a centralized `ErrorCodes` static class and migrate all 14+ handler files from inline error code string literals to constants. ~17 codes are renamed to follow `ENTITY_ACTION_REASON` convention; ~20 are kept with their existing values but moved to the registry.

**Independent Test**: `ErrorCodesTests` passes: all codes are unique, all follow SCREAMING_SNAKE_CASE, zero inline string literal error codes remain in handler files.

### Create Registry

- [X] T004 [US3] Create `src/Rentier.Application/Common/ErrorCodes.cs` — static class with all 37 `public const string` fields as specified in data-model.md (Generic, Credential, Filing, Report, Dashboard, Importer, Mailbox, Holiday, Pagination, Calculator categories)
- [X] T005 [US3] Update `src/Rentier.Application/Common/Error.cs` — replace all 8 inline string literals in factory methods (`Domain`, `NotFound`, `Infrastructure`, `CredentialNotFound`, `CredentialWriteFailed`, `CredentialReadFailed`, `CredentialDeleteFailed`, `ProviderUnavailable`, `UnsupportedPlatform`) with `ErrorCodes.*` references

### Migrate Handlers to ErrorCodes (all parallel — different files)

- [X] T006 [P] [US3] Update `src/Rentier.Application/Handlers/BulkDeleteFilingsCommandHandler.cs` — replace `"BULK_DELETE_FILINGS_FAILED"` → `ErrorCodes.FILING_BULK_DELETE_FAILED`, `"BULK_DELETE_FILINGS_INVALID"` → `ErrorCodes.FILING_BULK_DELETE_INVALID`
- [X] T007 [P] [US3] Update `src/Rentier.Application/Handlers/BulkDeleteReportsCommandHandler.cs` — replace `"BULK_DELETE_REPORTS_FAILED"` → `ErrorCodes.REPORT_BULK_DELETE_FAILED`, `"BULK_DELETE_REPORTS_INVALID"` → `ErrorCodes.REPORT_BULK_DELETE_INVALID`
- [X] T008 [P] [US3] Update `src/Rentier.Application/Handlers/GetDashboardQueryHandler.cs` — replace `"DASHBOARD_ERROR"` → `ErrorCodes.DASHBOARD_QUERY_FAILED`
- [X] T009 [P] [US3] Update `src/Rentier.Application/Handlers/DeleteReportCommandHandler.cs` — replace `"DELETE_REPORT_FAILED"` → `ErrorCodes.REPORT_DELETE_FAILED`
- [X] T010 [P] [US3] Update `src/Rentier.Application/Handlers/AddMailboxCommandHandler.cs` — replace `"DOMAIN_VALIDATION"` → `ErrorCodes.MAILBOX_VALIDATION_FAILED`
- [X] T011 [P] [US3] Update `src/Rentier.Application/Handlers/UpdateMailboxCommandHandler.cs` — replace `"DOMAIN_VALIDATION"` → `ErrorCodes.MAILBOX_VALIDATION_FAILED`; ensure `"NOT_FOUND"` references `ErrorCodes.NOT_FOUND`
- [X] T012 [P] [US3] Update `src/Rentier.Application/Handlers/SaveHolidayConfCommandHandler.cs` — replace `"DUPLICATE_DATES"` → `ErrorCodes.HOLIDAY_SAVE_DUPLICATE_DATES`, `"INVALID_YEAR_RANGE"` → `ErrorCodes.HOLIDAY_SAVE_INVALID_YEAR_RANGE`
- [X] T013 [P] [US3] Update `src/Rentier.Application/Handlers/CreateManualFilingCommandHandler.cs` — replace `"DUPLICATE_FILING"` → `ErrorCodes.FILING_CREATE_DUPLICATE`
- [X] T014 [P] [US3] Update `src/Rentier.Application/Handlers/ImportReportCommandHandler.cs` — replace `"IMPORT_FAILED"` → `ErrorCodes.REPORT_IMPORT_FAILED`, `"DUPLICATE_REPORT"` → `ErrorCodes.REPORT_IMPORT_DUPLICATE`, `"INVALID_CSV"` → `ErrorCodes.REPORT_IMPORT_INVALID_CSV`
- [X] T015 [P] [US3] Update `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs` — replace `"GET_REPORTS_FAILED"` → `ErrorCodes.REPORT_QUERY_FAILED`, `"VALIDATION_ERROR"` → `ErrorCodes.PAGINATION_VALIDATION_FAILED`
- [X] T016 [P] [US3] Update `src/Rentier.Application/Handlers/GetFilingsQueryHandler.cs` — replace `"VALIDATION_ERROR"` → `ErrorCodes.PAGINATION_VALIDATION_FAILED`
- [X] T017 [P] [US3] Update `src/Rentier.Application/Handlers/AddImporterCommandHandler.cs` — replace `"INVALID_REGEX"` → `ErrorCodes.IMPORTER_VALIDATION_INVALID_REGEX`; ensure `"IMPORTER_NOT_FOUND"` references `ErrorCodes.IMPORTER_NOT_FOUND`
- [X] T018 [P] [US3] Update `src/Rentier.Application/Handlers/UpdateImporterCommandHandler.cs` — replace `"INVALID_REGEX"` → `ErrorCodes.IMPORTER_VALIDATION_INVALID_REGEX`; ensure `"IMPORTER_NOT_FOUND"` references `ErrorCodes.IMPORTER_NOT_FOUND`
- [X] T019 [P] [US3] Update `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs` — replace `"NO_ATTACHMENT"` → `ErrorCodes.REPORT_PROCESS_NO_ATTACHMENT`, `"NO_TAXPAYER_PROFILE"` → `ErrorCodes.REPORT_PROCESS_NO_TAXPAYER`, `"PARSE_FAILED"` → `ErrorCodes.REPORT_PROCESS_PARSE_FAILED`; ensure `"IMPORTER_NOT_FOUND"` references `ErrorCodes.IMPORTER_NOT_FOUND`

### Tests for User Story 3

- [X] T020 [US3] Create `tests/Rentier.UnitTests/Common/ErrorCodesTests.cs` — write tests verifying: (1) all string values in `ErrorCodes` are unique across the entire class, (2) all values are SCREAMING_SNAKE_CASE, (3) entity-specific codes follow `ENTITY_ACTION_REASON` format; use reflection to enumerate all `public const string` fields

**Checkpoint**: SC-003 and SC-004 satisfied — 100% of error codes in registry, uniqueness verified by test. Run `dotnet test tests/Rentier.UnitTests/Rentier.UnitTests.csproj` to confirm.

---

## Phase 4: User Story 4 — Extract Shared Pagination Validation (Priority: P4)

> **⚠️ Must complete before Phase 5 (US2)** — `PaginationValidator` is used as a validation callback in `HandlerHelper`.

**Goal**: Create `IPaginatedQuery` interface and `PaginationValidator` static helper; update both paginated query handlers to use the shared validation instead of inline checks.

**Independent Test**: `PaginationValidatorTests` passes all boundary cases (page=0 fails, page=1 passes, size=0 fails, size=1 passes, size=100 passes, size=101 fails). Both `GetFilingsQueryHandler` and `GetReportsQueryHandler` produce consistent error responses for invalid pagination.

### Interface and Validator

- [X] T021 [US4] Create `src/Rentier.Application/Queries/IPaginatedQuery.cs` — interface with `int Page { get; }` and `int PageSize { get; }` properties
- [X] T022 [P] [US4] Add `IPaginatedQuery` to `src/Rentier.Application/Queries/GetFilingsQuery.cs` — implement interface on the existing record (no parameter changes)
- [X] T023 [P] [US4] Add `IPaginatedQuery` to `src/Rentier.Application/Queries/GetReportsQuery.cs` — implement interface on the existing record (no parameter changes)
- [X] T024 [US4] Create `src/Rentier.Application/Common/PaginationValidator.cs` — static class with `Validate<TValue>(IPaginatedQuery query)` returning `null` when valid, `Result<TValue, Error>.Failure(new Error(ErrorCodes.PAGINATION_VALIDATION_FAILED, "..."))` when page < 1 or page size outside 1–100; uses `ErrorCodes.PAGINATION_VALIDATION_FAILED`

### Update Handlers

- [X] T025 [P] [US4] Refactor `src/Rentier.Application/Handlers/GetFilingsQueryHandler.cs` — replace the inline `page < 1` and `pageSize < 1 || > 100` validation block with a `PaginationValidator.Validate<...>(query)` call; retain the sort-column validation which is handler-specific
- [X] T026 [P] [US4] Refactor `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs` — replace the inline `page < 1` and `pageSize < 1 || > 100` validation block with a `PaginationValidator.Validate<...>(query)` call

### Tests for User Story 4

- [X] T027 [US4] Create `tests/Rentier.UnitTests/Common/PaginationValidatorTests.cs` — write boundary-value tests: page=0 returns failure with `PAGINATION_VALIDATION_FAILED`; page=1 returns null (valid); pageSize=0 returns failure; pageSize=1 returns null; pageSize=100 returns null; pageSize=101 returns failure; valid pagination (page=2, size=30) returns null

**Checkpoint**: SC-005 satisfied — pagination validation exists in exactly one location. Run `dotnet test tests/Rentier.UnitTests/Rentier.UnitTests.csproj` to confirm.

---

## Phase 5: User Story 2 — Centralize Handler Error Handling (Priority: P2)

> **Depends on Phase 3 (US3) and Phase 4 (US4) being complete** — references `ErrorCodes.*` and uses `PaginationValidator` as a validation callback.

**Goal**: Create `HandlerHelper.ExecuteAsync<TValue>()` and migrate 14 handlers fully + 2 handlers partially (keeping their regex try-catch but using the helper for the main operation). `ProcessReportsCommandHandler` is explicitly excluded.

**Independent Test**: `HandlerHelperTests` passes all exception-path tests. All existing handler unit tests continue passing after each migration.

### Create HandlerHelper

- [X] T028 [US2] Create `src/Rentier.Application/Common/HandlerHelper.cs` — static class with two methods:
  - `ExecuteAsync<TValue>(Func<Task<Result<TValue, Error>>> operation, string errorCode, ILogger? logger = null, [CallerMemberName] string? caller = null)` — wraps operation: catches `OperationCanceledException` (rethrow), catches `DomainException` (return `Result.Failure(Error.Domain(ex.Message))`), catches `Exception` (log + return `Result.Failure(new Error(errorCode, ex.Message))`)
  - `ExecuteWithValidationAsync<TValue>(Func<Result<TValue, Error>?> validation, Func<Task<Result<TValue, Error>>> operation, string errorCode, ILogger? logger = null, [CallerMemberName] string? caller = null)` — runs validation first; if non-null result returned, short-circuit; otherwise delegates to `ExecuteAsync`

### Tests for User Story 2

- [X] T029 [US2] Create `tests/Rentier.UnitTests/Common/HandlerHelperTests.cs` — write tests for all exception handling paths:
  - `OperationCanceledException` is re-thrown (not caught)
  - `DomainException` returns `Result.Failure` with `Error.Code == ErrorCodes.DOMAIN_ERROR`
  - Unexpected `Exception` returns `Result.Failure` with the supplied `errorCode`
  - Successful operation returns the operation's result unchanged
  - `ExecuteWithValidationAsync`: validation returning non-null short-circuits before operation
  - `ExecuteWithValidationAsync`: validation returning null proceeds to operation

### Migrate Handlers (all parallel — different files)

> Start with the simplest Pattern B handlers (OCE + Exception), then Pattern A (DomainException only), then Pattern C.

**Pattern B — `OperationCanceledException` + `Exception`**

- [X] T030 [P] [US2] Migrate `src/Rentier.Application/Handlers/BulkDeleteFilingsCommandHandler.cs` to `HandlerHelper.ExecuteAsync` with `ErrorCodes.FILING_BULK_DELETE_FAILED`; remove existing try-catch boilerplate
- [X] T031 [P] [US2] Migrate `src/Rentier.Application/Handlers/BulkDeleteReportsCommandHandler.cs` to `HandlerHelper.ExecuteAsync` with `ErrorCodes.REPORT_BULK_DELETE_FAILED`; remove existing try-catch boilerplate
- [X] T032 [P] [US2] Migrate `src/Rentier.Application/Handlers/DeleteReportCommandHandler.cs` to `HandlerHelper.ExecuteAsync` with `ErrorCodes.REPORT_DELETE_FAILED`; remove existing try-catch boilerplate
- [X] T033 [P] [US2] Migrate `src/Rentier.Application/Handlers/GetDashboardQueryHandler.cs` to `HandlerHelper.ExecuteAsync` with `ErrorCodes.DASHBOARD_QUERY_FAILED`; remove existing try-catch boilerplate
- [X] T034 [P] [US2] Migrate `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs` to `HandlerHelper.ExecuteWithValidationAsync` — pass `PaginationValidator.Validate<PagedResult<ReportSummary>>` as the validation callback and `ErrorCodes.REPORT_QUERY_FAILED` as the error code; remove existing try-catch and inline pagination checks
- [X] T035 [P] [US2] Migrate `src/Rentier.Application/Handlers/ImportReportCommandHandler.cs` to `HandlerHelper.ExecuteAsync` with `ErrorCodes.REPORT_IMPORT_FAILED`; remove existing try-catch boilerplate (preserve inner validation logic)

**Pattern A — `DomainException` only**

- [X] T036 [P] [US2] Migrate `src/Rentier.Application/Handlers/AddMailboxCommandHandler.cs` to `HandlerHelper.ExecuteAsync` with `ErrorCodes.MAILBOX_VALIDATION_FAILED`; remove existing try-catch boilerplate
- [X] T037 [P] [US2] Migrate `src/Rentier.Application/Handlers/SaveHolidayConfCommandHandler.cs` to `HandlerHelper.ExecuteAsync` with `ErrorCodes.HOLIDAY_SAVE_DUPLICATE_DATES` (primary code); remove existing try-catch boilerplate
- [X] T038 [P] [US2] Migrate `src/Rentier.Application/Handlers/SaveTaxpayerProfileCommandHandler.cs` to `HandlerHelper.ExecuteAsync` with appropriate `ErrorCodes.*` constant; remove existing try-catch boilerplate
- [X] T039 [P] [US2] Migrate `src/Rentier.Application/Handlers/UpdateFilingStatusCommandHandler.cs` to `HandlerHelper.ExecuteAsync` with appropriate `ErrorCodes.*` constant; remove existing try-catch boilerplate
- [X] T040 [P] [US2] Migrate `src/Rentier.Application/Handlers/UpdateMailboxCommandHandler.cs` to `HandlerHelper.ExecuteAsync` with `ErrorCodes.MAILBOX_VALIDATION_FAILED`; remove existing try-catch boilerplate
- [X] T041 [P] [US2] Migrate `src/Rentier.Application/Handlers/UpdatePaymentReferenceCommandHandler.cs` to `HandlerHelper.ExecuteAsync` with appropriate `ErrorCodes.*` constant; remove existing try-catch boilerplate

**Pattern C — `DomainException` + `Exception`**

- [X] T042 [P] [US2] Migrate `src/Rentier.Application/Handlers/SetUserPreferenceCommandHandler.cs` to `HandlerHelper.ExecuteAsync` with appropriate `ErrorCodes.*` constant; remove existing try-catch boilerplate
- [X] T043 [P] [US2] Migrate `src/Rentier.Application/Handlers/GetUserPreferenceQueryHandler.cs` to `HandlerHelper.ExecuteAsync` with appropriate `ErrorCodes.*` constant; remove existing try-catch boilerplate

**Partial Migrations — keep regex try-catch, use HandlerHelper for main body**

- [X] T044 [P] [US2] Partially migrate `src/Rentier.Application/Handlers/AddImporterCommandHandler.cs` — retain the `ArgumentException` try-catch that validates the regex pattern; wrap the main handler body (after regex validation) in `HandlerHelper.ExecuteAsync` with `ErrorCodes.IMPORTER_VALIDATION_INVALID_REGEX`
- [X] T045 [P] [US2] Partially migrate `src/Rentier.Application/Handlers/UpdateImporterCommandHandler.cs` — retain the `ArgumentException` try-catch that validates the regex pattern; wrap the main handler body (after regex validation) in `HandlerHelper.ExecuteAsync` with `ErrorCodes.IMPORTER_VALIDATION_INVALID_REGEX`

**Checkpoint**: SC-002 and SC-007 satisfied — 14 full + 2 partial migrations complete; `ProcessReportsCommandHandler` retains custom logic (documented exclusion). Run `dotnet test Rentier.slnx` to confirm all existing tests still pass.

---

## Phase 6: Polish & Final Verification

**Purpose**: Cross-cutting validation confirming all success criteria are met.

- [X] T046 Run full test suite and confirm zero regressions: `dotnet test Rentier.slnx` — all tests pass including `ErrorCodesTests`, `HandlerHelperTests`, `PaginationValidatorTests`, and all pre-existing handler tests
- [X] T047 Verify SC-001 — zero `.Result` / `.Wait()` calls in production code: `Get-ChildItem src -Recurse -Include *.cs | Select-String '\.Result[^s]|\.Wait\(' | Where-Object { $_.Line -notmatch '//|///|^\s*\*' }`
- [X] T048 Verify SC-003 — zero inline error code string literals remain in handler files: `Get-ChildItem src/Rentier.Application/Handlers -Recurse -Include *.cs | Select-String '"[A-Z_]{3,}"' | Where-Object { $_.Line -notmatch 'ErrorCodes\.' -and $_.Line -notmatch '//|///|^\s*\*' }`

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Baseline Verification
    └── Phase 2: US1 Async Fix (standalone, no blockers)
    └── Phase 3: US3 Error Code Registry (prerequisite for US2)
        └── Phase 4: US4 Pagination Contract (prerequisite for US2, parallel with US3)
            └── Phase 5: US2 Handler Error Helper (requires US3 + US4 complete)
                └── Phase 6: Polish & Verification
```

### User Story Dependencies

| Story | Spec Priority | Execution Phase | Depends On |
|---|---|---|---|
| US1 — Async Fix | P1 | Phase 2 | None — fully standalone |
| US3 — Error Codes | P3 | Phase 3 | None — start immediately after baseline |
| US4 — Pagination | P4 | Phase 4 | None — can run in parallel with US3 |
| US2 — HandlerHelper | P2 | Phase 5 | **Must follow US3 and US4** |

> **Why US2 (P2) executes last**: `HandlerHelper` references `ErrorCodes.*` constants (US3) and handler migrations use `PaginationValidator` (US4). This is a compile-time dependency, not just a preference.

### Within Each Phase

1. Create new files first (ErrorCodes, IPaginatedQuery, PaginationValidator, HandlerHelper)
2. Update reference points second (Error.cs factory methods)
3. Migrate handlers last (parallel — all different files)
4. Write or update tests to verify

### Handler Migration Order (within Phase 5)

Recommended order for risk management (start simple, finish complex):
1. Pattern B handlers first (T030–T035) — clearest pattern, easiest to verify
2. Pattern A handlers second (T036–T041) — DomainException only, straightforward
3. Pattern C handlers third (T042–T043) — combined pattern
4. Partial migrations last (T044–T045) — requires care with the two-try-catch structure

---

## Parallel Execution Examples

### Phase 3: Error Code Handler Migration (T006–T019)

All 14 handler files are independent. Once T004 (ErrorCodes.cs) and T005 (Error.cs) are complete, all migration tasks can run simultaneously:

```
T004 → T005 → [T006, T007, T008, T009, T010, T011, T012, T013, T014, T015, T016, T017, T018, T019] → T020
```

### Phase 4: Pagination (T021–T026)

```
T021 → [T022, T023] → T024 → [T025, T026] → T027
```

### Phase 5: Handler Migrations (T030–T045)

Once T028 (HandlerHelper.cs) is created, all 16 handler migrations are file-independent:

```
T028 → T029 → [T030, T031, T032, T033, T034, T035, T036, T037, T038, T039, T040, T041, T042, T043, T044, T045]
```

---

## Implementation Strategy

### MVP First (Phase 2 alone)

The async fix (T002–T003) is a 1-line change with the highest principle compliance value. Completing Phase 2 alone achieves SC-001 and full Constitution Principle IV compliance.

### Incremental Delivery

1. **Phase 2** → Green build + SC-001 ✅ (async fully compliant)
2. **Phase 3** → SC-003, SC-004 ✅ (centralized error codes, uniqueness verified)
3. **Phase 4** → SC-005 ✅ (pagination validation in one place)
4. **Phase 5** → SC-002, SC-007 ✅ (handler helper, 14 migrations)
5. **Phase 6** → SC-006 ✅ (zero regressions confirmed)

Each phase can be committed and reviewed independently before proceeding to the next.

### Handler-by-Handler Safety Net

After migrating each handler in Phase 5, run the handler's specific unit test file before proceeding to the next:

```powershell
dotnet test tests/Rentier.UnitTests/Rentier.UnitTests.csproj --filter "FullyQualifiedName~BulkDeleteFilings"
```

This catches regressions immediately at the file level without waiting for the full suite.

---

## Notes

- **ProcessReportsCommandHandler is explicitly excluded** from HandlerHelper migration (plan.md D4). It retains its 3-level nested try-catch with per-item error accumulation. This is documented, not an oversight.
- **DeleteMailboxCommandHandler** is the only location in the UI layer that matches on a specific error code string (`CREDENTIAL_NOT_FOUND`). This code is **not renamed** — it stays `CREDENTIAL_NOT_FOUND` in `ErrorCodes.cs` with the same string value.
- **[P] tasks** = different files, no incomplete sibling dependencies — safe to run in parallel
- **[Story] labels** map each task to its user story for traceability to spec.md
- Commit after each phase or logical group (create → migrate → test)
- Run `dotnet test` after every handler migration in Phase 5 to catch regressions early

