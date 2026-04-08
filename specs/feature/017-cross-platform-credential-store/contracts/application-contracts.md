# Application Contracts: Cross-Platform Credential Store

**Feature**: 017 — Cross-Platform Credential Store  
**Date**: 2025-07-15

## ICredentialStore Interface Contract

**Location**: `src/Rentier.Application/Interfaces/ICredentialStore.cs`  
**Layer**: Application (abstraction — no platform knowledge)

### Current Interface (BEFORE)

```csharp
public interface ICredentialStore
{
    Task SaveCredentialAsync(string key, string secret, CancellationToken ct = default);
    Task<string?> GetCredentialAsync(string key, CancellationToken ct = default);
    Task DeleteCredentialAsync(string key, CancellationToken ct = default);
}
```

### New Interface (AFTER)

```csharp
using Rentier.Application.Common;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Abstracts OS-managed secure credential storage.
/// IMAP passwords MUST be stored exclusively via this interface.
/// Key format: Rentier/{entity-type}/{entity-id}/{field}
/// </summary>
public interface ICredentialStore
{
    /// <summary>
    /// Stores or updates a credential in the platform credential store.
    /// </summary>
    /// <param name="key">Credential key in format Rentier/{type}/{id}/{field}.</param>
    /// <param name="secret">The secret value to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success(VoidResult) on success.
    /// Failure with CREDENTIAL_WRITE_FAILED on platform error.
    /// Failure with PROVIDER_UNAVAILABLE if the credential store is inaccessible.
    /// </returns>
    Task<Result<VoidResult, Error>> SaveCredentialAsync(
        string key, string secret, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a credential from the platform credential store.
    /// </summary>
    /// <param name="key">Credential key in format Rentier/{type}/{id}/{field}.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success(secret) when credential exists.
    /// Failure with CREDENTIAL_NOT_FOUND when no credential matches the key.
    /// Failure with PROVIDER_UNAVAILABLE if the credential store is inaccessible.
    /// </returns>
    Task<Result<string, Error>> GetCredentialAsync(
        string key, CancellationToken ct = default);

    /// <summary>
    /// Deletes a credential from the platform credential store. Idempotent —
    /// returns Success even if the credential does not exist.
    /// </summary>
    /// <param name="key">Credential key in format Rentier/{type}/{id}/{field}.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success(VoidResult) on success or when credential was already absent.
    /// Failure with CREDENTIAL_DELETE_FAILED on platform error.
    /// Failure with PROVIDER_UNAVAILABLE if the credential store is inaccessible.
    /// </returns>
    Task<Result<VoidResult, Error>> DeleteCredentialAsync(
        string key, CancellationToken ct = default);
}
```

### Contract Rules

| Rule | Description |
|---|---|
| **Idempotent Delete** | `DeleteCredentialAsync` MUST return `Success` when the credential does not exist. It MUST NOT return `CREDENTIAL_NOT_FOUND` for delete operations. |
| **Not-Found on Get** | `GetCredentialAsync` MUST return `Failure(CREDENTIAL_NOT_FOUND)` when no credential matches the key. It MUST NOT return `null` or throw an exception. |
| **Upsert on Save** | `SaveCredentialAsync` MUST overwrite an existing credential with the same key. It MUST NOT return a "duplicate" error. |
| **Key Validation** | Implementations MUST validate that `key` is not null or whitespace. Return `CREDENTIAL_WRITE_FAILED` with message "Key must not be empty." |
| **Secret Validation** | `SaveCredentialAsync` MUST validate that `secret` is not null or empty. Return `CREDENTIAL_WRITE_FAILED` with message "Secret must not be empty." |
| **Async** | All operations MUST be async. Blocking I/O (e.g., P/Invoke) MUST be wrapped in `Task.Run()`. |
| **Thread Safety** | Implementations MUST be safe for concurrent use (platform credential stores handle their own locking). |
| **No Exceptions** | Implementations MUST NOT throw exceptions for expected failures. All failures MUST be expressed via `Result.Failure(Error)`. Exceptions are reserved for programmer errors (e.g., `ArgumentNullException` for null key). |

---

## Error Codes

| Error Code | Applicable Operations | Description |
|---|---|---|
| `CREDENTIAL_NOT_FOUND` | Get | No credential exists for the given key |
| `CREDENTIAL_WRITE_FAILED` | Save | Platform credential store rejected the write |
| `CREDENTIAL_DELETE_FAILED` | Delete | Platform credential store rejected the delete |
| `PROVIDER_UNAVAILABLE` | Save, Get, Delete | Credential store is inaccessible (locked keychain, daemon not running) |
| `UNSUPPORTED_PLATFORM` | Provider selection | No provider available for the current OS |

### Error Code → Platform Mapping

| Platform | Error Code | Native Error |
|---|---|---|
| Windows | `CREDENTIAL_NOT_FOUND` | `Win32 ERROR_NOT_FOUND (1168)` |
| Windows | `CREDENTIAL_WRITE_FAILED` | Any `Win32Exception` from `CredWriteW` |
| Windows | `CREDENTIAL_DELETE_FAILED` | Any `Win32Exception` from `CredDeleteW` (except 1168) |
| macOS | `CREDENTIAL_NOT_FOUND` | `security` exit code `44` (`errSecItemNotFound`) |
| macOS | `CREDENTIAL_WRITE_FAILED` | `security` non-zero exit (except 44) |
| macOS | `CREDENTIAL_DELETE_FAILED` | `security` non-zero exit (except 44) |
| macOS | `PROVIDER_UNAVAILABLE` | `security` binary not found or keychain locked |
| Linux | `CREDENTIAL_NOT_FOUND` | Empty search results from D-Bus Secret Service |
| Linux | `CREDENTIAL_WRITE_FAILED` | `DBusException` during `CreateItemAsync` |
| Linux | `CREDENTIAL_DELETE_FAILED` | `DBusException` during `DeleteAsync` |
| Linux | `PROVIDER_UNAVAILABLE` | `DBusException` during `ConnectAsync` (daemon unreachable) |

---

## CredentialStoreFactory Contract

**Location**: `src/Rentier.Infrastructure/Security/CredentialStoreFactory.cs`  
**Layer**: Infrastructure (knows about platform specifics)

```csharp
namespace Rentier.Infrastructure.Security;

/// <summary>
/// Creates the platform-appropriate ICredentialStore implementation.
/// Called once during DI composition at application startup.
/// </summary>
public static class CredentialStoreFactory
{
    /// <summary>
    /// Detects the current platform and creates the appropriate credential store provider.
    /// On Linux, probes the D-Bus Secret Service daemon availability.
    /// </summary>
    /// <returns>
    /// Success with (ICredentialStore, ProviderInfo) on supported platforms.
    /// Failure with UNSUPPORTED_PLATFORM if the OS is not recognized.
    /// Failure with PROVIDER_UNAVAILABLE if the platform provider cannot initialize
    /// (e.g., Linux without Secret Service daemon).
    /// </returns>
    public static async Task<Result<(ICredentialStore Store, ProviderInfo Info), Error>> CreateAsync();
}
```

### Selection Rules

| Platform | Provider | Availability Check |
|---|---|---|
| Windows | `WindowsCredentialStore` | None required — always available |
| macOS | `MacOsCredentialStore` | None required — `security` CLI always available |
| Linux | `LinuxCredentialStore` | D-Bus Secret Service probe: `SecretService.ConnectAsync()` |
| Other | N/A | Returns `UNSUPPORTED_PLATFORM` |

---

## CredentialKeys Utility Contract

**Location**: `src/Rentier.Application/Common/CredentialKeys.cs`  
**Layer**: Application

```csharp
namespace Rentier.Application.Common;

/// <summary>
/// Builds standardized credential store keys.
/// All credential operations MUST use keys from this class to ensure consistency.
/// Format: Rentier/{entity-type}/{entity-id}/{field}
/// </summary>
public static class CredentialKeys
{
    /// <summary>
    /// Returns the credential key for a mailbox IMAP password.
    /// </summary>
    public static string MailboxPassword(Guid mailboxId) =>
        $"Rentier/Mailbox/{mailboxId}/password";
}
```

### Key Format Specification

```text
Rentier/{entity-type}/{entity-id}/{field}
  │         │              │          │
  │         │              │          └── Field name: "password", "token", etc.
  │         │              └── Entity ID: typically a Guid
  │         └── Entity type: "Mailbox", future: "Account", etc.
  └── Application prefix: always "Rentier"
```

**Max key length**: 60 characters (well within all platform limits)

---

## FakeCredentialStore Contract (Test Infrastructure)

**Location**: `tests/Rentier.Tests.Common/Fakes/FakeCredentialStore.cs`  
**Layer**: Test

```csharp
namespace Rentier.Tests.Common.Fakes;

/// <summary>
/// In-memory ICredentialStore implementation for unit testing.
/// Always returns Success. Does not interact with any OS credential store.
/// </summary>
public sealed class FakeCredentialStore : ICredentialStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public Task<Result<VoidResult, Error>> SaveCredentialAsync(
        string key, string secret, CancellationToken ct = default)
    {
        _store[key] = secret;
        return Task.FromResult(Result<VoidResult, Error>.Success(VoidResult.Value));
    }

    public Task<Result<string, Error>> GetCredentialAsync(
        string key, CancellationToken ct = default)
    {
        return _store.TryGetValue(key, out var secret)
            ? Task.FromResult(Result<string, Error>.Success(secret))
            : Task.FromResult(Result<string, Error>.Failure(
                Error.CredentialNotFound(key)));
    }

    public Task<Result<VoidResult, Error>> DeleteCredentialAsync(
        string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.FromResult(Result<VoidResult, Error>.Success(VoidResult.Value));
    }

    /// <summary>Test helper: returns all stored keys.</summary>
    public IReadOnlyCollection<string> StoredKeys => _store.Keys;

    /// <summary>Test helper: clears all stored credentials.</summary>
    public void Clear() => _store.Clear();
}
```

### Test Contract Rules
| Rule | Description |
|---|---|
| **Deterministic** | Always returns `Success` for save/delete. Returns `CREDENTIAL_NOT_FOUND` only when key is absent. |
| **Thread-safe** | Uses `ConcurrentDictionary` for safe parallel test execution. |
| **Inspectable** | `StoredKeys` property allows test assertions on stored state. |
| **Resettable** | `Clear()` method for test isolation. |

---

## Consumer Migration Guide

### AddMailboxCommandHandler (BEFORE → AFTER)

```csharp
// BEFORE
if (!string.IsNullOrEmpty(command.Password))
    await _credentials.SaveCredentialAsync(
        $"Rentier/Mailbox/{mailbox.Id}", command.Password, ct);

// AFTER
if (!string.IsNullOrEmpty(command.Password))
{
    var credResult = await _credentials.SaveCredentialAsync(
        CredentialKeys.MailboxPassword(mailbox.Id), command.Password, ct);
    if (!credResult.IsSuccess)
        return Result<Guid, Error>.Failure(credResult.Error);
}
```

### DeleteMailboxCommandHandler (BEFORE → AFTER)

```csharp
// BEFORE
try
{
    await _credentials.DeleteCredentialAsync(
        $"Rentier/Mailbox/{command.Id}", ct);
}
catch
{
    // credential may not exist — swallow all exceptions
}

// AFTER
await _credentials.DeleteCredentialAsync(
    CredentialKeys.MailboxPassword(command.Id), ct);
// Result is always Success (idempotent delete) — no error handling needed
// Only log if non-success for unexpected platform errors
```

### ImapMailboxSyncService (BEFORE → AFTER)

```csharp
// BEFORE
var password = await _credentialStore.GetCredentialAsync(
    $"Rentier/Mailbox/{mailbox.Id}/password", ct);

// AFTER
var credResult = await _credentialStore.GetCredentialAsync(
    CredentialKeys.MailboxPassword(mailbox.Id), ct);
if (!credResult.IsSuccess)
    return Result<SyncResult, Error>.Failure(credResult.Error);
var password = credResult.Value;
```
