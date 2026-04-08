# Implementation Plan: Cross-Platform Credential Store

**Branch**: `feature/017-cross-platform-credential-store` | **Date**: 2025-07-15 | **Spec**: [spec.md](../../.specify/specs/017-cross-platform-credential-store/spec.md)
**Input**: Feature specification from `.specify/specs/017-cross-platform-credential-store/spec.md`

## Summary

Replace the Windows-only `OsCredentialStore` (P/Invoke to advapi32.dll) with a strategy-based credential store that supports Windows Credential Manager, macOS Keychain, and Linux Secret Service. The `ICredentialStore` interface evolves from exception-throwing to `Result<T, Error>` returns. Provider selection occurs at DI composition time via `RuntimeInformation.IsOSPlatform()` with an availability probe for Linux. No new external NuGet packages are required for Windows or macOS; the Linux provider uses `Ace4896.DBus.Services.Secrets` for native D-Bus Secret Service integration.

## Technical Context

**Language/Version**: C# 12, .NET 8  
**Primary Dependencies**: `Ace4896.DBus.Services.Secrets` (Linux D-Bus Secret Service), existing advapi32.dll P/Invoke (Windows), Security.framework P/Invoke via `security` CLI fallback (macOS)  
**Storage**: OS credential stores only (Windows Credential Manager, macOS Keychain, Linux Secret Service). No SQLite or file storage for secrets.  
**Testing**: xUnit + FluentAssertions + NSubstitute. `FakeCredentialStore` for unit tests, platform-gated integration tests.  
**Target Platform**: Windows 10+, macOS 12+ (Monterey), Linux with Secret Service daemon (GNOME Keyring / KDE Wallet)  
**Project Type**: Desktop application (Avalonia UI)  
**Performance Goals**: All credential operations < 2s. Provider selection < 1s at startup.  
**Constraints**: Offline-only (no network for credentials). Secrets MUST NOT touch SQLite/files/env vars.  
**Scale/Scope**: Single-user desktop app. ~3-5 stored credentials per user (IMAP mailbox passwords).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - `ICredentialStore` remains in `Rentier.Application/Interfaces/`. All platform providers in `Rentier.Infrastructure/Security/`. Provider selection in `InfrastructureServiceExtensions`. No boundary violations.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - N/A — no monetary values in credential storage.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - N/A — no date fields in credential operations.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - This feature's entire purpose is enforcing Constitution §II. Secrets stored exclusively in OS credential managers. D-Bus is local IPC, not a network call.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - No new network access. D-Bus Secret Service is local IPC via Unix domain sockets.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - All `ICredentialStore` methods remain `async Task` / `async Task<Result<T, Error>>`. Provider selection at DI time is synchronous (platform detection only, no I/O).
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - Domain: no changes. Application: existing handler tests already mock `ICredentialStore` — updated to mock new Result-returning signatures. Infrastructure: new unit tests for provider selection, per-platform integration tests behind `[SupportedOSPlatform]` guards.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - Spec: `.specify/specs/017-cross-platform-credential-store/spec.md` (approved).

## Project Structure

### Documentation (this feature)

```text
specs/feature/017-cross-platform-credential-store/
├── plan.md                              # This file
├── research.md                          # Phase 0: Platform API research
├── analysis.md                          # Existing code analysis
├── data-model.md                        # Phase 1: Types and error codes
├── quickstart.md                        # Phase 1: Developer quickstart
└── contracts/
    └── application-contracts.md         # Phase 1: ICredentialStore contract
```

### Source Code (repository root)

```text
src/
├── Rentier.Application/
│   ├── Common/
│   │   ├── Error.cs                     # MODIFY: add credential error factory methods
│   │   └── Result.cs                    # EXISTING: no changes
│   └── Interfaces/
│       └── ICredentialStore.cs          # MODIFY: return Result<T, Error> instead of throw/null
│
├── Rentier.Infrastructure/
│   ├── Security/
│   │   ├── WindowsCredentialStore.cs    # NEW: extracted from OsCredentialStore, Result-returning
│   │   ├── MacOsCredentialStore.cs      # NEW: macOS Keychain via security CLI
│   │   ├── LinuxCredentialStore.cs      # NEW: D-Bus Secret Service via Ace4896 package
│   │   └── CredentialStoreFactory.cs    # NEW: provider selection logic
│   └── InfrastructureServiceExtensions.cs  # MODIFY: platform-aware DI registration
│
└── Rentier.Desktop/
    └── (no changes — consumes ICredentialStore via DI)

tests/
├── Rentier.Application.Tests/
│   └── Handlers/
│       ├── AddMailboxCommandHandlerTests.cs      # MODIFY: updated mock signatures
│       ├── UpdateMailboxCommandHandlerTests.cs    # MODIFY: updated mock signatures
│       └── DeleteMailboxCommandHandlerTests.cs    # MODIFY: updated mock signatures
│
├── Rentier.Infrastructure.Tests/
│   └── Security/
│       ├── CredentialStoreFactoryTests.cs   # NEW: provider selection unit tests
│       ├── WindowsCredentialStoreTests.cs   # NEW: Windows integration tests
│       ├── MacOsCredentialStoreTests.cs     # NEW: macOS integration tests
│       └── LinuxCredentialStoreTests.cs     # NEW: Linux integration tests
│
└── Rentier.Tests.Common/
    └── Fakes/
        └── FakeCredentialStore.cs           # NEW: in-memory fake for all unit tests
```

**Structure Decision**: Follows existing Clean Architecture layout. Platform providers are co-located in `Infrastructure/Security/`. A `CredentialStoreFactory` encapsulates platform detection and provider creation, keeping the DI registration clean. The old `OsCredentialStore.cs` is replaced by `WindowsCredentialStore.cs` (renamed + Result-returning).

## Key Design Decisions

### D1: Provider Strategy via Factory, Not Abstract Base Class

**Decision**: Use a `CredentialStoreFactory` static class that returns the platform-appropriate `ICredentialStore` at DI composition time. No abstract base class or runtime polymorphism.

**Rationale**: Each platform's implementation is fundamentally different (P/Invoke structs vs D-Bus async protocol vs CLI process). A shared base class would be artificial. The factory pattern keeps selection logic in one place and the DI registration clean.

**Alternative rejected**: Abstract `CredentialStoreBase` with template methods — rejected because the implementations share no common logic, only the interface contract.

### D2: macOS Provider Uses `security` CLI (Process-Based)

**Decision**: macOS Keychain access via `Process.Start("security", ...)` spawning the built-in `security` command-line tool.

**Rationale**: P/Invoke to Security.framework on macOS requires Objective-C runtime interop and careful native binary management per architecture (arm64/x86_64). The `security` CLI tool is available on all macOS installations, battle-tested, and produces parseable output. For a desktop app storing 3-5 credentials, the process spawn overhead (< 100ms) is negligible. Credentials are passed via stdin where possible to minimize argument-list exposure.

**Alternative rejected**: P/Invoke to Security.framework — rejected due to high complexity, Objective-C bridge requirements, and fragile ABI across macOS versions.

**Alternative rejected**: KeySharp NuGet — rejected because it wraps libsecret/Keychain via native binaries that must be bundled per platform, adding deployment complexity with no benefit over the CLI approach for this use case.

### D3: Linux Provider Uses `Ace4896.DBus.Services.Secrets`

**Decision**: Linux Secret Service access via the `Ace4896.DBus.Services.Secrets` NuGet package, which provides high-level .NET bindings for the D-Bus Secret Service API.

**Rationale**: This package is purpose-built for the exact API we need, targets .NET 6-9, is AOT-friendly, and avoids the complexity of raw D-Bus protocol implementation or native libsecret P/Invoke. It communicates over Unix domain sockets (local IPC only) and supports both GNOME Keyring and KDE Wallet backends.

**Alternative rejected**: `secret-tool` CLI — rejected because parsing CLI output is fragile and `secret-tool` may not be installed on all Linux distributions, even when Secret Service is available.

**Alternative rejected**: Raw `Tmds.DBus` — rejected because it requires manually implementing the Secret Service D-Bus interface spec, which `Ace4896` already provides.

### D4: Windows Provider Retains P/Invoke (Evolved from OsCredentialStore)

**Decision**: Keep the existing advapi32.dll P/Invoke approach. Rename `OsCredentialStore` → `WindowsCredentialStore`, wrap returns in `Result<T, Error>`, remove exception-throwing.

**Rationale**: The existing P/Invoke code is proven, tested, and has no external dependencies. Windows Credential Manager is available on all supported Windows versions.

### D5: ICredentialStore Returns Result<T, Error> Instead of Exceptions

**Decision**: Change the interface from:
- `Task SaveCredentialAsync(string, string, CancellationToken)` (throws)
- `Task<string?> GetCredentialAsync(string, CancellationToken)` (null = not found)
- `Task DeleteCredentialAsync(string, CancellationToken)` (throws)

To:
- `Task<Result<VoidResult, Error>> SaveCredentialAsync(string, string, CancellationToken)`
- `Task<Result<string, Error>> GetCredentialAsync(string, CancellationToken)`
- `Task<Result<VoidResult, Error>> DeleteCredentialAsync(string, CancellationToken)`

**Rationale**: Aligns with the project's established `Result<T, Error>` pattern used by all command handlers. Eliminates the catch-all `try/catch` antipattern in `DeleteMailboxCommandHandler`. Makes error codes explicit and testable.

### D6: Provider Selection Algorithm

**Decision**: Deterministic selection at DI composition time:
```
1. if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) → WindowsCredentialStore
2. if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) → MacOsCredentialStore
3. if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) → probe D-Bus availability
   a. if Secret Service daemon reachable → LinuxCredentialStore
   b. else → return Failure(PROVIDER_UNAVAILABLE, "Secret Service daemon not running")
4. else → return Failure(UNSUPPORTED_PLATFORM, ...)
```

**Rationale**: Windows and macOS always have their credential stores available (built-in). Linux requires a runtime probe because Secret Service requires a running daemon. The probe happens once at startup, never at operation time.

### D7: FakeCredentialStore for Unit Tests

**Decision**: A simple `Dictionary<string, string>`-backed `ICredentialStore` implementation in `Rentier.Tests.Common/Fakes/`. Always returns `Result.Success`. Platform integration tests use real providers behind `[SupportedOSPlatform]` fact/theory guards.

**Rationale**: Application-layer handler tests must not depend on any platform. The fake provides deterministic behavior. Integration tests run on CI matrix (Windows + macOS + Linux runners).

### D8: Key Format Standardization

**Decision**: Standardize all credential keys to `Rentier/Mailbox/{id}/password` (with `/password` suffix). Fix the inconsistency where `AddMailboxCommandHandler` uses `Rentier/Mailbox/{id}` but `ImapMailboxSyncService` uses `Rentier/Mailbox/{id}/password`.

**Rationale**: The spec mandates key format `Rentier/<entity-type>/<entity-id>/<field>`. The field suffix enables future extension (e.g., OAuth tokens stored alongside passwords).

## Complexity Tracking

> No constitution violations. All checks pass.
