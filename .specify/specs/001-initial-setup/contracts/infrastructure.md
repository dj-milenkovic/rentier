# Contract: Infrastructure Interfaces

**Feature**: `001-initial-setup`  
**Layer**: `Rentier.Application/Interfaces/` (interface) + `Rentier.Infrastructure/Security/` (stub implementation)  
**Date**: 2026-04-06

---

## Overview

This document defines the infrastructure-facing interfaces declared in `Rentier.Application` that
abstract OS-specific or external-system capabilities. Concrete stub implementations reside in
`Rentier.Infrastructure` and are the only layer permitted to hold platform-specific code.

In this feature, only `ICredentialStore` is introduced. Additional infrastructure interfaces
(e.g., `IExchangeRateFetcher`, `IMailClient`) are introduced by the features that require them.

---

## ICredentialStore

**Interface file**: `Rentier.Application/Interfaces/ICredentialStore.cs`  
**Stub implementation file**: `Rentier.Infrastructure/Security/OsCredentialStore.cs`

### Purpose

Abstracts OS-managed secure credential storage so that IMAP passwords and other application
secrets are never stored in SQLite, plaintext files, configuration, or environment variables
(constitution Principle II). The interface lives in Application so that Desktop ViewModels and
Application handlers can declare a dependency on it without referencing Infrastructure. The
concrete implementation lives in Infrastructure and is registered at the DI composition root.

### Contract

| Method | Signature | Description |
|--------|-----------|-------------|
| **SaveCredentialAsync** | `Task SaveCredentialAsync(string key, string secret, CancellationToken ct = default)` | Persist a secret in the OS credential store under the given key. If an entry with that key already exists, it is overwritten. |
| **GetCredentialAsync** | `Task<string?> GetCredentialAsync(string key, CancellationToken ct = default)` | Retrieve a previously stored secret by key. Returns `null` if no credential with that key exists. |
| **DeleteCredentialAsync** | `Task DeleteCredentialAsync(string key, CancellationToken ct = default)` | Remove a stored credential by key. No-op if the key does not exist. |

### Key Naming Convention

Keys are application-scoped strings of the form:

```
Rentier/<entity-type>/<entity-id>/<field>
```

Examples:
- `Rentier/Mailbox/3f2a7e9b-0000-0000-0000-000000000001/ImapPassword`

This convention prevents key collisions across multiple configured mailboxes and allows all
credentials for a deleted entity to be enumerated and removed by prefix (future feature).

### Invariants and Constraints

- The `key` parameter MUST NOT be null or empty.
- The `secret` parameter for `SaveCredentialAsync` MUST NOT be null.
- IMAP passwords MUST be stored exclusively via this interface — nowhere else in the codebase.
- The interface MUST NOT expose any OS-specific types in its method signatures.
- Callers in `Rentier.Application` and `Rentier.Desktop` MUST depend only on the interface, never
  on the concrete `OsCredentialStore` class.

### OsCredentialStore Stub (Infrastructure)

The stub implementation in `Rentier.Infrastructure/Security/OsCredentialStore.cs`:

- Implements `ICredentialStore`.
- Is marked `sealed`.
- Uses platform-conditional compilation to select the OS-specific backend:
  - **Windows** (`#if WINDOWS` or runtime OS check): `Windows.Security.Credentials.PasswordVault`
  - **macOS** (`#if MACOS` or runtime OS check): `Security.SecKeychain` / `Security.SecRecord`
- All three method bodies throw `NotImplementedException` in this feature. Full implementation
  is deferred to the IMAP mailbox configuration feature.
- The class constructor accepts no parameters in the stub; the DI container registers it as
  `Transient`.

### DI Registration

> **Deferred to IMAP mailbox feature**: The `ICredentialStore → OsCredentialStore` DI registration
> is intentionally omitted from this scaffold. `Rentier.Desktop` references `Rentier.Application`
> and `Rentier.Domain` only — it does NOT reference `Rentier.Infrastructure` (constitution
> Principle I). Therefore, `CompositionRoot.cs` in Desktop cannot directly register
> `OsCredentialStore`.
>
> When the IMAP mailbox feature is implemented, `Rentier.Infrastructure` will expose a
> `public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)`
> extension method. This extension will be called from `Program.cs` or `App.axaml.cs` before
> building the DI container, providing the composition seam without violating layer boundaries.

Future registration pattern (for IMAP feature):

```text
// In Rentier.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
services.AddTransient<ICredentialStore, OsCredentialStore>();

// In Rentier.Desktop/App.axaml.cs (OnFrameworkInitializationCompleted)
services.AddInfrastructureServices();
services.AddDesktopServices();
var provider = services.BuildServiceProvider();
```

### Testability

Because `ICredentialStore` is a plain interface in `Rentier.Application`, tests in
`Rentier.Application.Tests` and `Rentier.Desktop.Tests` can substitute it with an NSubstitute mock:

```text
var credentialStore = Substitute.For<ICredentialStore>();
credentialStore.GetCredentialAsync("Rentier/Mailbox/.../ImapPassword")
               .Returns(Task.FromResult<string?>("test-password"));
```

No OS credential store is accessed in unit tests. Integration tests (future) that require real
credential storage would be tagged `[Trait("Category", "Integration")]` and excluded from the
default CI test run.

---

## Future Infrastructure Interfaces (Not in This Feature)

The following interfaces are expected in later features but are listed here to give implementors
a complete picture of planned Infrastructure contracts:

| Interface | Feature | Description |
|-----------|---------|-------------|
| `IExchangeRateFetcher` | NBS scraping feature | Fetches daily rates from the NBS public API |
| `IMailClient` | IMAP sync feature | Connects to a mailbox and fetches messages using MailKit |
| `ICsvImporter` | IBKR import feature | Reads and parses IBKR CSV activity statements via CsvHelper |
| `IXmlSerializer` | PP-OPO XML generation feature | Serialises filing data to ePorezi-compatible XML |

Each of these will be declared in `Rentier.Application` and implemented in
`Rentier.Infrastructure` following the same pattern as `ICredentialStore`.
