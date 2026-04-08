# Research: IMAP Mailbox Configuration (Feature 004)

**Phase**: 0 — Pre-Design Research  
**Branch**: `feature/004-mailbox-configuration`  
**Date**: 2026-04-06

---

## R-1 — Windows Credential Store: P/Invoke vs PasswordVault vs DPAPI

### Decision
Use **Windows Credential Manager via P/Invoke** (`advapi32.dll`):  
`CredWriteW`, `CredReadW`, `CredFreeW`, `CredDeleteW`.

### Rationale
| Approach | Works with `net8.0` plain TFM | Notes |
|---|---|---|
| **P/Invoke `advapi32.dll`** | ✅ Yes | No TFM change; identical security to PasswordVault; no extra NuGet |
| `Windows.Security.Credentials.PasswordVault` | ❌ No | Requires `net8.0-windows10.0.17763.0` TFM on at least one project |
| `System.Security.Cryptography.ProtectedData` (DPAPI) | ✅ Yes | Encrypts bytes, but does NOT persist via OS credential manager — survives only as an encrypted blob in SQLite, which is weaker than the credential manager and potentially leaks key material |
| `SecretManagement` PowerShell module | ❌ N/A | Not a .NET library API; scripting-layer only |

DPAPI is ruled out because constitution Principle II requires credentials to be stored **in the OS credential store**, not in SQLite (even encrypted). P/Invoke gives an identical result to PasswordVault without requiring a Windows-specific TFM.

### Alternatives Considered
- **`Microsoft.Windows.SDK.Contracts`** NuGet: provides WinRT APIs on `net8.0`, but adds a large dependency, conflicts with the intent to keep `net8.0` plain, and is not needed.
- **`Ben.Demystifier` or `Meziantou.Framework.Win32.CredentialManager`** NuGet: thin wrappers around the same P/Invoke; ruled out to keep dependency count minimal (P/Invoke is short enough to inline).

### Key Implementation Details
- **Credential type**: `CRED_TYPE_GENERIC` (1) — intended for application credentials not tied to a domain.
- **Persistence**: `CRED_PERSIST_LOCAL_MACHINE` (2) — credential persists across reboots; tied to the Windows user account.
- **Blob encoding**: `Encoding.UTF8.GetBytes(secret)` written to `CredentialBlob`; `CredentialBlobSize` = byte count.
- **Read back**: `Marshal.PtrToStringUni` is **wrong** for UTF-8 blobs; use `Marshal.Copy` + `Encoding.UTF8.GetString`.
- **Memory management**: `CredFreeW` must always be called on the pointer returned by `CredReadW`, even on error paths — use `try/finally`.
- **Not-found vs. error**: `CredReadW` returns `false` with `GetLastWin32Error()` == 1168 (`ERROR_NOT_FOUND`) on missing credential; this is a non-error case (return `null`).
- **Thread safety**: The Credential Manager APIs are thread-safe at the OS level; no additional locking needed.
- **Platform guard**: All methods carry `[SupportedOSPlatform("windows")]`; `OsCredentialStore` class itself is annotated too.
- **Key format**: `Rentier/Mailbox/{mailboxId}` — e.g., `Rentier/Mailbox/3fa85f64-5717-4562-b3fc-2c963f66afa6`. Using `Guid.ToString()` (lower-hyphenated) is canonical.

---

## R-2 — EF Core 8 `OwnsOne` Patterns for Value Objects

### Decision
Map `MailboxCursor` as **`OwnsOne<MailboxCursor>`** in `MailboxConfiguration`. Columns: `Cursor_LastSyncDate` (nullable `DateOnly`) and `Cursor_LastUid` (nullable `long`). Both inline on the `Mailboxes` table.

### Rationale
- `OwnsOne` stores the value object columns inline in the owning entity's table — avoids a join, appropriate for a small value object always fetched with the parent.
- EF Core 8 natively supports `DateOnly` for SQLite (stored as TEXT `YYYY-MM-DD`); no custom converter is required.
- `MailboxCursor` is a `record` — EF Core 8 supports owned type materialization of records through a parameterized constructor match: fields named `LastSyncDate` and `LastUid` match ctor parameters of the same name (case-insensitive).
- Alternative: own table with `OwnsOne(..., o => o.ToTable("MailboxCursors"))` — adds a join for every query; not justified for a two-column value object.
- Alternative: `[Owned]` attribute on the record — works, but Fluent API is preferred (constitution style; avoids domain project depending on EF Core attributes).

### Configuration Pattern
```csharp
builder.OwnsOne(m => m.Cursor, cursor =>
{
    cursor.Property(c => c.LastSyncDate)
          .HasColumnName("Cursor_LastSyncDate")
          .IsRequired(false);
    cursor.Property(c => c.LastUid)
          .HasColumnName("Cursor_LastUid")
          .IsRequired(false);
});
```

### EF Core Private Constructor + Private Setters Pattern
EF Core 8 supports private parameterless constructors and private property setters via:
1. Add `private Mailbox() { }` — EF uses this for materialization.
2. Change all auto-properties to `{ get; private set; }` — EF can set them through reflection (shadow-property channel).
3. Alternatively, use `HasField` Fluent API to bind backing fields directly; prefer private setters for clarity.

---

## R-3 — Avalonia Password Input (No Built-in PasswordBox)

### Decision
Use a standard Avalonia `TextBox` with `PasswordChar="•"` property to mask the password field.

### Rationale
- Avalonia 11 does not ship a dedicated `PasswordBox` control.
- `TextBox` with `PasswordChar` (any single character, commonly `•` U+2022 or `*`) masks input visually while the `Text` property still carries the plain-text value — suitable for binding.
- Alternative: `MaskedTextBox` via the `Avalonia.Controls.Primitives` namespace — not present in Avalonia 11 stable; requires extra packages.
- Alternative: A community `PasswordBox` via `Material.Avalonia` — adds a heavy theming dependency; overkill for a single field.

### Binding Note
```xml
<TextBox PasswordChar="•" Text="{Binding Password}" Watermark="Leave blank to keep existing" />
```
- On load: leave `Password` binding empty (AD-4: never retrieve credentials for display).
- On save: empty string → "keep existing"; non-empty → write to credential store.

---

## R-4 — `CredWriteW` Struct Layout and Error Handling

### Decision
Use `[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]` on `CREDENTIALW`. Use `Marshal.GetLastPInvokeError()` (not `GetLastWin32Error`) for P/Invoke error retrieval in .NET 6+.

### Rationale
- `CharSet.Unicode` on the struct is required so that `string` fields (TargetName, Comment, TargetAlias, UserName) are marshalled as `LPWSTR` (UTF-16) — matching the `W` suffix of all Win32 APIs used.
- `LayoutKind.Sequential` preserves field ordering required by the Win32 ABI.
- `Marshal.GetLastPInvokeError()` (introduced .NET 6) is the correct method; `GetLastWin32Error` has subtle caching behaviour that can be unreliable in managed code after the P/Invoke.
- `CredentialBlobSize` is a `uint` (unsigned 32-bit); the marshalled IntPtr for `CredentialBlob` must be allocated with `Marshal.AllocHGlobal` and freed with `Marshal.FreeHGlobal` after `CredWriteW` returns.

### Struct Layout
```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct CREDENTIALW
{
    public uint           Flags;
    public uint           Type;
    public string         TargetName;
    public string?        Comment;
    public FILETIME       LastWritten;
    public uint           CredentialBlobSize;
    public IntPtr         CredentialBlob;
    public uint           Persist;
    public uint           AttributeCount;
    public IntPtr         Attributes;
    public string?        TargetAlias;
    public string?        UserName;
}
```

### FILETIME
`FILETIME` is a struct of two `uint` fields (`dwLowDateTime`, `dwHighDateTime`). Zeroing both is valid for `CredWriteW` — Windows accepts it for new credentials.

### Error Codes to Handle
| Code | Hex | Meaning | Action |
|------|-----|---------|--------|
| 0 | 0x0 | `ERROR_SUCCESS` | proceed |
| 1168 | 0x490 | `ERROR_NOT_FOUND` | return `null` (not an error for `GetCredentialAsync`) |
| 5 | 0x5 | `ERROR_ACCESS_DENIED` | propagate as `Infrastructure` error |
| 998 | 0x3E6 | `ERROR_NOACCESS` | propagate as `Infrastructure` error |

---

## R-5 — EF Core Migration Naming Convention

### Decision
Migration name: **`0004_MailboxConfiguration`** (next after `0002_TaxpayerProfile`).

### Rationale
- Existing migrations: `0001_InitialCreate`, `0002_TaxpayerProfile`.
- Feature 003 (Holiday Config) would use `0003_`; this feature is 004.
- No feature-003 migration exists in the current codebase, so `0004_MailboxConfiguration` is correct (avoids forcing a gap — EF Core orders by timestamp, not by the embedded number, but consistency aids readability).
- Migration creates: table `Mailboxes` with columns `Id`, `Host`, `Port`, `Username`, `InitialSyncDate`, `Cursor_LastSyncDate`, `Cursor_LastUid`.

---

## R-6 — Mailbox Domain Entity Modification Strategy (Private Setters + EF)

### Decision
- Add `private Mailbox() { }` for EF materialization.
- Convert all auto-properties to `{ get; private set; }`.
- Add `public DateOnly InitialSyncDate { get; private set; }`.
- Add `public static Mailbox Create(...)` factory (replaces direct constructor usage).
- Keep public constructor for backward compatibility within Domain/Application only; rename intent: factory for Application use, public ctor retained for tests if needed.
- Add `public void UpdateCursor(MailboxCursor newCursor)` mutation method — used by future sync feature.

### Rationale
EF Core 8 with private setters: when a type has a parameterless private constructor, EF uses it to instantiate the entity, then sets each property via reflection (it can access private setters). This is the canonical approach documented in EF Core for Domain-Driven Design.

Using a static factory (`Mailbox.Create(...)`) ensures validation always runs. The public constructor is kept (with required args) for test harness compatibility. EF ignores it during materialization as long as the no-arg private ctor exists.
