# Quickstart: IMAP Mailbox Configuration (Feature 004)

**Branch**: `feature/004-mailbox-configuration`  
**Prerequisite**: Feature 002 (Taxpayer Profile) merged to `develop`

---

## 1. Environment Prerequisites

| Tool | Version | Check |
|------|---------|-------|
| .NET SDK | 8.x | `dotnet --version` |
| EF Core tools | 8.x | `dotnet ef --version` |
| OS | Windows (for P/Invoke credential store) | — |
| Git | any | `git --version` |

---

## 2. Branch Setup

```powershell
# From repo root
git checkout develop
git pull
git checkout -b feature/004-mailbox-configuration
```

---

## 3. Build — Verify Clean State

```powershell
cd F:\Projects\Rentier\rentier
dotnet build --no-incremental -warnaserror
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

---

## 4. Implementation Order

Follow this sequence to avoid compiler errors mid-flight (dependencies flow Domain → Application → Infrastructure → Desktop):

### Step 4.1 — Modify `Mailbox` Domain Entity

File: `src/Rentier.Domain/Entities/Mailbox.cs`

Changes required:
- Add `private Mailbox() { }` EF constructor.
- Change all `{ get; }` to `{ get; private set; }`.
- Add `public DateOnly InitialSyncDate { get; private set; }`.
- Update existing public constructor to accept `DateOnly initialSyncDate` parameter.
- Add `public static Mailbox Create(string host, int port, string username, DateOnly initialSyncDate)` factory.
- Add `public void UpdateCursor(MailboxCursor newCursor)` method.

See `data-model.md §1.1` for the complete before/after.

### Step 4.2 — Add Application DTOs

File: `src/Rentier.Application/DTOs/MailboxDto.cs` ← **NEW**

```csharp
namespace Rentier.Application.DTOs;

public sealed record MailboxDto(
    Guid       Id,
    string     Host,
    int        Port,
    string     Username,
    DateOnly   InitialSyncDate,
    DateOnly?  LastSyncDate,
    long?      LastUid);
```

### Step 4.3 — Add Application Commands

Create in `src/Rentier.Application/Commands/`:
- `AddMailboxCommand.cs`
- `UpdateMailboxCommand.cs`
- `DeleteMailboxCommand.cs`

Create in `src/Rentier.Application/Queries/`:
- `GetMailboxesQuery.cs`

See `data-model.md §3` for record definitions.

### Step 4.4 — Add Application Handlers

Create in `src/Rentier.Application/Handlers/`:
- `AddMailboxCommandHandler.cs`
- `UpdateMailboxCommandHandler.cs`
- `DeleteMailboxCommandHandler.cs`
- `GetMailboxesQueryHandler.cs`

**Note**: `IMailboxRepository` already exists at `src/Rentier.Application/Repositories/IMailboxRepository.cs`. No changes needed.

Handler responsibility summary:
- `AddMailboxCommandHandler` → `Mailbox.Create(...)` → `_repo.AddAsync(...)` → save password if non-empty.
- `UpdateMailboxCommandHandler` → `GetByIdAsync` → construct updated entity → `UpdateAsync` → save password if non-empty.
- `DeleteMailboxCommandHandler` → delete credential (swallow not-found) → `DeleteAsync`.
- `GetMailboxesQueryHandler` → `GetAllAsync` → project to `MailboxDto` list.

See `contracts/IMailboxRepository.cs` pseudo-code section for full handler logic.

### Step 4.5 — Implement `OsCredentialStore`

File: `src/Rentier.Infrastructure/Security/OsCredentialStore.cs` ← **REPLACE STUB**

Replace the three `NotImplementedException` methods with the full P/Invoke implementation.  
Full method bodies and struct definitions: `contracts/OsCredentialStoreImpl.md`.

### Step 4.6 — Add EF Core Configuration

File: `src/Rentier.Infrastructure/Persistence/Configurations/MailboxConfiguration.cs` ← **NEW**

See `data-model.md §5.1` for the complete `MailboxConfiguration` class.

### Step 4.7 — Update `AppDbContext`

File: `src/Rentier.Infrastructure/Persistence/AppDbContext.cs` ← **MODIFIED**

Add one line:
```csharp
public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
```

Add `using Rentier.Domain.Entities;` if not already present.

### Step 4.8 — Register Services in `InfrastructureServiceExtensions`

File: `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` ← **MODIFIED**

Add:
```csharp
services.AddTransient<ICredentialStore, OsCredentialStore>();
services.AddTransient<IMailboxRepository, MailboxRepository>();
```

Add required `using` statements:
```csharp
using Rentier.Application.Interfaces;
using Rentier.Infrastructure.Security;
```

### Step 4.9 — Add `MailboxRepository`

File: `src/Rentier.Infrastructure/Repositories/MailboxRepository.cs` ← **NEW**

Pattern: same as `TaxpayerProfileRepository` but multi-record. Key methods:
- `GetByIdAsync` — `FirstOrDefaultAsync(m => m.Id == id, ct)`
- `GetAllAsync` — `_context.Mailboxes.AsNoTracking().ToListAsync(ct)` wrapped as `IReadOnlyList<Mailbox>`
- `AddAsync` — `_context.Mailboxes.Add(mailbox)` + `SaveChangesAsync`
- `UpdateAsync` — detach stale tracked entry if any → `_context.Mailboxes.Update(mailbox)` + `SaveChangesAsync`
- `DeleteAsync` — `ExecuteDeleteAsync(m => m.Id == id)` (EF 8 bulk delete; no-op if not found)

### Step 4.10 — Add EF Core Migration

```powershell
cd F:\Projects\Rentier\rentier
dotnet ef migrations add 0004_MailboxConfiguration `
    --project src/Rentier.Infrastructure `
    --startup-project src/Rentier.Desktop
```

Verify the generated migration creates table `Mailboxes` with all 7 columns (see `data-model.md §6.1`).

Apply locally:
```powershell
dotnet ef database update `
    --project src/Rentier.Infrastructure `
    --startup-project src/Rentier.Desktop
```

### Step 4.11 — Add Desktop ViewModels

Files to create in `src/Rentier.Desktop/ViewModels/`:

**`MailboxItemViewModel.cs`** — list-item wrapper:
```csharp
public sealed class MailboxItemViewModel : ReactiveObject
{
    public Guid     Id              { get; }
    public string   DisplayName     { get; }   // "{Username} @ {Host}:{Port}"
    public bool     IsNew           { get; set; }  // true = not yet persisted

    public MailboxItemViewModel(MailboxDto dto) { ... }
    public MailboxItemViewModel()               { IsNew = true; DisplayName = "(new)"; }
}
```

**`MailboxSettingsViewModel.cs`** — two-panel VM with:
- `ObservableCollection<MailboxItemViewModel> Mailboxes`
- `MailboxItemViewModel? SelectedMailbox` (RaiseAndSetIfChanged)
- `string Host`, `int Port`, `string Username`, `string Password`, `DateOnly InitialSyncDate` (all RaiseAndSetIfChanged)
- `ReactiveCommand AddCommand`, `SaveCommand`, `DeleteCommand`
- `LoadAsync()` for initial data population
- `string ErrorMessage`, `string SuccessMessage`, `bool IsLoading`

### Step 4.12 — Add Desktop Views

Files to create in `src/Rentier.Desktop/Views/`:
- `MailboxSettingsView.axaml` — two-panel `Grid`: left `ListBox` bound to `Mailboxes`, right `StackPanel` form.
- `MailboxSettingsView.axaml.cs` — `ReactiveUserControl<MailboxSettingsViewModel>` with `WhenActivated`.

### Step 4.13 — Wire `SettingsViewModel` + `SettingsView`

**`SettingsViewModel.cs`** — add `MailboxesTab` property:
```csharp
public MailboxSettingsViewModel MailboxesTab { get; }

public SettingsViewModel(ProfileSettingsViewModel profileTab,
                          MailboxSettingsViewModel mailboxesTab)
{
    ProfileTab  = profileTab;
    MailboxesTab = mailboxesTab;
}
```

**`SettingsView.axaml`** — add second `TabItem`:
```xml
<TabItem Header="{x:Static res:Strings.Settings_Mailboxes_TabHeader}">
  <views:MailboxSettingsView DataContext="{Binding MailboxesTab}" />
</TabItem>
```

**`Strings.resx`** — add string keys:
- `Settings_Mailboxes_TabHeader` = `"Mailboxes"`
- `Mailbox_Host_Label`, `Mailbox_Port_Label`, `Mailbox_Username_Label`, `Mailbox_Password_Label`, `Mailbox_InitialSyncDate_Label`
- `Mailbox_Add_Button` = `"Add"`, `Mailbox_Save_Button` = `"Save"`, `Mailbox_Delete_Button` = `"Delete"`
- `Mailbox_Saved_Confirmation` = `"Mailbox saved."`
- `Mailbox_Deleted_Confirmation` = `"Mailbox deleted."`

### Step 4.14 — Update `CompositionRoot`

File: `src/Rentier.Desktop/Composition/CompositionRoot.cs` ← **MODIFIED**

Add handler registrations:
```csharp
services.AddTransient<
    ICommandHandler<AddMailboxCommand, Result<Guid, Error>>,
    AddMailboxCommandHandler>();
services.AddTransient<
    ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>,
    UpdateMailboxCommandHandler>();
services.AddTransient<
    ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>,
    DeleteMailboxCommandHandler>();
services.AddTransient<
    IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>,
    GetMailboxesQueryHandler>();
```

Add ViewModel registration:
```csharp
services.AddTransient<MailboxItemViewModel>();
services.AddTransient<MailboxSettingsViewModel>();
```

Update `SettingsViewModel` constructor injection (update wiring to pass `MailboxSettingsViewModel`):
```csharp
services.AddTransient<SettingsViewModel>(sp =>
    new SettingsViewModel(
        sp.GetRequiredService<ProfileSettingsViewModel>(),
        sp.GetRequiredService<MailboxSettingsViewModel>()));
```

---

## 5. Build Verification

```powershell
dotnet build --no-incremental -warnaserror
```

Expected: `0 Warning(s). 0 Error(s).`

---

## 6. Run Tests

```powershell
# All tests
dotnet test --no-build

# Domain tests only
dotnet test tests/Rentier.Domain.Tests --no-build

# Application tests
dotnet test tests/Rentier.Application.Tests --no-build

# Infrastructure tests
dotnet test tests/Rentier.Infrastructure.Tests --no-build

# Desktop tests
dotnet test tests/Rentier.Desktop.Tests --no-build
```

Expected test files for this feature:
| File | Count | Coverage Target |
|------|-------|-----------------|
| `MailboxTests.cs` | ~12 cases | 100% domain rules |
| `AddMailboxCommandHandlerTests.cs` | ~4 cases | handler paths |
| `UpdateMailboxCommandHandlerTests.cs` | ~4 cases | handler paths |
| `DeleteMailboxCommandHandlerTests.cs` | ~3 cases | handler paths |
| `GetMailboxesQueryHandlerTests.cs` | ~2 cases | empty + populated |
| `MailboxRepositoryTests.cs` | ~6 cases | EF InMemory |
| `MailboxSettingsViewModelTests.cs` | ~6 cases | VM commands + state |

---

## 7. Manual Smoke Test

1. Run `dotnet run --project src/Rentier.Desktop`.
2. Navigate to **Settings → Mailboxes** tab.
3. Click **Add** — form clears; new unsaved entry appears in the list.
4. Fill in: Host = `imap.gmail.com`, Port = `993`, Username = `test@example.com`, Password = `secret`, Initial Date = `2024-01-01`.
5. Click **Save** — entry updates in list; success message appears.
6. Close and reopen the app — mailbox persists in the list.
7. Select the mailbox — form populates (password field is empty per AD-4).
8. Enter new password, click **Save** — credential updated in Windows Credential Manager.
9. Verify via Windows: Control Panel → Credential Manager → Windows Credentials → look for `Rentier/Mailbox/{Id}`.
10. Click **Delete** — mailbox removed from list; credential removed from Credential Manager.

---

## 8. Rollback

```powershell
# Revert database migration (if applied)
dotnet ef database update 0002_TaxpayerProfile `
    --project src/Rentier.Infrastructure `
    --startup-project src/Rentier.Desktop

# Remove migration files
dotnet ef migrations remove `
    --project src/Rentier.Infrastructure `
    --startup-project src/Rentier.Desktop

# Delete feature branch
git checkout develop
git branch -D feature/004-mailbox-configuration
```
