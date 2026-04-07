# Feature 004 — IMAP Mailbox Configuration: Clarifications

**Status**: Resolved  
**Date**: 2026-04-06  
**Feature**: Settings → Mailboxes — CRUD for IMAP connection configs, OsCredentialStore implementation  
**Method**: Autonomous resolution (all 5 questions answered before specifying)

---

## Resolved Questions

### Q1 — OS Credential Store Implementation Approach

**Question**: The user specified `Windows.Security.Credentials.PasswordVault`. However, the project targets `net8.0` (plain, not `net8.0-windows10.0.x`). PasswordVault is a WinRT API requiring a Windows TFM. What implementation approach?

**Decision**: Implement `OsCredentialStore` using **Windows Credential Manager via P/Invoke** (`advapi32.dll` — `CredWriteW`, `CredReadW`, `CredFreeW`, `CredDeleteW`). This approach:
- Works with `net8.0` (no TFM change to any project)
- Stores credentials in the Windows Credential Locker (OS-managed, user-account-bound)
- Achieves identical security guarantee to PasswordVault
- Satisfies constitution Principle II (OS credential store, not SQLite)
- No extra NuGet package needed
- Mark methods with `[SupportedOSPlatform("windows")]`

**Key format**: `Rentier/Mailbox/{mailboxId}` (e.g., `Rentier/Mailbox/3fa85f64-5717-4562-b3fc-2c963f66afa6`)

---

### Q2 — Multiple Mailboxes and UI Layout

**Question**: Can a user have multiple mailboxes? What is the UX layout?

**Decision**: Yes, multiple mailboxes are supported (one per email account / per broker).  
**UI pattern**: Two-panel layout within the Mailboxes tab:
- **Left panel**: `ListBox` of mailbox display names (`{Username} @ {Host}:{Port}`) — selecting one populates the form
- **Right panel**: Form with all fields; Add, Save, Delete buttons
- "Add" clears form and creates a new mailbox entry in the list (unsaved, marked as new)
- "Save" persists the selected entry (AddMailboxCommand or UpdateMailboxCommand depending on whether it already has a DB Id)
- "Delete" calls DeleteMailboxCommand for the selected entry + removes from list

---

### Q3 — Cursor / Initial Sync Date

**Question**: The user sets an `initial cursor date`. How is this stored? Does the Mailbox entity change?

**Decision**:
- The `Mailbox` entity already has `MailboxCursor Cursor { get; }`. The initial cursor date populates `Cursor = new MailboxCursor(LastSyncDate: command.InitialSyncDate, LastUid: null)`.
- A new property `DateOnly InitialSyncDate { get; private set; }` is added to `Mailbox` for display purposes (the original date the user configured — not mutated by sync).
- The `MailboxCursor` is stored via EF `OwnsOne<MailboxCursor>` mapping with two nullable columns.
- On update, the `Cursor` can be replaced via an `UpdateCursor(MailboxCursor newCursor)` method on Mailbox.

---

### Q4 — Password on Edit

**Question**: When the user opens an existing mailbox for editing, should the password field be pre-populated?

**Decision**: **No** — the password field is always empty on load. The credential is never retrieved and displayed. Submitting an empty password field means "keep existing password". Submitting a non-empty field updates the credential in the OS store.

---

### Q5 — Delete Cascade

**Question**: When a mailbox is deleted, should its stored credential be deleted too?

**Decision**: **Yes** — `DeleteMailboxCommandHandler` calls `ICredentialStore.DeleteCredentialAsync` with the mailbox key, then `IMailboxRepository.DeleteAsync`. If the credential doesn't exist (was never saved), swallow the error silently. If the DB delete fails, propagate as `Result.Failure`.

---

## Architecture Decisions

### AD-1: IMailboxRepository in Application
```
src/Rentier.Application/Interfaces/IMailboxRepository.cs
```
Methods: `GetAllAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync(Guid id)`.

### AD-2: Mailbox Entity Modifications
The existing `Mailbox` entity needs:
- `private Mailbox() { }` (EF parameterless ctor)
- `private set` on all properties
- `public DateOnly InitialSyncDate { get; private set; }` — new field
- `public static Mailbox Create(string host, int port, string username, DateOnly initialSyncDate)` factory
- `public Mailbox WithCursor(MailboxCursor cursor)` — returns updated copy (for sync feature, can be a mutation too)
- Retain existing validation logic in constructor/factory

### AD-3: MailboxCursor EF Mapping
EF `OwnsOne<MailboxCursor>` in `MailboxConfiguration`:
- `LastSyncDate` → nullable `DateOnly` column
- `LastUid` → nullable `long` column
Both are nullable — null means no sync has occurred yet.

### AD-4: MailboxDto
```csharp
public record MailboxDto(Guid Id, string Host, int Port, string Username, DateOnly InitialSyncDate, DateOnly? LastSyncDate, long? LastUid);
```
No password field in DTO.

### AD-5: Commands
| Command | Input | Output |
|---------|-------|--------|
| `AddMailboxCommand` | Host, Port, Username, Password?, InitialSyncDate | `Result<Guid, Error>` |
| `UpdateMailboxCommand` | Id, Host, Port, Username, Password?, InitialSyncDate | `Result<VoidResult, Error>` |
| `DeleteMailboxCommand` | Guid Id | `Result<VoidResult, Error>` |
| `GetMailboxesQuery` | (none) | `Result<IReadOnlyList<MailboxDto>, Error>` |

Password is `string?` — null/empty means "keep existing" on update, "no password yet" on add (unlikely but allowed).

### AD-6: OsCredentialStore P/Invoke Implementation
- Struct: `CREDENTIALW` with `Type`, `TargetName`, `CredentialBlob`, `CredentialBlobSize`, `Persist`, `UserName`
- `CredWriteW` — saves/updates a credential
- `CredReadW` — reads a credential by target name
- `CredFreeW` — frees memory allocated by `CredReadW`
- `CredDeleteW` — deletes a credential
- Type: `CRED_TYPE_GENERIC` (1)
- Persist: `CRED_PERSIST_LOCAL_MACHINE` (2) — persists across reboots
- `[SupportedOSPlatform("windows")]` on all methods
- On CredReadW failure (not found): return `null` (not an error)

### AD-7: SettingsViewModel + View
- `SettingsViewModel` gets `MailboxesTab` property (constructor-injected `MailboxSettingsViewModel`)
- `SettingsView.axaml` gets third `TabItem` — "Mailboxes"

### AD-8: No IMAP connection testing
This feature creates configuration only. No IMAP connection is made. No `Test Connection` button.

---

## Assumptions

1. **Singleton-per-VM**: `MailboxSettingsViewModel` is long-lived (AddTransient, but effectively captive through MainWindowViewModel singleton) — consistent with Profile and Holiday tabs.
2. **CredentialBlob encoding**: UTF-8 byte array. `Encoding.UTF8.GetBytes(secret)`.
3. **Error on CredWriteW failure**: If Win32 call returns false, return `Result.Failure(new Error("CREDENTIAL_STORE_FAILED", Marshal.GetLastPInvokeErrorMessage()))`.
4. **AddTransient for OsCredentialStore** — consistent with pattern in InfrastructureServiceExtensions.
5. **No IMailboxRepository** exists yet — create it.
6. **Mailbox.Cursor** needs EF OwnsOne mapping — EF8 supports `DateOnly?` natively, so no custom converter needed.
7. **Migration**: `0004_MailboxConfiguration` creates `Mailboxes` table with columns for Host, Port, Username, InitialSyncDate, Cursor_LastSyncDate, Cursor_LastUid.
8. **VoidResult** (not Unit) in command results.
9. **RaiseAndSetIfChanged** in all ViewModels — no Fody, no CommunityToolkit observable properties.
10. **Password field binding**: Use `PasswordBox`-like masked input — Avalonia has no built-in PasswordBox; use `TextBox` with `PasswordChar="•"`. Binding via `Text` property.
11. **Selected mailbox**: `SelectedMailbox` property on VM of type `MailboxItemViewModel?`; Delete button CanExecute = `SelectedMailbox != null`.
12. **VaultKey constant**: `private const string VaultPrefix = "Rentier/Mailbox/";` — key = `VaultPrefix + mailboxId.ToString()`.
13. **Existing tests must not regress** — OsCredentialStore was a stub (`NotImplementedException`); after implementation, the Infrastructure.Tests project must mock it or test via abstraction.
14. **`net8.0` target stays unchanged** — P/Invoke does not require WinRT TFM change.
15. **No IMailboxRepository stub yet** — feature 001 generated a stub. Check and use if exists.
