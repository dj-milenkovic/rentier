# Feature Specification: Cross-Platform Credential Store

**Feature Branch**: `feature/017-cross-platform-credential-store`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Implement cross-platform credential storage for Rentier"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Store and Retrieve IMAP Credentials on Any OS (Priority: P1)

A user running Rentier on Windows, macOS, or Linux adds a new IMAP mailbox. The system securely stores the password in the platform's native credential manager (Windows Credential Manager, macOS Keychain, or Linux Secret Service). When the user later syncs the mailbox, the system retrieves the stored password transparently — the user never sees or handles the raw credential outside the initial entry.

**Why this priority**: This is the core capability. Without cross-platform save/retrieve, the application cannot function on macOS or Linux at all. It also validates that the existing Windows path remains unbroken.

**Independent Test**: Can be fully tested by adding a mailbox with credentials on each supported platform, then retrieving those credentials via a sync operation. Delivers the fundamental value of cross-platform credential management.

**Acceptance Scenarios**:

1. **Given** a user on Windows adds a mailbox with an IMAP password, **When** the system stores the credential, **Then** it is persisted in Windows Credential Manager and can be retrieved for mailbox sync.
2. **Given** a user on macOS adds a mailbox with an IMAP password, **When** the system stores the credential, **Then** it is persisted in macOS Keychain and can be retrieved for mailbox sync.
3. **Given** a user on Linux with Secret Service available adds a mailbox with an IMAP password, **When** the system stores the credential, **Then** it is persisted via the Secret Service D-Bus protocol and can be retrieved for mailbox sync.
4. **Given** a user updates an existing mailbox password, **When** the system overwrites the stored credential, **Then** the new password replaces the old one in the platform credential store.

---

### User Story 2 - Deterministic Provider Selection at Startup (Priority: P2)

When Rentier starts, the system automatically detects the operating system and selects the appropriate credential store provider. The user receives clear diagnostic information about which provider was selected. If the preferred provider is unavailable (e.g., Secret Service daemon not running on Linux), the system reports the failure reason rather than silently degrading.

**Why this priority**: Users and support need to understand which provider is active and why fallback occurred. This enables troubleshooting and ensures no silent data loss from a missing credential backend.

**Independent Test**: Can be fully tested by launching the application on each platform and verifying that the diagnostic output reports the correct provider selection or a clear failure reason.

**Acceptance Scenarios**:

1. **Given** the application starts on Windows, **When** provider selection runs, **Then** the Windows Credential Manager provider is selected and the diagnostic result reports success with the selected provider name.
2. **Given** the application starts on macOS, **When** provider selection runs, **Then** the macOS Keychain provider is selected and the diagnostic result reports success with the selected provider name.
3. **Given** the application starts on Linux with Secret Service available, **When** provider selection runs, **Then** the Linux Secret Service provider is selected and the diagnostic result reports success.
4. **Given** the application starts on Linux without Secret Service running, **When** provider selection runs, **Then** the system returns a failure result with error code PROVIDER_UNAVAILABLE and a human-readable reason explaining that the Secret Service daemon is not reachable.
5. **Given** the application starts on an unsupported platform, **When** provider selection runs, **Then** the system returns a failure result with error code UNSUPPORTED_PLATFORM.

---

### User Story 3 - Delete Credentials When Mailbox Is Removed (Priority: P2)

When a user deletes a mailbox from Rentier, the system removes the associated IMAP password from the platform credential store. No orphaned credentials remain in the OS keychain after mailbox removal.

**Why this priority**: Credential hygiene is essential for security. Orphaned secrets in OS keystores create a risk surface and confuse users who inspect their credential managers manually.

**Independent Test**: Can be fully tested by adding a mailbox, verifying the credential exists in the OS store, deleting the mailbox, and then confirming the credential is no longer present.

**Acceptance Scenarios**:

1. **Given** a mailbox exists with stored credentials, **When** the user deletes the mailbox, **Then** the credential is removed from the platform credential store and a subsequent retrieval returns "not found."
2. **Given** a user deletes a mailbox whose credential was already manually removed from the OS store, **When** the delete operation runs, **Then** the system handles the missing credential gracefully (no error raised to the user).

---

### User Story 4 - Clear Error Reporting for Credential Operations (Priority: P3)

When a credential operation fails (save, retrieve, or delete), the system returns a structured error with a specific error code and a human-readable message. The user sees a meaningful explanation rather than a raw system exception.

**Why this priority**: Good error reporting reduces support burden and helps users self-diagnose issues (e.g., locked keychain, insufficient permissions).

**Independent Test**: Can be tested by simulating credential operation failures and verifying that structured error codes and messages are returned.

**Acceptance Scenarios**:

1. **Given** a credential save fails due to a platform-level error, **When** the operation returns, **Then** the result contains error code CREDENTIAL_WRITE_FAILED and a descriptive message.
2. **Given** a credential retrieval finds no matching entry, **When** the operation returns, **Then** the result contains error code CREDENTIAL_NOT_FOUND.
3. **Given** a credential deletion fails due to a platform-level error, **When** the operation returns, **Then** the result contains error code CREDENTIAL_DELETE_FAILED and a descriptive message.

---

### Edge Cases

- What happens when the OS credential store is locked (e.g., macOS Keychain locked after sleep)? The system returns PROVIDER_UNAVAILABLE with a message indicating the store is inaccessible.
- What happens when the credential key exceeds the platform's maximum key length? The system returns CREDENTIAL_WRITE_FAILED with a descriptive message about the key constraint.
- What happens when the user runs Rentier in a headless Linux environment with no Secret Service daemon? Provider selection returns PROVIDER_UNAVAILABLE and the application reports the issue at startup rather than crashing.
- What happens when concurrent operations attempt to read and write the same credential? Each platform's native credential store handles serialization; the application does not add its own locking.
- What happens when a credential's value contains special characters (Unicode, newlines, null bytes)? The system stores and retrieves the value faithfully via UTF-8 encoding, preserving all characters.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide an `ICredentialStore` interface in the Application layer with operations: save, retrieve, and delete credentials by key.
- **FR-002**: System MUST implement a Windows Credential Manager provider that stores credentials as generic credentials, updated to return structured results (`Result<T, Error>`) instead of throwing exceptions.
- **FR-003**: System MUST implement a macOS Keychain provider that stores credentials via the macOS Security framework Keychain API.
- **FR-004**: System MUST implement a Linux Secret Service provider that stores credentials via the D-Bus Secret Service protocol (compatible with GNOME Keyring and KDE Wallet).
- **FR-005**: System MUST perform deterministic provider selection at startup based on the detected operating system, returning a `Result<T, Error>` that includes the selected provider name or a failure reason.
- **FR-006**: System MUST return structured errors with specific error codes: UNSUPPORTED_PLATFORM, CREDENTIAL_NOT_FOUND, CREDENTIAL_WRITE_FAILED, CREDENTIAL_DELETE_FAILED, PROVIDER_UNAVAILABLE.
- **FR-007**: System MUST NEVER store passwords in SQLite databases, plaintext files, or environment variables — only in OS-managed credential stores.
- **FR-008**: System MUST use the key format `Rentier/<entity-type>/<entity-id>/<field>` for all credential entries, consistent with the existing convention.
- **FR-009**: System MUST handle the case where a credential does not exist during retrieval by returning CREDENTIAL_NOT_FOUND rather than null or an exception.
- **FR-010**: System MUST handle the case where a credential does not exist during deletion gracefully (idempotent delete — no error if already absent).
- **FR-011**: System MUST perform all credential store I/O asynchronously.
- **FR-012**: Provider selection MUST be resolved during application startup (DI composition) and MUST NOT change at runtime.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: `ICredentialStore` interface remains in `Rentier.Application/Interfaces`. All platform-specific providers live in `Rentier.Infrastructure/Security`. Provider selection logic belongs in Infrastructure's DI registration. Clean Architecture boundaries are preserved — Application depends on the abstraction, Infrastructure provides implementations.
- **CA-002 (Money and Dates)**: No monetary or date fields are involved in credential storage. N/A for this feature.
- **CA-003 (Privacy and Security)**: Credentials are stored exclusively in OS-managed credential stores. The security invariant — IMAP passwords MUST NEVER be stored in SQLite, plaintext files, or environment variables — is the central constraint of this feature.
- **CA-004 (Network Scope)**: No new outbound network calls are introduced. D-Bus communication for Linux Secret Service is a local IPC mechanism, not a network call.
- **CA-005 (Async and UI)**: All credential operations (`SaveCredentialAsync`, `GetCredentialAsync`, `DeleteCredentialAsync`) are async. Provider selection at startup is synchronous (part of DI composition) but does not perform I/O — it only detects the platform and registers the provider.
- **CA-006 (Testing Impact)**: New tests required at Infrastructure layer (provider selection logic, per-platform read/write/delete behavior via integration tests). Application layer handler tests already mock `ICredentialStore` and remain unchanged. New unit tests for provider selection logic and error code mapping.

### Key Entities *(include if feature involves data)*

- **Credential**: A secret value (e.g., IMAP password) identified by a structured key (`Rentier/<entity-type>/<entity-id>/<field>`). Not persisted in any database — exists only in the OS credential store.
- **Credential Provider**: A platform-specific implementation that communicates with the OS credential store (Windows Credential Manager, macOS Keychain, Linux Secret Service). Selected deterministically at startup.
- **Provider Selection Result**: A diagnostic value returned during startup indicating which provider was selected or why selection failed. Contains the provider name on success or an error code with reason on failure.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users on all three supported platforms (Windows, macOS, Linux) can add a mailbox, store credentials, sync mail, and delete the mailbox with credential cleanup — full lifecycle works end-to-end.
- **SC-002**: Provider selection completes during application startup in under 1 second on all platforms, with a clear diagnostic result available for logging.
- **SC-003**: No credential data appears in any SQLite database, log file, plaintext configuration file, or environment variable at any point during the application lifecycle — verifiable by inspection.
- **SC-004**: When the credential store is unavailable (e.g., no Secret Service on Linux), the application reports a clear, actionable error within 5 seconds rather than crashing or hanging.
- **SC-005**: All credential operations (save, retrieve, delete) complete within 2 seconds under normal conditions on each supported platform.
- **SC-006**: Existing Windows users experience no change in behavior — the Windows credential path continues to function identically to the current implementation.

## Assumptions

- Users on macOS have the default Keychain available (standard on all macOS installations since OS X 10.0).
- Users on Linux who need credential storage have a Secret Service-compatible daemon running (GNOME Keyring or KDE Wallet). Headless/minimal Linux installations without a desktop environment are out of scope for automatic credential storage — these users receive a clear error at startup.
- The existing `ICredentialStore` interface contract (save, get, delete with string key and string secret) is sufficient for all platforms and does not need method signature changes beyond wrapping returns in `Result<T, Error>`.
- Each platform's native credential store handles its own concurrency and thread-safety; the application does not add an additional synchronization layer.
- The application is not expected to migrate credentials between platforms (e.g., moving from Windows to macOS). Each platform's credential store is independent.
- WSL (Windows Subsystem for Linux) environments are treated as Linux for provider selection purposes; users in WSL are expected to have Secret Service available or receive the PROVIDER_UNAVAILABLE error.
