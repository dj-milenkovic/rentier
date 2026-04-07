# OsCredentialStore P/Invoke Implementation Notes

**Contract**: `src/Rentier.Application/Interfaces/ICredentialStore.cs`  
**Implementation**: `src/Rentier.Infrastructure/Security/OsCredentialStore.cs`  
**Status**: Stub exists — **requires full implementation in this feature**

---

## 1. Windows Credential Manager API Overview

The implementation targets the Windows Credential Manager (`advapi32.dll`) using P/Invoke, which is fully compatible with `net8.0` (no Windows-specific TFM change required).

**Key format** used by Rentier for mailbox passwords:
```
Rentier/Mailbox/{mailboxId}
```
Example: `Rentier/Mailbox/3fa85f64-5717-4562-b3fc-2c963f66afa6`

The prefix constant should be declared as:
```csharp
private const string VaultPrefix = "Rentier/Mailbox/";
```
Note: `ICredentialStore` is generic — it receives the full key from the caller. The `VaultPrefix` is applied by the **Application handler** when constructing the key, not inside `OsCredentialStore`.

---

## 2. P/Invoke Declarations

### 2.1 Win32 API Imports

```csharp
[DllImport("advapi32.dll", EntryPoint = "CredWriteW",
           CharSet = CharSet.Unicode, SetLastError = true)]
private static extern bool CredWriteW(ref CREDENTIALW credential, uint flags);

[DllImport("advapi32.dll", EntryPoint = "CredReadW",
           CharSet = CharSet.Unicode, SetLastError = true)]
private static extern bool CredReadW(
    string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

[DllImport("advapi32.dll", EntryPoint = "CredFreeW")]
private static extern void CredFreeW(IntPtr buffer);  // Windows API returns void — do NOT declare as bool

[DllImport("advapi32.dll", EntryPoint = "CredDeleteW",
           CharSet = CharSet.Unicode, SetLastError = true)]
private static extern bool CredDeleteW(string target, uint type, uint flags);
```

### 2.2 `CREDENTIALW` Struct

```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
private struct CREDENTIALW
{
    public uint    Flags;
    public uint    Type;
    public string  TargetName;
    public string? Comment;
    public FILETIME LastWritten;
    public uint    CredentialBlobSize;
    public IntPtr  CredentialBlob;
    public uint    Persist;
    public uint    AttributeCount;
    public IntPtr  Attributes;
    public string? TargetAlias;
    public string? UserName;
}

[StructLayout(LayoutKind.Sequential)]
private struct FILETIME
{
    public uint LowDateTime;
    public uint HighDateTime;
}
```

### 2.3 Named Constants

```csharp
private const uint CRED_TYPE_GENERIC        = 1;
private const uint CRED_PERSIST_LOCAL_MACHINE = 2;
private const int  ERROR_NOT_FOUND          = 1168;  // 0x490
```

---

## 3. Method Implementations

### 3.1 `SaveCredentialAsync`

```csharp
[SupportedOSPlatform("windows")]
public Task SaveCredentialAsync(string key, string secret, CancellationToken ct = default)
{
    byte[] blob = Encoding.UTF8.GetBytes(secret);
    IntPtr blobPtr = Marshal.AllocHGlobal(blob.Length);
    try
    {
        Marshal.Copy(blob, 0, blobPtr, blob.Length);
        var cred = new CREDENTIALW
        {
            Flags              = 0,
            Type               = CRED_TYPE_GENERIC,
            TargetName         = key,
            Comment            = null,
            LastWritten        = default,
            CredentialBlobSize = (uint)blob.Length,
            CredentialBlob     = blobPtr,
            Persist            = CRED_PERSIST_LOCAL_MACHINE,
            AttributeCount     = 0,
            Attributes         = IntPtr.Zero,
            TargetAlias        = null,
            UserName           = null,
        };

        if (!CredWriteW(ref cred, 0))
        {
            int err = Marshal.GetLastPInvokeError();
            throw new InvalidOperationException(
                $"CredWriteW failed (key={key}): Win32 error {err} — {Marshal.GetPInvokeErrorMessage(err)}");
        }
    }
    finally
    {
        Marshal.FreeHGlobal(blobPtr);
    }
    return Task.CompletedTask;
}
```

### 3.2 `GetCredentialAsync`

```csharp
[SupportedOSPlatform("windows")]
public Task<string?> GetCredentialAsync(string key, CancellationToken ct = default)
{
    if (!CredReadW(key, CRED_TYPE_GENERIC, 0, out IntPtr credPtr))
    {
        int err = Marshal.GetLastPInvokeError();
        if (err == ERROR_NOT_FOUND)
            return Task.FromResult<string?>(null);   // Not-found is a non-error case

        throw new InvalidOperationException(
            $"CredReadW failed (key={key}): Win32 error {err} — {Marshal.GetPInvokeErrorMessage(err)}");
    }

    try
    {
        var cred = Marshal.PtrToStructure<CREDENTIALW>(credPtr);
        if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero)
            return Task.FromResult<string?>(null);

        byte[] blob = new byte[cred.CredentialBlobSize];
        Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);
        return Task.FromResult<string?>(Encoding.UTF8.GetString(blob));
    }
    finally
    {
        CredFreeW(credPtr);   // Must always free, even on exception paths
    }
}
```

**Important**: Do NOT use `Marshal.PtrToStringUni` on the blob pointer — the blob is UTF-8 bytes, not a null-terminated UTF-16 string. Always use `Marshal.Copy` + `Encoding.UTF8.GetString`.

### 3.3 `DeleteCredentialAsync`

```csharp
[SupportedOSPlatform("windows")]
public Task DeleteCredentialAsync(string key, CancellationToken ct = default)
{
    if (!CredDeleteW(key, CRED_TYPE_GENERIC, 0))
    {
        int err = Marshal.GetLastPInvokeError();
        if (err == ERROR_NOT_FOUND)
            return Task.CompletedTask;   // Credential was never stored — swallow silently

        throw new InvalidOperationException(
            $"CredDeleteW failed (key={key}): Win32 error {err} — {Marshal.GetPInvokeErrorMessage(err)}");
    }
    return Task.CompletedTask;
}
```

---

## 4. Class-Level Annotations

```csharp
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure.Security;

/// <summary>
/// Windows Credential Manager implementation of ICredentialStore.
/// Uses advapi32.dll P/Invoke — requires Windows OS; no WinRT TFM change needed.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class OsCredentialStore : ICredentialStore
{
    // ... (P/Invoke declarations, constants, and methods above)
}
```

---

## 5. Error Handling Contract

| Win32 Code | Decimal | Constant | Action |
|---|---|---|---|
| `0x0` | 0 | `ERROR_SUCCESS` | success — proceed normally |
| `0x490` | 1168 | `ERROR_NOT_FOUND` | `GetCredentialAsync` → return `null`; `DeleteCredentialAsync` → no-op |
| `0x5` | 5 | `ERROR_ACCESS_DENIED` | throw `InvalidOperationException` with code + message |
| `0x3E6` | 998 | `ERROR_NOACCESS` | throw `InvalidOperationException` with code + message |
| any other | — | — | throw `InvalidOperationException` with code + message |

`DeleteMailboxCommandHandler` wraps the credential delete in `try/catch` and swallows silently if the key does not exist (the handler has seen the clarification in AD-5: "swallow silently if key not found").

---

## 6. Testing Notes

- `OsCredentialStore` is Windows-only. Infrastructure tests for this class MUST either:
  - Be decorated with `[SkipUnlessWindowsPlatform]` / `[Fact(Skip = "Windows only")]` on non-Windows CI, OR
  - Be integration tests guarded by `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`.
- **Do not** use `NSubstitute` to mock `OsCredentialStore` directly — mock the `ICredentialStore` interface instead at the Application layer.
- Application handler tests (`AddMailboxCommandHandlerTests`, `DeleteMailboxCommandHandlerTests`) mock `ICredentialStore` via NSubstitute. No P/Invoke occurs in those tests.
- Infrastructure repository tests (`MailboxRepositoryTests`) use EF Core In-Memory provider and do **not** involve `ICredentialStore`.

---

## 7. DI Registration

In `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`, add:

```csharp
services.AddTransient<ICredentialStore, OsCredentialStore>();
services.AddTransient<IMailboxRepository, MailboxRepository>();
```

`AddTransient` is consistent with the existing pattern (Feature 002 uses `AddTransient` for all repositories). The Desktop uses a root `ServiceProvider` — `AddTransient` is correct per constitution AD-1.
