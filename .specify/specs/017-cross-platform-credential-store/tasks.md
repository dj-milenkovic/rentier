---
description: "Task list for Feature 017: Cross-Platform Credential Store"
feature: "017-cross-platform-credential-store"
branch: "feature/017-cross-platform-credential-store"
generated: "2025-07-15"
spec: ".specify/specs/017-cross-platform-credential-store/spec.md"
plan: "specs/feature/017-cross-platform-credential-store/plan.md"
---

# Tasks: Cross-Platform Credential Store

**Feature**: 017 — Cross-Platform Credential Store  
**Branch**: `feature/017-cross-platform-credential-store`  
**Design Docs**: `specs/feature/017-cross-platform-credential-store/`

## Format: `[ID] [P?] [Story?] Description with file path`

- **[P]**: Can run in parallel with other [P] tasks in the same phase (different files, no shared dependencies)
- **[US#]**: Maps task to a specific user story from spec.md
- Tests are included per project constitution quality gates

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the Linux NuGet dependency and create the shared test infrastructure project. All of Phase 2 depends on Phase 1 completing first.

- [X] T001 Add `Ace4896.DBus.Services.Secrets` NuGet package to `src/Rentier.Infrastructure/Rentier.Infrastructure.csproj` (unconditional reference — inert on Windows/macOS at runtime, active on Linux)
- [X] T002 Create `tests/Rentier.Tests.Common/` xUnit test helper project: add `Rentier.Tests.Common.csproj` referencing `Rentier.Application`, `xunit`, `FluentAssertions`, and `NSubstitute`; create `Fakes/` subdirectory
- [X] T003 Add `<ProjectReference>` to `Rentier.Tests.Common` in both `tests/Rentier.Application.Tests/Rentier.Application.Tests.csproj` and `tests/Rentier.Infrastructure.Tests/Rentier.Infrastructure.Tests.csproj`

**Checkpoint**: Infrastructure NuGet added, Tests.Common project created and referenced — all subsequent work can begin.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core abstractions and shared utilities that ALL user stories depend on. The `ICredentialStore` interface update is the central change — every provider, handler, and test depends on it.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete. The `ICredentialStore` interface change (T006) is a breaking change across the entire codebase.

- [X] T004 [P] Add 5 credential error factory methods to `src/Rentier.Application/Common/Error.cs`: `CredentialNotFound(string key)`, `CredentialWriteFailed(string message)`, `CredentialDeleteFailed(string message)`, `ProviderUnavailable(string reason)`, `UnsupportedPlatform(string os)` — all return `new Error(CODE, message)` matching data-model.md spec
- [X] T005 [P] Create `CredentialKeys` utility class at `src/Rentier.Application/Common/CredentialKeys.cs` with `static string MailboxPassword(Guid mailboxId) => $"Rentier/Mailbox/{mailboxId}/password"` — this is the single source of truth for key format and fixes the save/read mismatch bug (analysis.md §2)
- [X] T006 Update `ICredentialStore` interface at `src/Rentier.Application/Interfaces/ICredentialStore.cs`: change signatures to `Task<Result<VoidResult, Error>> SaveCredentialAsync(string key, string secret, CancellationToken ct = default)`, `Task<Result<string, Error>> GetCredentialAsync(string key, CancellationToken ct = default)`, `Task<Result<VoidResult, Error>> DeleteCredentialAsync(string key, CancellationToken ct = default)` per contracts/application-contracts.md; add XML doc comments
- [X] T007 [P] Create `ProviderInfo` value record at `src/Rentier.Infrastructure/Security/ProviderInfo.cs`: `public sealed record ProviderInfo(string ProviderName, string Platform)` with `ToString()` returning `$"{ProviderName} ({Platform})"` per data-model.md
- [X] T008 Create `FakeCredentialStore` at `tests/Rentier.Tests.Common/Fakes/FakeCredentialStore.cs`: `ConcurrentDictionary<string,string>`-backed `ICredentialStore` implementation; `SaveCredentialAsync` upserts and returns `Success(VoidResult.Value)`; `GetCredentialAsync` returns `Success(secret)` or `Failure(Error.CredentialNotFound(key))`; `DeleteCredentialAsync` removes idempotently and returns `Success(VoidResult.Value)`; expose `StoredKeys` and `Clear()` test helpers per contracts/application-contracts.md §FakeCredentialStore

**Checkpoint**: Foundation complete — interface updated, error codes defined, key utility created, fake store ready. All three user story phases can now begin (sequentially or in parallel if staffed).

---

## Phase 3: User Story 1 — Store and Retrieve IMAP Credentials on Any OS (Priority: P1) 🎯 MVP

**Goal**: All three platform providers exist and correctly implement `ICredentialStore`. The three affected command handlers and the sync service are updated to use `CredentialKeys.MailboxPassword()` and handle `Result<T, Error>` returns. The critical key-format bug (save used `Rentier/Mailbox/{id}`, read used `Rentier/Mailbox/{id}/password`) is fixed.

**Independent Test**: On Windows — add a mailbox, confirm credential in Windows Credential Manager (`rundll32 keymgr.dll, KRShowKeyMgr`), sync mailbox (retrieves credential), delete mailbox (credential removed). Repeat on macOS (Keychain Access app) and Linux (GNOME Keyring or `secret-tool lookup application Rentier`).

### Implementation for User Story 1

- [X] T009 [P] [US1] Create `WindowsCredentialStore` at `src/Rentier.Infrastructure/Security/WindowsCredentialStore.cs`: extract all P/Invoke declarations (`CredWriteW`, `CredReadW`, `CredDeleteW`, `CredFreeW`, `CREDENTIAL`, `CREDENTIAL_ATTRIBUTE` structs) from `OsCredentialStore.cs`; keep `[SupportedOSPlatform("windows")]`; wrap `CredWriteW` in `Task.Run()` returning `Result<VoidResult, Error>` (`Win32Exception` → `Error.CredentialWriteFailed`); wrap `CredReadW` returning `Result<string, Error>` (`ERROR_NOT_FOUND 1168` → `Error.CredentialNotFound(key)`); wrap `CredDeleteW` returning `Result<VoidResult, Error>` (idempotent: `ERROR_NOT_FOUND` treated as success, other `Win32Exception` → `Error.CredentialDeleteFailed`)
- [X] T010 [P] [US1] Create `MacOsCredentialStore` at `src/Rentier.Infrastructure/Security/MacOsCredentialStore.cs`: `[SupportedOSPlatform("osx")]`; `SaveCredentialAsync` runs `security add-generic-password -a "Rentier" -s {key} -w {secret} -U` via `Process.Start`, exit 0 → `Success`, non-zero → `Error.CredentialWriteFailed`; `GetCredentialAsync` runs `security find-generic-password -a "Rentier" -s {key} -w`, exit 0 → `Success(stdout.Trim())`, exit 44 → `Error.CredentialNotFound(key)`, other → `Error.CredentialWriteFailed`; `DeleteCredentialAsync` runs `security delete-generic-password -a "Rentier" -s {key}`, exit 0 or 44 → `Success`, other → `Error.CredentialDeleteFailed`; all wrapped in `Task.Run()`
- [X] T011 [P] [US1] Create `LinuxCredentialStore` at `src/Rentier.Infrastructure/Security/LinuxCredentialStore.cs`: `[SupportedOSPlatform("linux")]`; constructor takes `SecretService service`; build lookup attributes `{ "application", "Rentier" }, { "key", key }`; `SaveCredentialAsync` calls `collection.CreateItemAsync("Rentier Credential", attributes, UTF8 bytes, "text/plain; charset=utf8", replaceExisting: true)` — `DBusException` → `Error.CredentialWriteFailed`; `GetCredentialAsync` calls `collection.SearchItemsAsync(attributes)`, empty result → `Error.CredentialNotFound(key)`, `DBusException` → `Error.ProviderUnavailable`; `DeleteCredentialAsync` calls `items[0].DeleteAsync()` if found, empty → `Success` (idempotent), `DBusException` → `Error.CredentialDeleteFailed`
- [X] T012 [P] [US1] Update `AddMailboxCommandHandler` at `src/Rentier.Application/Handlers/AddMailboxCommandHandler.cs`: replace `$"Rentier/Mailbox/{mailbox.Id}"` with `CredentialKeys.MailboxPassword(mailbox.Id)`; change `await _credentials.SaveCredentialAsync(...)` to `var credResult = await _credentials.SaveCredentialAsync(...)`; add `if (!credResult.IsSuccess) return Result<Guid, Error>.Failure(credResult.Error);` per contracts/application-contracts.md §Consumer Migration Guide
- [X] T013 [P] [US1] Update `UpdateMailboxCommandHandler` at `src/Rentier.Application/Handlers/UpdateMailboxCommandHandler.cs`: replace hardcoded key with `CredentialKeys.MailboxPassword(mailbox.Id)`; wrap `SaveCredentialAsync` result in `if (!credResult.IsSuccess) return` guard matching the AddMailbox pattern
- [X] T014 [P] [US1] Update `ImapMailboxSyncService` at `src/Rentier.Infrastructure/Sync/ImapMailboxSyncService.cs`: replace `$"Rentier/Mailbox/{mailbox.Id}/password"` with `CredentialKeys.MailboxPassword(mailbox.Id)`; change `var password = await _credentialStore.GetCredentialAsync(...)` to `var credResult = await _credentialStore.GetCredentialAsync(...); if (!credResult.IsSuccess) return Result<SyncResult, Error>.Failure(credResult.Error); var password = credResult.Value;`

### Tests for User Story 1

- [X] T015 [P] [US1] Create `WindowsCredentialStoreTests` at `tests/Rentier.Infrastructure.Tests/Security/WindowsCredentialStoreTests.cs`: `[SupportedOSPlatform("windows")]` facts guarded with `Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))`; tests: save and retrieve round-trip returns same secret; save overwrites existing credential (upsert); get on absent key returns `CREDENTIAL_NOT_FOUND`; delete removes stored credential; delete on absent key returns `Success` (idempotent); all test keys use `$"Rentier/Test/{Guid.NewGuid()}/password"` with cleanup in `finally`
- [X] T016 [P] [US1] Create `MacOsCredentialStoreTests` at `tests/Rentier.Infrastructure.Tests/Security/MacOsCredentialStoreTests.cs`: `[SupportedOSPlatform("osx")]` facts guarded with platform skip; tests: save/retrieve round-trip; upsert overwrites; get absent key returns `CREDENTIAL_NOT_FOUND` (maps exit 44); delete removes; idempotent delete; `security` binary not found → `CREDENTIAL_WRITE_FAILED` (mock `Process.Start` via interface injection or test subclass)
- [X] T017 [P] [US1] Create `LinuxCredentialStoreTests` at `tests/Rentier.Infrastructure.Tests/Security/LinuxCredentialStoreTests.cs`: `[SupportedOSPlatform("linux")]` facts guarded with platform skip + daemon availability check; tests: save/retrieve round-trip via `Ace4896.DBus.Services.Secrets`; upsert overwrites; empty search returns `CREDENTIAL_NOT_FOUND`; delete removes; idempotent delete returns `Success`; `DBusException` during `GetCredentialAsync` returns `PROVIDER_UNAVAILABLE`
- [X] T018 [P] [US1] Update `AddMailboxCommandHandlerTests` at `tests/Rentier.Application.Tests/AddMailboxCommandHandlerTests.cs`: replace `NSubstitute` mock `SaveCredentialAsync` setup from `void` to `Task<Result<VoidResult, Error>>`; add test asserting `StoredKeys.Single()` matches `CredentialKeys.MailboxPassword(mailbox.Id)` using `FakeCredentialStore`; add test asserting handler returns `Failure` when `SaveCredentialAsync` returns `Error.CredentialWriteFailed`
- [X] T019 [P] [US1] Update `UpdateMailboxCommandHandlerTests` at `tests/Rentier.Application.Tests/UpdateMailboxCommandHandlerTests.cs`: update NSubstitute mock signatures for `SaveCredentialAsync` to return `Task<Result<VoidResult, Error>>`; add test verifying updated key format uses `CredentialKeys.MailboxPassword`; add test asserting handler propagates `CREDENTIAL_WRITE_FAILED` on save failure
- [X] T020 [P] [US1] Update `SyncMailboxCommandHandlerTests` at `tests/Rentier.Application.Tests/SyncMailboxCommandHandlerTests.cs`: update NSubstitute mock setup for `GetCredentialAsync` from `Task<string?>` to `Task<Result<string, Error>>`; add test asserting sync returns `CREDENTIAL_NOT_FOUND` when credential is absent; verify `FakeCredentialStore` can be used as a drop-in for mock in happy-path tests

**Checkpoint**: User Story 1 fully functional — all three providers implemented, key-format bug fixed, handlers updated, tests cover happy-path and error-path for each provider.

---

## Phase 4: User Story 2 — Deterministic Provider Selection at Startup (Priority: P2)

**Goal**: `CredentialStoreFactory.CreateAsync()` selects the correct provider based on `RuntimeInformation.IsOSPlatform()`, probes Linux Secret Service availability, and returns `Result<(ICredentialStore, ProviderInfo), Error>`. `InfrastructureServiceExtensions` registers the provider as a `Singleton` and handles factory failure gracefully via `NullCredentialStore`.

**Independent Test**: Launch app on Windows → logs "Windows Credential Manager (Windows)". Launch on macOS → logs "macOS Keychain (macOS)". Launch on Linux with daemon → logs "Linux Secret Service (Linux)". Launch on Linux without daemon → startup reports `PROVIDER_UNAVAILABLE` with reason string.

### Implementation for User Story 2

- [X] T021 [US2] Create `CredentialStoreFactory` at `src/Rentier.Infrastructure/Security/CredentialStoreFactory.cs`: `public static class CredentialStoreFactory` with `public static async Task<Result<(ICredentialStore Store, ProviderInfo Info), Error>> CreateAsync()`; algorithm: (1) `IsOSPlatform(Windows)` → `Success(new WindowsCredentialStore(), new ProviderInfo("Windows Credential Manager", "Windows"))`; (2) `IsOSPlatform(OSX)` → `Success(new MacOsCredentialStore(), new ProviderInfo("macOS Keychain", "macOS"))`; (3) `IsOSPlatform(Linux)` → try `SecretService.ConnectAsync(EncryptionType.Dh)`, success → `Success(new LinuxCredentialStore(service), new ProviderInfo("Linux Secret Service", "Linux"))`, `DBusException` → `Failure(Error.ProviderUnavailable("Secret Service daemon is not running..."))`; (4) else → `Failure(Error.UnsupportedPlatform(RuntimeInformation.OSDescription))`
- [X] T022 [P] [US2] Create `NullCredentialStore` at `src/Rentier.Infrastructure/Security/NullCredentialStore.cs`: `ICredentialStore` implementation that takes an `Error providerError` in its constructor and returns `Task.FromResult(Result<T,Error>.Failure(providerError))` for all three operations — used when factory fails at startup so the app starts but reports errors clearly on first credential access
- [X] T023 [US2] Update `InfrastructureServiceExtensions` at `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`: remove `#pragma warning disable CA1416` and `AddTransient<ICredentialStore, OsCredentialStore>()`; add `var factoryResult = await CredentialStoreFactory.CreateAsync()`; on success: `services.AddSingleton<ICredentialStore>(factoryResult.Value.Store)` + log provider info; on failure: `services.AddSingleton<ICredentialStore>(new NullCredentialStore(factoryResult.Error))` + log warning; make method `async Task` if not already; change registration lifetime from Transient to Singleton

### Tests for User Story 2

- [X] T024 [P] [US2] Create `CredentialStoreFactoryTests` at `tests/Rentier.Infrastructure.Tests/Security/CredentialStoreFactoryTests.cs`: test OS-platform selection using `RuntimeInformation.IsOSPlatform()` guard skips; test Windows selection on Windows runner returns `WindowsCredentialStore` + `ProviderInfo("Windows Credential Manager", "Windows")`; test macOS selection on macOS runner; test Linux success returns `LinuxCredentialStore` + correct `ProviderInfo`; test Linux `DBusException` returns `Failure(PROVIDER_UNAVAILABLE)`; test that `UnsupportedPlatform` error includes OS description string; verify `ProviderInfo.ToString()` format

**Checkpoint**: Provider selection works deterministically on all platforms. DI registration is platform-aware and fails gracefully. User Story 2 independently verifiable.

---

## Phase 5: User Story 3 — Delete Credentials When Mailbox Is Removed (Priority: P2)

**Goal**: `DeleteMailboxCommandHandler` uses `CredentialKeys.MailboxPassword()`, removes the exception-swallowing `try/catch` antipattern, and relies on `ICredentialStore`'s idempotent delete contract. No orphaned credentials remain after mailbox deletion.

**Independent Test**: Add a mailbox (credential saved), verify key in OS store, delete the mailbox, confirm `DeleteCredentialAsync` was called with the correct key and no exception is thrown for a missing credential.

### Implementation for User Story 3

- [X] T025 [US3] Update `DeleteMailboxCommandHandler` at `src/Rentier.Application/Handlers/DeleteMailboxCommandHandler.cs`: replace `$"Rentier/Mailbox/{command.Id}"` with `CredentialKeys.MailboxPassword(command.Id)`; remove entire `try { await _credentials.DeleteCredentialAsync(...) } catch { }` block; replace with `var deleteResult = await _credentials.DeleteCredentialAsync(CredentialKeys.MailboxPassword(command.Id), ct);` — no error check needed for success path (idempotent delete always succeeds); optionally log if `!deleteResult.IsSuccess` as a diagnostic warning

### Tests for User Story 3

- [X] T026 [US3] Update `DeleteMailboxCommandHandlerTests` at `tests/Rentier.Application.Tests/DeleteMailboxCommandHandlerTests.cs`: update NSubstitute mock `DeleteCredentialAsync` setup from `void`/`throws` to `Task<Result<VoidResult, Error>>`; add test verifying `DeleteCredentialAsync` called with `CredentialKeys.MailboxPassword(mailboxId)` (correct key format); add test verifying handler succeeds even when `DeleteCredentialAsync` returns `Success` for a missing credential (idempotent); add test verifying no exception is thrown when credential was already absent; use `FakeCredentialStore` for full integration test of add-then-delete lifecycle

**Checkpoint**: Delete lifecycle correct — no orphaned credentials, no antipattern exception swallowing, idempotent contract respected.

---

## Phase 6: User Story 4 — Clear Error Reporting for Credential Operations (Priority: P3)

**Goal**: Each platform provider maps its native error codes to the five defined `Error` codes. `FakeCredentialStore` surfaces `CREDENTIAL_NOT_FOUND` correctly. Error paths are unit-tested with mocked platform behavior.

**Independent Test**: Simulate a failed `SaveCredentialAsync` on each provider and assert the result contains `CREDENTIAL_WRITE_FAILED` with a non-empty message. Simulate a missing key and assert `CREDENTIAL_NOT_FOUND`. Simulate daemon unavailable and assert `PROVIDER_UNAVAILABLE`.

### Tests for User Story 4

- [X] T027 [P] [US4] Add error-path test methods to `WindowsCredentialStoreTests` at `tests/Rentier.Infrastructure.Tests/Security/WindowsCredentialStoreTests.cs`: test that `GetCredentialAsync` with a key that has never been saved returns `CREDENTIAL_NOT_FOUND` (not null, not exception); test that `SaveCredentialAsync` with empty `key` returns `CREDENTIAL_WRITE_FAILED` with message "Key must not be empty"; test that `SaveCredentialAsync` with empty `secret` returns `CREDENTIAL_WRITE_FAILED` with message "Secret must not be empty"
- [X] T028 [P] [US4] Add error-path test methods to `MacOsCredentialStoreTests` at `tests/Rentier.Infrastructure.Tests/Security/MacOsCredentialStoreTests.cs`: test that `GetCredentialAsync` on absent key returns `CREDENTIAL_NOT_FOUND` (mapped from `security` exit code 44); test that `SaveCredentialAsync` returns `CREDENTIAL_WRITE_FAILED` when `security` binary returns non-zero, non-44 exit code (use process mock or skip if on-platform test covers this); verify error `Message` field is non-empty for all failure cases
- [X] T029 [P] [US4] Add error-path test methods to `LinuxCredentialStoreTests` at `tests/Rentier.Infrastructure.Tests/Security/LinuxCredentialStoreTests.cs`: test that empty `SearchItemsAsync` result returns `CREDENTIAL_NOT_FOUND`; test that `DBusException` during `CreateItemAsync` maps to `CREDENTIAL_WRITE_FAILED`; test that `DBusException` during `GetSecretAsync` maps to `PROVIDER_UNAVAILABLE`; test that `DBusException` during `DeleteAsync` maps to `CREDENTIAL_DELETE_FAILED`

**Checkpoint**: All five error codes exercised across all providers. Error messages are non-empty and human-readable. User Story 4 independently verifiable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Clean up the obsolete implementation, validate the full build, and run the quickstart scenarios.

- [X] T030 [P] Delete `src/Rentier.Infrastructure/Security/OsCredentialStore.cs` — obsolete after `WindowsCredentialStore` is created in T009; confirm no remaining references with `grep -r OsCredentialStore src/` before deletion
- [X] T031 [P] Verify clean build on Windows: `dotnet build Rentier.sln` — confirms no `CA1416` suppression warnings remain, no null-reference warnings from removed `Task<string?>` pattern, no unresolved references to `OsCredentialStore`
- [X] T032 Run quickstart.md validation: execute the save/retrieve/delete code snippets from `specs/feature/017-cross-platform-credential-store/quickstart.md` against the running application on the current platform; confirm `FakeCredentialStore` works as a drop-in in a unit test following the quickstart template; confirm provider selection logs the expected provider name at startup

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)         → no dependencies — start immediately
Phase 2 (Foundational)  → depends on Phase 1 (Tests.Common project must exist for T008)
Phase 3 (US1 P1)        → depends on Phase 2 completion (ICredentialStore interface broken until T006 done)
Phase 4 (US2 P2)        → depends on Phase 3 (providers T009/T010/T011 must exist for factory T021)
Phase 5 (US3 P2)        → depends on Phase 2 (CredentialKeys T005 must exist)
Phase 6 (US4 P3)        → depends on Phase 3 tests (test files from T015/T016/T017 must exist)
Phase 7 (Polish)        → depends on all user story phases
```

### User Story Dependencies

```
US1 (P1)  ← foundational: T004 (Error.cs), T005 (CredentialKeys), T006 (ICredentialStore)
US2 (P2)  ← US1 providers (T009, T010, T011) + foundational T007 (ProviderInfo)
US3 (P2)  ← foundational T005 (CredentialKeys) + T006 (ICredentialStore) only — independent of US1 providers
US4 (P3)  ← US1 test files (T015, T016, T017) — adds error-path methods to existing test classes
```

### Within Each User Story

```
US1: T009/T010/T011 (providers, parallel) → T012/T013/T014 (handlers, parallel) → T015-T020 (tests, parallel)
US2: T021 (factory) → T022 (null store, parallel) → T023 (DI update) → T024 (tests)
US3: T025 (handler fix) → T026 (tests)
US4: T027/T028/T029 (error tests, parallel within US4)
```

### Critical Blocking Dependencies

- **T006** (`ICredentialStore` update) is a **hard blocker** — everything else in the feature depends on it. Complete T004 + T005 in parallel, then T006.
- **T008** (`FakeCredentialStore`) must complete before any Application.Tests updates (T018, T019, T020, T026).
- **T009, T010, T011** (providers) must complete before **T021** (factory).

---

## Parallel Opportunities

### Phase 2 (Foundational) — run T004, T005, T007 in parallel, then T006, then T008

```
Parallel: T004 (Error.cs)  |  T005 (CredentialKeys.cs)  |  T007 (ProviderInfo.cs)
          ↓ all complete
Sequential: T006 (ICredentialStore interface — the breaking change)
          ↓
Sequential: T008 (FakeCredentialStore — depends on T006 signature)
```

### Phase 3 (US1) — provider creation and handler fixes are fully parallel

```
Parallel: T009 (WindowsCredentialStore)  |  T010 (MacOsCredentialStore)  |  T011 (LinuxCredentialStore)
Parallel: T012 (AddMailboxHandler)        |  T013 (UpdateMailboxHandler)   |  T014 (ImapSyncService)
          ↓ all complete
Parallel: T015 (WindowsTests)  |  T016 (MacOsTests)  |  T017 (LinuxTests)
Parallel: T018 (AddHandlerTests)  |  T019 (UpdateHandlerTests)  |  T020 (SyncHandlerTests)
```

### Phase 6 (US4) — all error-path test tasks are fully parallel

```
Parallel: T027 (Windows error paths)  |  T028 (macOS error paths)  |  T029 (Linux error paths)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only — Windows + macOS in scope)

1. Complete Phase 1: Setup (T001–T003)
2. Complete Phase 2: Foundational — T004, T005, T007 in parallel → T006 → T008
3. Complete Phase 3: US1 — T009 + T010 in parallel (skip T011 for Linux MVP) → T012/T013/T014 → T018/T019/T020
4. **STOP and VALIDATE on Windows**: Build + run handler tests with `FakeCredentialStore`; verify `CredentialKeys` key format in test assertions
5. Add Linux (T011 + T017 + error path T029) as a separate increment

### Incremental Delivery

1. **Foundation + US1 (Windows only)** → validate key-format bug is fixed, Windows path unbroken
2. **Add macOS (T010 + T016 + T028)** → test on macOS runner in CI
3. **Add Linux (T001 + T011 + T017 + T029)** → test on Linux runner with `gnome-keyring-daemon`
4. **US2 (T021–T024)** → provider selection and DI — completes startup diagnostics
5. **US3 (T025–T026)** → delete lifecycle — credential hygiene
6. **US4 (T027–T029)** → error reporting completeness
7. **Polish (T030–T032)** → cleanup and validation

### Parallel Team Strategy

With two developers:

- **Dev A**: Phase 2 foundational (T004–T008), then US1 providers (T009, T010, T011)
- **Dev B**: US3 handler fix (T025, T026) after T005/T006 complete, then US2 factory (T021–T024)
- Merge point: US1 providers complete → both devs integrate for US4 error tests

---

## Notes

- `[P]` tasks operate on different files and have no shared dependencies within their phase
- `[US#]` label maps each task to a specific user story for traceability and independent testing
- **Key format bug** (analysis.md §2): the bug is fixed by T005 (CredentialKeys) + T012 + T013 + T025 together. Any partial application of these three leaves the mismatch active — complete all three before testing end-to-end.
- **OsCredentialStore.cs** (T030): do not delete until T009 is complete and the build confirms no remaining references
- **Singleton vs Transient**: DI registration in T023 MUST use `AddSingleton`, not `AddTransient` — providers are stateless and thread-safe; the existing Transient lifetime was wasteful
- **CA1416 suppression**: the `#pragma warning disable CA1416` in `InfrastructureServiceExtensions.cs` is removed in T023 — this was the indicator that platform detection was missing
- **Linux CI**: `LinuxCredentialStoreTests` (T017) require `gnome-keyring-daemon` running on the CI Linux runner; add `gnome-keyring` install step to the Linux GitHub Actions job
- Commit after each phase checkpoint to enable clean rollback
- Verify `dotnet build` after T006 to confirm all `ICredentialStore` consumers fail to compile (expected) before fixing them in T012–T014 and T025
