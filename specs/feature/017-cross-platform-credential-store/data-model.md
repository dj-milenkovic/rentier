# Data Model: Cross-Platform Credential Store

**Feature**: 017 — Cross-Platform Credential Store  
**Date**: 2025-07-15

## Overview

This feature introduces no new database entities. Credentials are stored exclusively in OS-managed credential stores (not SQLite). The data model defines **types, value objects, and error codes** used across the credential store abstraction.

## Types

### Error Factory Methods (extension to existing `Error` record)

**Location**: `src/Rentier.Application/Common/Error.cs`

| Factory Method | Error Code | Description |
|---|---|---|
| `Error.CredentialNotFound(string key)` | `CREDENTIAL_NOT_FOUND` | Credential does not exist in the OS store |
| `Error.CredentialWriteFailed(string message)` | `CREDENTIAL_WRITE_FAILED` | Platform-level error during save operation |
| `Error.CredentialDeleteFailed(string message)` | `CREDENTIAL_DELETE_FAILED` | Platform-level error during delete operation |
| `Error.ProviderUnavailable(string reason)` | `PROVIDER_UNAVAILABLE` | Provider cannot be initialized (e.g., Secret Service daemon not running) |
| `Error.UnsupportedPlatform(string os)` | `UNSUPPORTED_PLATFORM` | Current OS has no supported credential store provider |

```csharp
public sealed record Error(string Code, string Message)
{
    // Existing factory methods
    public static Error Domain(string message) => new("DOMAIN_ERROR", message);
    public static Error NotFound(string message) => new("NOT_FOUND", message);
    public static Error Infrastructure(string message) => new("INFRASTRUCTURE_ERROR", message);
    
    // New credential-specific factory methods
    public static Error CredentialNotFound(string key) =>
        new("CREDENTIAL_NOT_FOUND", $"No credential found for key '{key}'.");
    public static Error CredentialWriteFailed(string message) =>
        new("CREDENTIAL_WRITE_FAILED", message);
    public static Error CredentialDeleteFailed(string message) =>
        new("CREDENTIAL_DELETE_FAILED", message);
    public static Error ProviderUnavailable(string reason) =>
        new("PROVIDER_UNAVAILABLE", reason);
    public static Error UnsupportedPlatform(string os) =>
        new("UNSUPPORTED_PLATFORM", $"No credential store provider available for platform: {os}.");
}
```

### ProviderInfo (Value Object)

**Location**: `src/Rentier.Infrastructure/Security/ProviderInfo.cs`

Diagnostic information about the selected credential store provider.

```csharp
namespace Rentier.Infrastructure.Security;

/// <summary>
/// Diagnostic result from credential store provider selection.
/// </summary>
public sealed record ProviderInfo(string ProviderName, string Platform)
{
    public override string ToString() => $"{ProviderName} ({Platform})";
}
```

| Field | Type | Description |
|---|---|---|
| `ProviderName` | `string` | Display name: `"Windows Credential Manager"`, `"macOS Keychain"`, `"Linux Secret Service"` |
| `Platform` | `string` | OS identifier: `"Windows"`, `"macOS"`, `"Linux"` |

### CredentialKeys (Utility — Application Layer)

**Location**: `src/Rentier.Application/Common/CredentialKeys.cs`

Centralized key format builder to prevent key format mismatches.

```csharp
namespace Rentier.Application.Common;

/// <summary>
/// Builds standardized credential store keys.
/// Format: Rentier/{entity-type}/{entity-id}/{field}
/// </summary>
public static class CredentialKeys
{
    public static string MailboxPassword(Guid mailboxId) =>
        $"Rentier/Mailbox/{mailboxId}/password";
}
```

## Credential Storage Model (Per Platform)

Credentials are NOT stored in SQLite. Each platform maps the Rentier credential model to its native storage differently:

### Windows Credential Manager

| Rentier Concept | Windows Field | Value |
|---|---|---|
| Key | `TargetName` | `Rentier/Mailbox/{id}/password` |
| Secret | `CredentialBlob` | UTF-8 encoded password bytes |
| App identity | `UserName` | `Rentier/Mailbox/{id}/password` |
| Type | `Type` | `CRED_TYPE_GENERIC (1)` |
| Persistence | `Persist` | `CRED_PERSIST_LOCAL_MACHINE (2)` |

### macOS Keychain

| Rentier Concept | Keychain Field | Value |
|---|---|---|
| Key | Service (`-s`) | `Rentier/Mailbox/{id}/password` |
| Secret | Password (`-w`) | The password string |
| App identity | Account (`-a`) | `"Rentier"` |
| Keychain | Default | User's login keychain |

### Linux Secret Service

| Rentier Concept | Secret Service Field | Value |
|---|---|---|
| Key | Lookup attribute `key` | `Rentier/Mailbox/{id}/password` |
| Secret | Secret value (bytes) | UTF-8 encoded password bytes |
| App identity | Lookup attribute `application` | `"Rentier"` |
| Label | Item label | `"Rentier Credential"` |
| Content type | MIME type | `text/plain; charset=utf8` |
| Collection | Default collection | `"login"` or default keyring |

## Entity Relationships

```text
Mailbox (Domain Entity)
  │
  │  1:1  (by convention, not FK)
  │
  ▼
Credential (OS Store)
  Key: Rentier/Mailbox/{Mailbox.Id}/password
  Value: IMAP password (string)
```

The relationship between `Mailbox` and its credential is **by convention** (key derived from mailbox ID), not by foreign key. There is no database join — the credential lives in the OS store and is looked up by key when needed.

## Validation Rules

| Rule | Enforcement |
|---|---|
| Key must not be null or empty | Argument validation in `ICredentialStore` implementations |
| Secret must not be null or empty | Argument validation in `ICredentialStore.SaveCredentialAsync` |
| Key format must match `Rentier/{type}/{id}/{field}` | Enforced by `CredentialKeys` utility (compile-time) |
| Secret must not contain null bytes | UTF-8 encoding handles this — null bytes are valid UTF-8 |
| Key length must not exceed platform limits | Windows: 256 chars, macOS: no practical limit, Linux: no practical limit. Current key format is ~60 chars max. |

## State Transitions

Credentials have no formal state machine. They follow a simple CRUD lifecycle:

```text
(not exists) --[SaveCredentialAsync]--> (stored)
(stored)     --[GetCredentialAsync]---> (stored)      [read-only, no state change]
(stored)     --[SaveCredentialAsync]--> (stored)      [upsert/overwrite]
(stored)     --[DeleteCredentialAsync]-> (not exists)
(not exists) --[DeleteCredentialAsync]-> (not exists)  [idempotent]
(not exists) --[GetCredentialAsync]---> CREDENTIAL_NOT_FOUND error
```
