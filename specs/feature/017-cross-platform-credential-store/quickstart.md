# Quickstart: Cross-Platform Credential Store

**Feature**: 017 — Cross-Platform Credential Store  
**Date**: 2025-07-15

## What This Feature Does

Replaces the Windows-only credential store with a strategy-based system that supports Windows, macOS, and Linux. The correct provider is selected automatically at startup based on the operating system.

## Key Files to Know

| File | Purpose |
|---|---|
| `src/Rentier.Application/Interfaces/ICredentialStore.cs` | Interface contract — returns `Result<T, Error>` |
| `src/Rentier.Application/Common/Error.cs` | Error codes (new credential-specific factories) |
| `src/Rentier.Application/Common/CredentialKeys.cs` | Key format builder — prevents key mismatches |
| `src/Rentier.Infrastructure/Security/WindowsCredentialStore.cs` | Windows provider (P/Invoke to advapi32.dll) |
| `src/Rentier.Infrastructure/Security/MacOsCredentialStore.cs` | macOS provider (`security` CLI) |
| `src/Rentier.Infrastructure/Security/LinuxCredentialStore.cs` | Linux provider (D-Bus Secret Service) |
| `src/Rentier.Infrastructure/Security/CredentialStoreFactory.cs` | Platform detection + provider creation |
| `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` | DI registration (platform-aware) |
| `tests/Rentier.Tests.Common/Fakes/FakeCredentialStore.cs` | In-memory fake for unit tests |

## How to Use ICredentialStore

### Save a Credential

```csharp
var key = CredentialKeys.MailboxPassword(mailboxId);
var result = await _credentialStore.SaveCredentialAsync(key, password, ct);
if (!result.IsSuccess)
    return Result<Guid, Error>.Failure(result.Error);
```

### Retrieve a Credential

```csharp
var key = CredentialKeys.MailboxPassword(mailboxId);
var result = await _credentialStore.GetCredentialAsync(key, ct);
if (!result.IsSuccess)
    return Result<SyncResult, Error>.Failure(result.Error);
var password = result.Value;
```

### Delete a Credential

```csharp
var key = CredentialKeys.MailboxPassword(mailboxId);
// Idempotent — always succeeds even if credential doesn't exist
await _credentialStore.DeleteCredentialAsync(key, ct);
```

## How Provider Selection Works

```text
App starts
  → InfrastructureServiceExtensions.AddInfrastructureServices()
    → CredentialStoreFactory.CreateAsync()
      → RuntimeInformation.IsOSPlatform(Windows)? → WindowsCredentialStore
      → RuntimeInformation.IsOSPlatform(OSX)?     → MacOsCredentialStore
      → RuntimeInformation.IsOSPlatform(Linux)?   → probe D-Bus daemon
        → reachable?  → LinuxCredentialStore
        → unreachable → PROVIDER_UNAVAILABLE error
      → else → UNSUPPORTED_PLATFORM error
    → Register ICredentialStore as Singleton
```

## Error Codes Reference

| Code | Meaning | Typical Action |
|---|---|---|
| `CREDENTIAL_NOT_FOUND` | No credential for key | Prompt user to re-enter password |
| `CREDENTIAL_WRITE_FAILED` | OS rejected save | Show error, suggest retry |
| `CREDENTIAL_DELETE_FAILED` | OS rejected delete | Log warning, non-blocking |
| `PROVIDER_UNAVAILABLE` | Store inaccessible | Show startup diagnostic |
| `UNSUPPORTED_PLATFORM` | Unknown OS | Fatal startup error |

## How to Write Tests

### Unit Tests (any platform)

```csharp
// Use FakeCredentialStore — no OS dependency
var fakeStore = new FakeCredentialStore();
var handler = new AddMailboxCommandHandler(mockRepo, fakeStore);

var result = await handler.HandleAsync(command, CancellationToken.None);

Assert.True(result.IsSuccess);
Assert.Contains("Rentier/Mailbox/", fakeStore.StoredKeys.Single());
```

### Integration Tests (platform-specific)

```csharp
[Fact]
[SupportedOSPlatform("windows")]
public async Task WindowsCredentialStore_SaveAndRetrieve_RoundTrips()
{
    var store = new WindowsCredentialStore();
    var key = $"Rentier/Test/{Guid.NewGuid()}/password";

    var saveResult = await store.SaveCredentialAsync(key, "test-secret");
    Assert.True(saveResult.IsSuccess);

    var getResult = await store.GetCredentialAsync(key);
    Assert.True(getResult.IsSuccess);
    Assert.Equal("test-secret", getResult.Value);

    // Cleanup
    await store.DeleteCredentialAsync(key);
}
```

## NuGet Packages

| Package | Purpose | Platform |
|---|---|---|
| `Ace4896.DBus.Services.Secrets` | D-Bus Secret Service bindings | Linux only (inert on other platforms) |

Add to `Rentier.Infrastructure.csproj`:
```xml
<PackageReference Include="Ace4896.DBus.Services.Secrets" Version="*" />
```

## Common Pitfalls

1. **Don't hardcode credential keys** — always use `CredentialKeys.MailboxPassword(id)`.
2. **Don't catch exceptions from ICredentialStore** — it returns `Result<T, Error>`, never throws for expected failures.
3. **Don't register ICredentialStore as Transient** — use `Singleton` (providers are stateless/thread-safe).
4. **Don't skip the factory** — always go through `CredentialStoreFactory.CreateAsync()` for provider selection.
5. **Don't store secrets in SQLite** — Constitution §II explicitly forbids this.
