# Research: Cross-Platform Credential Store

**Feature**: 017 — Cross-Platform Credential Store  
**Date**: 2025-07-15  
**Status**: Complete

## R1: Windows Credential Manager API

### Decision
Retain existing P/Invoke to `advapi32.dll` (`CredWriteW`, `CredReadW`, `CredDeleteW`, `CredFreeW`). Evolve `OsCredentialStore` → `WindowsCredentialStore` with `Result<T, Error>` returns.

### Rationale
The current implementation in `OsCredentialStore.cs` is proven and production-tested. It uses generic credential type (`CRED_TYPE_GENERIC = 1`) with local machine persistence (`CRED_PERSIST_LOCAL_MACHINE = 2`). UTF-8 encoding for credential blobs handles Unicode correctly. No external packages needed.

### Alternatives Considered
| Alternative | Why Rejected |
|---|---|
| `Meziantou.Framework.Win32.CredentialManager` NuGet | Adds a dependency for functionality we already have working. Would still be Windows-only. |
| Windows DPAPI (`ProtectedData`) | Lower-level encryption API — requires managing storage ourselves. Windows Credential Manager is the intended high-level API for application secrets. |
| `System.Security.Cryptography.ProtectedData` | Encrypts data but doesn't manage it. We'd need to write encrypted blobs to disk, violating the "no plaintext files" constitution rule. |

### Key Technical Details
- P/Invoke signatures: `CredWriteW`, `CredReadW`, `CredDeleteW`, `CredFreeW` from `advapi32.dll`
- Credential type: `CRED_TYPE_GENERIC (1)` — application-defined credential, not domain or certificate
- Persistence: `CRED_PERSIST_LOCAL_MACHINE (2)` — credential survives logoff, stored in Windows Vault
- Error code: `ERROR_NOT_FOUND (1168)` — handled gracefully for Get (not found) and Delete (idempotent)
- Max target name length: 256 characters (sufficient for `Rentier/Mailbox/{guid}/password` format)

---

## R2: macOS Keychain API

### Decision
Use the macOS `security` CLI tool via `Process.Start()` for Keychain operations.

### Rationale
Direct P/Invoke to Security.framework (`SecItemAdd`, `SecItemCopyMatching`, `SecItemDelete`) requires Objective-C runtime interop (`objc_msgSend`) and careful marshaling of Core Foundation types (`CFDictionary`, `CFString`, `CFData`). This is fragile across macOS versions and architectures (arm64 vs x86_64). The `security` CLI is a stable, Apple-maintained interface to the same Keychain backend.

### Alternatives Considered
| Alternative | Why Rejected |
|---|---|
| P/Invoke to Security.framework | Requires Objective-C bridge, CFType marshaling, architecture-specific native interop. High implementation and maintenance cost for 3-5 stored credentials. |
| KeySharp NuGet (`KeySharp 1.0.5`) | Wraps native libraries but requires bundling platform-specific `.dylib` files. Last updated 2021. Unmaintained. |
| `keyring-dotnet` | Community fork of KeySharp. Same native binary bundling issues. Low download count. |
| .NET MAUI SecureStorage | Requires MAUI framework — inappropriate for an Avalonia UI application. |

### Key Technical Details

**CLI Commands**:
```bash
# Save/Update a generic password
security add-generic-password -a "Rentier" -s "Rentier/Mailbox/{id}/password" \
  -w "{secret}" -U

# Retrieve a generic password
security find-generic-password -a "Rentier" -s "Rentier/Mailbox/{id}/password" -w

# Delete a generic password
security delete-generic-password -a "Rentier" -s "Rentier/Mailbox/{id}/password"
```

**Mapping**:
| Keychain Field | Rentier Usage |
|---|---|
| `-a` (account) | `"Rentier"` (application identifier) |
| `-s` (service) | Credential key: `Rentier/Mailbox/{id}/password` |
| `-w` (password) | The secret value |
| `-U` (update) | Upsert: update if exists, add if not |

**Error Handling**:
- Exit code `0`: success
- Exit code `44` (`errSecItemNotFound`): credential not found → `CREDENTIAL_NOT_FOUND`
- Exit code `36` (`errSecDuplicateItem`): handled by `-U` flag (upsert)
- Other non-zero: platform error → `CREDENTIAL_WRITE_FAILED` / `CREDENTIAL_DELETE_FAILED`

**Security Considerations**:
- Password passed via `-w` argument (visible in process list briefly). For a local desktop app with single-user access, this is acceptable.
- Alternative: pipe password via stdin with `-w` omitted and redirect — adds complexity with minimal security benefit in single-user context.
- Keychain access may trigger system dialog for first-time access — `security` tool handles this natively.

---

## R3: Linux Secret Service (D-Bus) API

### Decision
Use the `Ace4896.DBus.Services.Secrets` NuGet package for D-Bus Secret Service integration.

### Rationale
This package provides high-level .NET 8 bindings for the freedesktop.org Secret Service D-Bus protocol (spec version 0.2). It supports GNOME Keyring and KDE Wallet backends, handles D-H encryption for secret transfer over D-Bus, and is AOT/trimmer-friendly. It communicates via Unix domain sockets (local IPC only — no network access).

### Alternatives Considered
| Alternative | Why Rejected |
|---|---|
| `secret-tool` CLI | Not installed by default on all distributions. Parsing CLI output is fragile. Secret Service daemon being available ≠ `secret-tool` binary being installed. |
| Raw `Tmds.DBus` | Low-level D-Bus client. Requires manually implementing the full Secret Service interface spec (methods, properties, signals). Significant boilerplate. |
| `DBusSharp` / `ndesk-dbus` | Unmaintained. Last significant update years ago. Not compatible with modern .NET. |
| `libsecret` P/Invoke | Requires native `libsecret-1-0.so` library and GLib type marshaling. Adds a native dependency that may not be present on all Linux distributions. |
| File-based encryption (DPAPI-like) | Violates Constitution §II — secrets must live in OS credential stores, not encrypted files. |

### Key Technical Details

**NuGet Package**: `Ace4896.DBus.Services.Secrets` (latest stable)  
**Dependency chain**: `Ace4896.DBus.Services.Secrets` → `Tmds.DBus.Protocol` (transitive)

**API Usage Pattern**:
```csharp
// Connect to Secret Service daemon
SecretService service = await SecretService.ConnectAsync(EncryptionType.Dh);

// Get default collection (usually "login" or "Default keyring")
Collection? collection = await service.GetDefaultCollectionAsync();

// Store a secret
var attributes = new Dictionary<string, string>
{
    { "application", "Rentier" },
    { "key", "Rentier/Mailbox/{id}/password" }
};
byte[] secretBytes = Encoding.UTF8.GetBytes(password);
await collection.CreateItemAsync("Rentier Credential", attributes, secretBytes,
    "text/plain; charset=utf8", replaceExisting: true);

// Retrieve a secret
Item[] items = await collection.SearchItemsAsync(attributes);
byte[] secret = await items[0].GetSecretAsync();
string password = Encoding.UTF8.GetString(secret);

// Delete a secret
await items[0].DeleteAsync();
```

**Availability Probe** (for provider selection):
```csharp
try
{
    var service = await SecretService.ConnectAsync(EncryptionType.Dh);
    // Daemon is reachable
    service.Dispose();
    return true;
}
catch (DBusException)
{
    // Daemon is not running or D-Bus session bus unavailable
    return false;
}
```

**Prerequisites on Linux**:
- A running Secret Service daemon: `gnome-keyring-daemon` (GNOME), `kwalletd5`/`kwalletd6` (KDE)
- D-Bus session bus available (standard on any graphical Linux session)
- Headless/server Linux without desktop environment → `PROVIDER_UNAVAILABLE`

---

## R4: Provider Selection Strategy

### Decision
Static factory method `CredentialStoreFactory.Create()` called once during DI composition. Returns `Result<ICredentialStore, Error>`.

### Rationale
Provider selection is a startup concern. Once selected, the provider never changes during the application lifetime. Making it a factory rather than a runtime service keeps the DI container simple — one `ICredentialStore` registration, resolved everywhere.

### Algorithm
```
CredentialStoreFactory.CreateAsync():
  1. Windows → Success(new WindowsCredentialStore())
  2. macOS   → Success(new MacOsCredentialStore())
  3. Linux   → probe D-Bus Secret Service availability
     a. reachable  → Success(new LinuxCredentialStore(service))
     b. unreachable → Failure(PROVIDER_UNAVAILABLE, "Secret Service daemon not running...")
  4. Other   → Failure(UNSUPPORTED_PLATFORM, "Current OS is not supported...")
```

### DI Registration Pattern
```csharp
// In InfrastructureServiceExtensions.AddInfrastructureServices()
var providerResult = await CredentialStoreFactory.CreateAsync();
if (providerResult.IsSuccess)
{
    services.AddSingleton<ICredentialStore>(providerResult.Value);
}
else
{
    // Log the error, register a NullCredentialStore that always returns errors
    services.AddSingleton<ICredentialStore>(
        new NullCredentialStore(providerResult.Error));
}
```

**Note**: Provider registered as `Singleton` (not Transient) because all providers are stateless and thread-safe. Avoids re-creating P/Invoke handles or D-Bus connections per request.

---

## R5: Error Code Design

### Decision
Extend the existing `Error` record with credential-specific factory methods. No new enum type — follows the established `Error(string Code, string Message)` pattern.

### Rationale
The codebase consistently uses string-based error codes in the `Error` record (e.g., `"DOMAIN_ERROR"`, `"NOT_FOUND"`, `"INFRASTRUCTURE_ERROR"`). Adding a separate `CredentialStoreError` enum would break this consistency. Factory methods provide discoverability.

### Error Codes
| Code | When Raised | User Impact |
|---|---|---|
| `CREDENTIAL_NOT_FOUND` | Get returns no matching entry | Prompt user to re-enter password |
| `CREDENTIAL_WRITE_FAILED` | Save fails (platform error) | Show error message, suggest retry |
| `CREDENTIAL_DELETE_FAILED` | Delete fails (platform error) | Show warning, non-blocking |
| `PROVIDER_UNAVAILABLE` | Linux: daemon not running; macOS: keychain locked | Startup diagnostic, suggest fix |
| `UNSUPPORTED_PLATFORM` | OS not recognized | Fatal startup error |

### Alternatives Considered
| Alternative | Why Rejected |
|---|---|
| `CredentialStoreError` enum | Breaks consistency with existing `Error` record pattern. Would require mapping between enum and `Error`. |
| Exception hierarchy (`CredentialNotFoundException`, etc.) | Violates the project's `Result<T, Error>` pattern. Exceptions are reserved for programmer errors, not expected failures. |

---

## R6: Key Format Inconsistency Fix

### Decision
Standardize all credential keys to `Rentier/Mailbox/{id}/password`.

### Findings
Current code has an inconsistency:
- `AddMailboxCommandHandler.cs` saves as `Rentier/Mailbox/{mailbox.Id}` (missing field suffix)
- `UpdateMailboxCommandHandler.cs` saves as `Rentier/Mailbox/{mailbox.Id}` (missing field suffix)
- `ImapMailboxSyncService.cs` reads as `Rentier/Mailbox/{mailbox.Id}/password` (has field suffix)
- `DeleteMailboxCommandHandler.cs` deletes as `Rentier/Mailbox/{command.Id}` (missing field suffix)

This means **the sync service cannot retrieve credentials saved by the command handlers** because the keys don't match. This is a pre-existing bug.

### Fix
Update all usages to `Rentier/Mailbox/{id}/password`. Define a helper constant or method:
```csharp
// In Application layer — key builder utility
public static class CredentialKeys
{
    public static string MailboxPassword(Guid mailboxId) =>
        $"Rentier/Mailbox/{mailboxId}/password";
}
```
