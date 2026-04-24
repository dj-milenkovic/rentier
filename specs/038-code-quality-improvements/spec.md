# Feature Specification: Code Quality Improvements

**Feature Branch**: `038-code-quality-improvements`  
**Created**: 2025-07-24  
**Status**: Draft  
**Input**: User description: "Four code quality improvements identified in the DevOps analysis: Fix .Result anti-pattern in MacOsCredentialStore, extract handler error handling helper, standardize error codes, extract pagination validation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Eliminate Blocking Async Anti-Pattern (Priority: P1)

As a developer maintaining the credential store, the blocking `.Result` call on completed tasks in the macOS credential store must be replaced with a proper async pattern. Although the tasks are awaited via `Task.WhenAll` before `.Result` is accessed, this pattern violates the project's constitution (Principle IV) and sets a precedent for misuse elsewhere. Replacing it ensures the codebase has zero `.Result` / `.Wait()` calls, making static analysis rules enforceable without exceptions.

**Why this priority**: This is a correctness and constitution-compliance issue. The `.Result` anti-pattern can cause deadlocks in certain synchronization contexts and violates the project's own async standards. Fixing it is a targeted, low-risk change with high principle value.

**Independent Test**: Can be fully tested by running the credential store operations on macOS and verifying that reading, writing, and deleting credentials complete without deadlocks or exceptions, and that no `.Result` or `.Wait()` calls remain in the codebase.

**Acceptance Scenarios**:

1. **Given** the macOS credential store is invoked to read a credential, **When** the external process completes, **Then** stdout and stderr are captured using an async-only pattern with no `.Result` or `.Wait()` calls.
2. **Given** the macOS credential store is invoked on a slow system, **When** the process takes longer than expected, **Then** the async operation remains non-blocking and respects cancellation tokens.
3. **Given** a developer searches the entire codebase for `.Result` or `.Wait()` on task types, **When** the search completes, **Then** zero matches are found in production code.

---

### User Story 2 - Centralize Handler Error Handling (Priority: P2)

As a developer adding or modifying CQRS handlers, there should be a single shared mechanism for the common try-catch error handling pattern. Currently, 17+ handlers independently implement variations of the same pattern: try the operation, catch `OperationCanceledException` (rethrow), catch domain exceptions (return domain failure), and catch generic exceptions (return infrastructure failure). A shared helper eliminates the repetition and ensures all handlers follow the same error classification rules consistently.

**Why this priority**: This is the highest-impact refactoring in terms of lines of code affected (17+ handlers) and consistency gains. Every new handler must currently copy-paste the error handling pattern, risking drift and inconsistency.

**Independent Test**: Can be fully tested by verifying that each handler using the shared helper correctly propagates cancellation, maps domain exceptions to domain failures, and maps unexpected exceptions to infrastructure failures. All existing handler tests must continue to pass.

**Acceptance Scenarios**:

1. **Given** a handler uses the shared error handling helper, **When** a domain exception is thrown during execution, **Then** the handler returns a domain failure result with the exception message.
2. **Given** a handler uses the shared error handling helper, **When** an `OperationCanceledException` is thrown, **Then** the exception is re-thrown (not swallowed) to propagate cancellation.
3. **Given** a handler uses the shared error handling helper, **When** an unexpected exception occurs, **Then** the handler returns an infrastructure failure result with an appropriate error code.
4. **Given** a new handler is created, **When** the developer uses the shared helper, **Then** the handler has consistent error handling without writing any try-catch boilerplate.

---

### User Story 3 - Standardize Error Codes (Priority: P3)

As a developer debugging failures or writing error-handling logic in the UI layer, all error codes returned by handlers should follow a consistent, discoverable naming convention. Currently, some handlers use descriptive codes (e.g., "GET_REPORTS_FAILED", "IMPORTER_NOT_FOUND") while others use generic ones (e.g., "VALIDATION_ERROR", "DOMAIN_ERROR"). A centralized constants registry ensures every error code is unique, discoverable, and consistently formatted.

**Why this priority**: Inconsistent error codes make it difficult to programmatically handle specific failure cases in the UI and make log analysis unreliable. Standardizing codes is a prerequisite for robust error handling in the presentation layer.

**Independent Test**: Can be fully tested by verifying that all handler error codes reference the centralized constants (no string literals), that each code follows the naming convention, and that no duplicate codes exist.

**Acceptance Scenarios**:

1. **Given** the centralized error codes registry exists, **When** a developer searches for string literal error codes in handlers, **Then** zero inline string literal error codes are found — all reference the constants.
2. **Given** a handler returns a failure, **When** the error code is inspected, **Then** it follows the `ENTITY_ACTION_REASON` naming pattern (e.g., "REPORT_DELETE_FAILED", "FILING_VALIDATION_PAGE_INVALID").
3. **Given** the constants registry is reviewed, **When** all defined codes are listed, **Then** no two codes have the same string value.
4. **Given** a new handler needs a new error code, **When** the developer adds it, **Then** it is added to the centralized registry following the established naming convention.

---

### User Story 4 - Extract Shared Pagination Validation (Priority: P4)

As a developer working on paginated queries, the validation logic for page number and page size should be defined once and shared across all paginated query handlers. Currently, the filings and reports query handlers duplicate identical validation: page must be ≥ 1, page size must be between 1 and 100. Extracting this into a shared contract ensures consistency and makes it trivial to add pagination to future queries.

**Why this priority**: While the duplication currently affects only two handlers, the pattern will recur as more paginated queries are added. Fixing it now prevents further copy-paste and establishes a reusable contract.

**Independent Test**: Can be fully tested by verifying that both paginated query handlers apply the same validation rules, that invalid pagination parameters produce consistent error responses, and that adding a new paginated query automatically inherits the validation.

**Acceptance Scenarios**:

1. **Given** a paginated query with page = 0, **When** the query is executed, **Then** a validation failure is returned with a consistent error code and message indicating page must be ≥ 1.
2. **Given** a paginated query with page size = 150, **When** the query is executed, **Then** a validation failure is returned indicating page size must be between 1 and 100.
3. **Given** a paginated query with valid pagination (page = 1, size = 30), **When** the query is executed, **Then** pagination validation passes and the query proceeds to data retrieval.
4. **Given** a new paginated query handler is created, **When** it implements the shared pagination contract, **Then** it inherits page and page size validation without duplicating any logic.

---

### Edge Cases

- What happens when handlers catch both `DomainException` and `ArgumentException`? The shared helper must allow handler-specific exception types (e.g., `ArgumentException` for regex validation in importer handlers) to be handled before the generic pattern kicks in.
- What happens when an existing handler has custom error codes that don't fit the standard naming pattern? Migration must preserve backward compatibility for any UI code that matches on specific error code strings.
- What happens when a handler needs a unique error code not yet in the registry? The process for adding new codes must be documented and the registry must be extensible.
- What happens when page and page size are at exact boundary values (page = 1, size = 1 or size = 100)? Boundary values must be accepted, not rejected.
- What happens when a paginated query has additional validation beyond pagination (e.g., sort column validation in filings)? The shared pagination validation must compose with handler-specific validation, not replace it.

## Requirements *(mandatory)*

### Functional Requirements

**Async Correctness (Story 1)**

- **FR-001**: The system MUST NOT use `.Result` or `.Wait()` on any task in the macOS credential store process execution path; all task results MUST be obtained via `await`.
- **FR-002**: The system MUST NOT contain any `.Result` or `.Wait()` calls on task types anywhere in production code across all layers.

**Handler Error Handling (Story 2)**

- **FR-003**: The system MUST provide a shared error handling mechanism in the Application layer that wraps handler operations with a standard try-catch pattern.
- **FR-004**: The shared error handler MUST re-throw `OperationCanceledException` to propagate cancellation correctly.
- **FR-005**: The shared error handler MUST catch domain-specific exceptions and return a domain failure result containing the exception message.
- **FR-006**: The shared error handler MUST catch all other exceptions and return an infrastructure failure result with the handler's designated error code.
- **FR-007**: The shared error handler MUST allow handlers to perform pre-operation validation and return validation failures before invoking the wrapped operation.
- **FR-008**: Handlers that have custom exception handling (e.g., `ArgumentException` for regex validation) MUST be able to either extend the shared pattern or opt out of it for those specific cases.

**Error Code Standardization (Story 3)**

- **FR-009**: The system MUST provide a centralized error codes registry in the Application layer containing all error codes used by handlers.
- **FR-010**: All handler error codes MUST reference the centralized registry; no handler may use inline string literals for error codes.
- **FR-011**: Error codes MUST follow a consistent naming convention: `ENTITY_ACTION_REASON` format using SCREAMING_SNAKE_CASE.
- **FR-012**: Each error code in the registry MUST be unique — no two constants may share the same string value.
- **FR-013**: Existing error codes that are already descriptive and specific (e.g., "IMPORTER_NOT_FOUND", "PARSE_FAILED") MUST be preserved or mapped to equivalent standard names to maintain backward compatibility.

**Pagination Validation (Story 4)**

- **FR-014**: The system MUST provide a shared pagination contract that paginated queries can implement to declare they support paging.
- **FR-015**: The shared pagination validation MUST enforce: page ≥ 1 and page size between 1 and 100 (inclusive).
- **FR-016**: The shared pagination validation MUST return a validation failure result with a standardized error code from the centralized registry.
- **FR-017**: Paginated query handlers MUST use the shared validation instead of implementing their own page/page-size checks.
- **FR-018**: The shared pagination contract MUST compose with handler-specific validation — it supplements but does not replace additional validation logic a handler may need.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature impacts the **Application layer** (shared error handler, error codes registry, pagination contract) and the **Infrastructure layer** (async fix in credential store). All new shared constructs live in the Application layer. The handler refactoring stays within existing handler files. No new cross-layer dependencies are introduced. Clean Architecture boundaries remain valid.
- **CA-002 (Money and Dates)**: This feature does not introduce or modify any monetary or date fields. No impact.
- **CA-003 (Privacy and Security)**: The credential store fix improves the reliability of the existing OS credential storage mechanism. No new secrets are introduced. The security boundary remains local-first with OS credential stores.
- **CA-004 (Network Scope)**: No network calls are added or modified. The credential store fix changes how process output is read, not which processes are invoked. No impact to allowed endpoints.
- **CA-005 (Async and UI)**: This feature directly addresses Principle IV compliance by eliminating the `.Result` anti-pattern. After this feature, the codebase will have zero `.Result`/`.Wait()` calls on tasks in production code. All I/O remains async.
- **CA-006 (Testing Impact)**: **Application layer**: Unit tests for the shared error handler, error codes uniqueness, and pagination validation. Existing handler tests must be updated to verify they use the shared mechanisms. **Infrastructure layer**: The credential store async fix must be verified through integration tests on the macOS platform. **Desktop layer**: No direct test changes, but verify that UI error handling still works with the standardized error codes.

### Key Entities

- **Error Code**: A unique, human-readable string constant identifying a specific failure category. Key attributes: code string (unique, SCREAMING_SNAKE_CASE, `ENTITY_ACTION_REASON` format), associated error severity (domain vs infrastructure).
- **Paginated Query**: A query that supports paging through results. Key attributes: page number (integer ≥ 1), page size (integer 1–100). Relationship: implemented by any query handler that returns paged results.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero `.Result` or `.Wait()` calls exist on task types in production code (verifiable via codebase search).
- **SC-002**: The number of distinct try-catch error handling patterns across handlers is reduced from 3+ variations to 1 shared mechanism, with at least 12 of the 17 affected handlers migrated.
- **SC-003**: 100% of handler error codes reference the centralized registry — zero inline string literal error codes remain in handler files.
- **SC-004**: All error codes in the registry are unique and follow the `ENTITY_ACTION_REASON` naming convention (verifiable via a unit test).
- **SC-005**: Pagination validation logic exists in exactly one location, with both existing paginated query handlers (filings and reports) using the shared contract.
- **SC-006**: All existing automated tests pass after the refactoring with no regressions.
- **SC-007**: Adding a new CQRS handler with standard error handling requires zero lines of try-catch boilerplate (only a call to the shared helper).

## Assumptions

- The three distinct try-catch variations identified in the codebase analysis (DomainException only, OperationCanceledException + Exception, and DomainException + Exception) can all be unified under a single shared mechanism with appropriate configuration points.
- Handlers with highly custom error handling (e.g., `ProcessReportsCommandHandler` with its multi-stage pipeline and per-report error accumulation) may need to retain some custom logic and will be migrated on a best-effort basis.
- The `ENTITY_ACTION_REASON` naming convention is compatible with all existing error codes — any codes that don't fit will be renamed with backward-compatible mappings if UI code depends on specific code strings.
- The page size upper bound of 100 is sufficient for all current and foreseeable query use cases in the application.
- This is a purely internal refactoring — no user-facing behavior changes, no UI modifications, no data model changes.
- Handlers that currently have no try-catch (pure validation + delegation) are not required to adopt the shared helper if they have no need for exception handling.
