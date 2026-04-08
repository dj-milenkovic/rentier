# Analysis: Existing Credential Store Implementation

**Feature**: 017 — Cross-Platform Credential Store  
**Date**: 2025-07-15

## 1. Current Architecture

### ICredentialStore Interface
**Location**: `src/Rentier.Application/Interfaces/ICredentialStore.cs`

```csharp
public interface ICredentialStore
{
    Task SaveCredentialAsync(string key, string secret, CancellationToken ct = default);
    Task<string?> GetCredentialAsync(string key, CancellationToken ct = default);
    Task DeleteCredentialAsync(string key, CancellationToken ct = default);
}
```

**Issues identified**:
1. **Exception-based error handling**: `SaveCredentialAsync` and `DeleteCredentialAsync` throw `Win32Exception` on failure. This is inconsistent with the project's `Result<T, Error>` pattern used by all command handlers.
2. **Nullable return for "not found"**: `GetCredentialAsync` returns `null` when a credential doesn't exist. This conflates "not found" with potential null-related bugs and provides no error code.
3. **No error codes**: The interface provides no way to distinguish between different failure modes (write failed, delete failed, provider unavailable).

### OsCredentialStore Implementation
**Location**: `src/Rentier.Infrastructure/Security/OsCredentialStore.cs`

**Strengths**:
- Clean P/Invoke implementation with proper memory management (`Marshal.AllocHGlobal` / `Marshal.FreeHGlobal` in `try/finally`)
- Correct `[SupportedOSPlatform("windows")]` annotation
- Idempotent delete (swallows `ERROR_NOT_FOUND` on delete)
- UTF-8 encoding for credential blobs (handles Unicode)
- Uses `Task.Run()` to push blocking P/Invoke calls off the calling thread

**Issues**:
- Class named `OsCredentialStore` but only supports Windows — misleading name
- `[SupportedOSPlatform("windows")]` attribute suppressed with `#pragma` at DI registration site
- Throws `Win32Exception` on unexpected errors — callers must use try/catch

### DI Registration
**Location**: `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` (lines 33-35)

```csharp
#pragma warning disable CA1416
services.AddTransient<ICredentialStore, OsCredentialStore>();
#pragma warning restore CA1416
```

**Issues**:
1. **Platform suppression**: `CA1416` warning suppressed rather than addressed — running on non-Windows platforms would crash at runtime
2. **No platform detection**: No `RuntimeInformation.IsOSPlatform()` check before registration
3. **Transient lifetime**: Creates a new `OsCredentialStore` per injection, which is wasteful since the implementation is stateless. Should be `Singleton`.

## 2. Usage Analysis

### Consumers of ICredentialStore

| Consumer | File | Operation | Key Format |
|---|---|---|---|
| `AddMailboxCommandHandler` | `src/Rentier.Application/Handlers/AddMailboxCommandHandler.cs` | `SaveCredentialAsync` | `Rentier/Mailbox/{id}` ⚠️ |
| `UpdateMailboxCommandHandler` | `src/Rentier.Application/Handlers/UpdateMailboxCommandHandler.cs` | `SaveCredentialAsync` | `Rentier/Mailbox/{id}` ⚠️ |
| `DeleteMailboxCommandHandler` | `src/Rentier.Application/Handlers/DeleteMailboxCommandHandler.cs` | `DeleteCredentialAsync` | `Rentier/Mailbox/{id}` ⚠️ |
| `ImapMailboxSyncService` | `src/Rentier.Infrastructure/Sync/ImapMailboxSyncService.cs` | `GetCredentialAsync` | `Rentier/Mailbox/{id}/password` ✅ |

### Critical Bug: Key Format Mismatch
The save operations use `Rentier/Mailbox/{id}` but the read operation uses `Rentier/Mailbox/{id}/password`. This means:
- Credentials saved by `AddMailboxCommandHandler` **cannot be retrieved** by `ImapMailboxSyncService`
- The `/password` suffix follows the spec-mandated format `Rentier/<entity-type>/<entity-id>/<field>`
- Fix: standardize all keys to include the `/password` field suffix

### Error Handling Pattern in Consumers
```csharp
// DeleteMailboxCommandHandler — catch-all antipattern
try
{
    await _credentials.DeleteCredentialAsync($"Rentier/Mailbox/{command.Id}", ct);
}
catch
{
    // credential may not exist — swallow all exceptions
}
```

This catch-all hides genuine errors (e.g., permission denied, store locked). With `Result<T, Error>` returns, this becomes explicit:
```csharp
var result = await _credentials.DeleteCredentialAsync(key, ct);
// Failure with CREDENTIAL_NOT_FOUND is expected — ignore
// Failure with other codes → log as warning
```

## 3. Error Handling Infrastructure

### Error Record
**Location**: `src/Rentier.Application/Common/Error.cs`

```csharp
public sealed record Error(string Code, string Message)
{
    public static Error Domain(string message) => new("DOMAIN_ERROR", message);
    public static Error NotFound(string message) => new("NOT_FOUND", message);
    public static Error Infrastructure(string message) => new("INFRASTRUCTURE_ERROR", message);
}
```

**Extension point**: Add credential-specific factory methods here:
- `Error.CredentialNotFound(string key)`
- `Error.CredentialWriteFailed(string message)`
- `Error.CredentialDeleteFailed(string message)`
- `Error.ProviderUnavailable(string reason)`
- `Error.UnsupportedPlatform(string os)`

### Result<T, Error>
**Location**: `src/Rentier.Application/Common/Result.cs`

Already supports the pattern needed. No changes required.

## 4. Test Infrastructure

### Existing Test Projects
- `tests/Rentier.Application.Tests/` — handler tests with NSubstitute mocks
- `tests/Rentier.Infrastructure.Tests/` — integration tests
- `tests/Rentier.Domain.Tests/` — domain logic tests

### Current Test Coverage for Credential Operations
Existing handler tests mock `ICredentialStore` with NSubstitute. These tests will need signature updates when the interface returns `Result<T, Error>` instead of throwing/returning null.

### Testing Strategy for Cross-Platform
1. **Unit tests**: Use `FakeCredentialStore` (in-memory dictionary) — runs on all platforms
2. **Integration tests**: Platform-gated with `[SupportedOSPlatform]` attributes or runtime skips
3. **CI matrix**: GitHub Actions already runs Windows + macOS. Add Linux runner for Secret Service tests (may need `gnome-keyring-daemon` setup in CI).

## 5. NuGet Package Impact

### Current Infrastructure Dependencies
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.*" />
<PackageReference Include="MailKit" Version="4.*" />
<PackageReference Include="AngleSharp" Version="1.*" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.*" />
<PackageReference Include="CsvHelper" Version="33.*" />
```

### New Package Required
```xml
<PackageReference Include="Ace4896.DBus.Services.Secrets" Version="*" />
```

**Impact**: This package transitively depends on `Tmds.DBus.Protocol`. Both are pure managed .NET packages with no native binaries. They are only active on Linux at runtime; on Windows/macOS, the package is inert.

### Conditional Package Reference (Optional Optimization)
```xml
<PackageReference Include="Ace4896.DBus.Services.Secrets" Version="*"
                  Condition="$([MSBuild]::IsOSPlatform('Linux'))" />
```
This prevents the package from being included in Windows/macOS builds. However, this may complicate cross-compilation. Recommend including unconditionally for simplicity and only instantiating on Linux.

## 6. Migration Path

### Phase 1: Interface Evolution
1. Rename `OsCredentialStore` → `WindowsCredentialStore`
2. Update `ICredentialStore` method signatures to return `Result<T, Error>`
3. Update `WindowsCredentialStore` to wrap P/Invoke results in `Result`

### Phase 2: New Providers
4. Implement `MacOsCredentialStore` (security CLI)
5. Implement `LinuxCredentialStore` (D-Bus Secret Service)
6. Implement `CredentialStoreFactory` (platform detection + availability probe)

### Phase 3: Integration
7. Update `InfrastructureServiceExtensions` with platform-aware registration
8. Update all command handlers to use `Result`-based API
9. Fix key format inconsistency across all consumers
10. Add `FakeCredentialStore` for test infrastructure

### Phase 4: Tests
11. Unit tests for `CredentialStoreFactory` selection logic
12. Update existing handler tests for new signatures
13. Platform-gated integration tests for each provider

### Backward Compatibility
- Windows users: no behavior change. Same Windows Credential Manager backend, same key format (after fix).
- Existing stored credentials: keys stored as `Rentier/Mailbox/{id}` (without `/password`) will need migration or the handlers need to check both formats during a transition period.
