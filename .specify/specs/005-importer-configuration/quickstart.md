# Quickstart: Feature 005 — Importer Configuration

**Branch**: `feature/005-importer-configuration`  
**Date**: 2026-04-06  
**Prerequisite**: Feature 004 (Mailbox Configuration) must be fully applied — migration 0004 must exist in the DB snapshot.

---

## Overview

This guide walks through building, migrating, and testing Feature 005 from scratch on the `feature/005-importer-configuration` branch.

---

## Step 1: Switch to Feature Branch

```bash
git checkout feature/005-importer-configuration
# or create it from develop:
git checkout develop
git checkout -b feature/005-importer-configuration
```

---

## Step 2: Verify Prerequisites

Ensure Feature 004 is already applied and the EF model snapshot includes the `Mailboxes` table:

```bash
# Check that migration 0004 exists
ls src/Rentier.Infrastructure/Persistence/Migrations/ | grep 0004

# Confirm the DB is up to date (apply 004 if not already)
dotnet ef database update --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop
```

---

## Step 3: Implement Domain Changes

1. **Create the `Enums/` folder** under `src/Rentier.Domain/`:
   ```
   src/Rentier.Domain/Enums/ReportType.cs
   ```
   Content: `public enum ReportType { IbkrCsv = 0 }`

2. **Redesign `src/Rentier.Domain/Entities/Importer.cs`**:
   - Replace the existing public constructor.
   - Add `private Importer() {}` for EF.
   - Change all `{ get; }` to `{ get; private set; }`.
   - Remove `FilterExpression`.
   - Add `ReportType`, `TaxpayerProfileId`, `MailboxId`, `FromFilter`, `SubjectFilter`, `AttachmentRegex`, `PaymentNotes`.
   - Add `Importer.Create(string, ReportType)` factory.
   - Add `void UpdateDetails(...)` mutation method.

   See `data-model.md` Section 1 for the full redesigned class.

---

## Step 4: Implement Application Layer

Create files in order (no circular deps):

```
src/Rentier.Application/DTOs/ImporterDto.cs
src/Rentier.Application/Queries/GetImportersQuery.cs
src/Rentier.Application/Commands/AddImporterCommand.cs
src/Rentier.Application/Commands/UpdateImporterCommand.cs
src/Rentier.Application/Commands/DeleteImporterCommand.cs
src/Rentier.Application/Handlers/GetImportersQueryHandler.cs
src/Rentier.Application/Handlers/AddImporterCommandHandler.cs
src/Rentier.Application/Handlers/UpdateImporterCommandHandler.cs
src/Rentier.Application/Handlers/DeleteImporterCommandHandler.cs
```

Key handler logic:
- **AddImporterCommandHandler**: validate `AttachmentRegex` → `Importer.Create(...)` → `UpdateDetails(...)` → `AddAsync`.
- **UpdateImporterCommandHandler**: `GetByIdAsync` → 404 check → validate regex → `UpdateDetails(...)` → `UpdateAsync`.
- **DeleteImporterCommandHandler**: `DeleteAsync` (no-op if missing — no explicit check needed).
- **GetImportersQueryHandler**: `GetAllAsync()` → project each `Importer` to `ImporterDto`.

---

## Step 5: Implement Infrastructure Layer

```
src/Rentier.Infrastructure/Persistence/Configurations/ImporterConfiguration.cs
```
See `data-model.md` Section 4 for the full EF configuration.

Modify `src/Rentier.Infrastructure/Persistence/AppDbContext.cs`:
```csharp
public DbSet<Importer> Importers => Set<Importer>();
```

Create `src/Rentier.Infrastructure/Repositories/ImporterRepository.cs` implementing `IImporterRepository`.

Modify `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`:
```csharp
services.AddTransient<IImporterRepository, ImporterRepository>();
```

---

## Step 6: Run EF Migration

```bash
dotnet ef migrations add 0005_ImporterConfiguration \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Desktop

# Apply to local dev DB
dotnet ef database update \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Desktop
```

Verify the generated migration creates:
- Table `Importers` with 9 columns.
- FK constraints to `TaxpayerProfiles` and `Mailboxes` with `ON DELETE SET NULL`.

---

## Step 7: Implement Desktop Layer

```
src/Rentier.Desktop/Extensions/ReportTypeExtensions.cs
src/Rentier.Desktop/ViewModels/ImporterItemViewModel.cs
src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs
src/Rentier.Desktop/Views/ImporterSettingsView.axaml
src/Rentier.Desktop/Views/ImporterSettingsView.axaml.cs
```

Modify:
```
src/Rentier.Desktop/Composition/CompositionRoot.cs    ← add 4 handler registrations + ImporterSettingsViewModel
src/Rentier.Desktop/ViewModels/SettingsViewModel.cs   ← add ImportersTab as 4th param
src/Rentier.Desktop/Views/SettingsView.axaml          ← add 4th tab item
src/Rentier.Desktop/Resources/Strings.resx            ← add 14 string keys
```

**Strings.resx keys to add**:

| Key | Value |
|---|---|
| `Settings_Importers_TabHeader` | `Importers` |
| `Importers_DisplayName_Label` | `Display Name` |
| `Importers_ReportType_Label` | `Report Type` |
| `Importers_TaxpayerProfile_Label` | `Taxpayer Profile` |
| `Importers_Mailbox_Label` | `Mailbox` |
| `Importers_FromFilter_Label` | `From Filter` |
| `Importers_SubjectFilter_Label` | `Subject Filter` |
| `Importers_AttachmentRegex_Label` | `Attachment Pattern (Regex)` |
| `Importers_PaymentNotes_Label` | `Payment Notes` |
| `Importers_AddNew_Button` | `Add New` |
| `Importers_Save_Button` | `Save` |
| `Importers_Delete_Button` | `Delete` |
| `Importers_Saved_Confirmation` | `Importer saved.` |
| `Importers_NoneOption_Label` | `(none)` |

---

## Step 8: Build

```bash
dotnet build rentier.sln
```

Expected: **0 errors, 0 warnings**.

Common errors to watch for:
- `Importer` constructor changes break any existing usages → update to `Importer.Create(...)`.
- Missing `using Rentier.Domain.Enums;` in files referencing `ReportType`.
- `SettingsViewModel` constructor arity mismatch → update `CompositionRoot` factory lambda.

---

## Step 9: Run Tests

```bash
# All tests
dotnet test rentier.sln

# Individual test projects
dotnet test tests/Rentier.Domain.Tests/Rentier.Domain.Tests.csproj -v minimal
dotnet test tests/Rentier.Application.Tests/Rentier.Application.Tests.csproj -v minimal
dotnet test tests/Rentier.Infrastructure.Tests/Rentier.Infrastructure.Tests.csproj -v minimal
dotnet test tests/Rentier.Desktop.Tests/Rentier.Desktop.Tests.csproj -v minimal
```

Expected test files (new in this feature):
- `tests/Rentier.Domain.Tests/ImporterTests.cs` (~10 cases)
- `tests/Rentier.Application.Tests/AddImporterCommandHandlerTests.cs` (~4 cases)
- `tests/Rentier.Application.Tests/UpdateImporterCommandHandlerTests.cs` (~4 cases)
- `tests/Rentier.Application.Tests/DeleteImporterCommandHandlerTests.cs` (~2 cases)
- `tests/Rentier.Application.Tests/GetImportersQueryHandlerTests.cs` (~2 cases)
- `tests/Rentier.Infrastructure.Tests/ImporterRepositoryTests.cs` (~6 cases)
- `tests/Rentier.Desktop.Tests/ImporterSettingsViewModelTests.cs` (~7 cases)

---

## Step 10: Manual Smoke Test

1. Launch `Rentier.Desktop`.
2. Open **Settings** → verify four tabs: Profile, Holidays, Mailboxes, **Importers**.
3. Click **Importers** tab → list is empty, form is blank.
4. Click **Add New** → placeholder row appears in list, form clears.
5. Enter a display name (e.g. "My IBKR") → click **Save** → importer appears in list.
6. Click the saved importer → form populates with saved values.
7. Change display name → **Save** → list entry updates.
8. Click **Delete** → importer removed from list and DB.
9. Enter an invalid regex (e.g. `[`) in **Attachment Pattern** → **Save** → inline error shown, no DB write.
10. Leave TaxpayerProfile and Mailbox dropdowns unset → **Save** → importer saved successfully with null FKs.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| Migration fails: "no such table: Mailboxes" | Apply migration 0004 first (`dotnet ef database update` on feature/004 branch) |
| `DomainException` on app startup | Check that `Importer.Create(...)` is used wherever importers are constructed; old `new Importer(id, name)` calls must be replaced |
| ComboBox shows no items | Ensure `LoadAsync()` is called in `WhenActivated`; check that profile/mailbox handlers are registered in `CompositionRoot` |
| `SettingsViewModel` DI error | Confirm `ImporterSettingsViewModel` is registered with `AddTransient` and SettingsViewModel factory injects it as 4th arg |
| Regex validation not triggered | Confirm `AttachmentRegex` is non-empty before `new Regex(...)` call; empty string is valid (no validation) |
