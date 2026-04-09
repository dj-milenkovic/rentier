# Data Model: Importers Settings Form Reset

**Feature**: 023-importers-settings-form-reset  
**Date**: 2025-07-15

## Entities (No Changes)

This feature does not modify any Domain entities, Application DTOs, or database schema. All changes are confined to the ViewModel layer (form state management).

The relevant existing entities are documented here for reference.

### Importer Entity (Domain)

**File**: `src/Rentier.Domain/Entities/Importer.cs`

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `Guid` | Primary key, generated on `Create()` |
| `DisplayName` | `string` | Required, max 200 chars, trimmed |
| `ReportType` | `ReportType` (enum) | Default: `IbkrCsv` |
| `TaxpayerProfileId` | `Guid?` | FK to TaxpayerProfile (nullable) |
| `MailboxId` | `Guid?` | FK to Mailbox (nullable) |
| `FromFilter` | `string` | Default: `string.Empty` |
| `SubjectFilter` | `string` | Default: `string.Empty` |
| `AttachmentRegex` | `string` | Default: `string.Empty`, validated as regex |
| `PaymentNotes` | `string` | Default: `string.Empty`, max 4000 chars |

### ImporterDto (Application)

**File**: `src/Rentier.Application/DTOs/ImporterDto.cs`

```csharp
public sealed record ImporterDto(
    Guid Id,
    string DisplayName,
    ReportType ReportType,
    Guid? TaxpayerProfileId,
    Guid? MailboxId,
    string FromFilter,
    string SubjectFilter,
    string AttachmentRegex,
    string PaymentNotes);
```

## Form Field Inventory

The 8 editable fields on `ImporterSettingsViewModel` that MUST be covered by populate and clear logic:

| # | ViewModel Property | Type | DTO Source Field | Clear/Default Value | UI Control |
|---|-------------------|------|-----------------|--------------------|-----------| 
| 1 | `DisplayName` | `string` | `ImporterDto.DisplayName` | `string.Empty` | `TextBox` |
| 2 | `ReportType` | `ReportType` | `ImporterDto.ReportType` | `ReportType.IbkrCsv` | `ComboBox` |
| 3 | `SelectedProfile` | `TaxpayerProfileDto?` | `ImporterDto.TaxpayerProfileId` (resolved via `AvailableProfiles`) | `null` | `ComboBox` |
| 4 | `SelectedMailbox` | `MailboxDto?` | `ImporterDto.MailboxId` (resolved via `AvailableMailboxes`) | `null` | `ComboBox` |
| 5 | `FromFilter` | `string` | `ImporterDto.FromFilter` | `string.Empty` | `TextBox` |
| 6 | `SubjectFilter` | `string` | `ImporterDto.SubjectFilter` | `string.Empty` | `TextBox` |
| 7 | `AttachmentRegex` | `string` | `ImporterDto.AttachmentRegex` | `string.Empty` | `TextBox` |
| 8 | `PaymentNotes` | `string` | `ImporterDto.PaymentNotes` | `string.Empty` | `TextBox` (multiline) |

**Note**: Fields 3 and 4 require lookup resolution — the DTO stores a `Guid?` ID, but the ViewModel binds to the full DTO object resolved from `AvailableProfiles` / `AvailableMailboxes` collections.

## Form State Transitions

```text
                    ┌──────────────────────────────────────────────┐
                    │                  EMPTY                        │
                    │  All fields = default/empty                   │
                    │  IsEditMode = false                           │
                    │  SelectedImporter = null                      │
                    └──────┬────────────────────────┬───────────────┘
                           │                        │
                   [Select importer]        [AddNewCommand]
                           │                        │
                           ▼                        │
                    ┌──────────────────────────────────────────────┐
                    │               POPULATED                      │
                    │  All fields = DTO values                     │
                    │  IsEditMode = true                            │
                    │  SelectedImporter = item                      │
                    └──────┬─────┬──────────────┬───────────────────┘
                           │     │              │
                   [User edits] │       [Select different]
                           │     │              │
                           ▼     │              ▼
                    ┌─────────────┐     ┌──────────────────────┐
                    │   DIRTY     │     │  POPULATED (new DTO) │
                    │  Fields ≠   │     │  Fields = new DTO    │
                    │  persisted  │     │  IsEditMode = true   │
                    └──────┬──────┘     └──────────────────────┘
                           │
                    [Save success]
                           │
                           ▼
                    ┌──────────────────────────────────────────────┐
                    │          POPULATED (refreshed DTO)           │
                    │  All fields = persisted DTO values           │◄── THIS IS THE BUG FIX
                    │  IsEditMode = true                            │
                    │  SelectedImporter = re-selected item          │
                    └──────────────────────────────────────────────┘
```

### State Transition Table

| From State | Trigger | To State | Action |
|-----------|---------|----------|--------|
| EMPTY | Select importer | POPULATED | `PopulateFormFromDto(item.Dto)`, `IsEditMode = true` |
| POPULATED | Select different importer | POPULATED (new) | `PopulateFormFromDto(newItem.Dto)`, `IsEditMode = true` |
| POPULATED/DIRTY | Deselect (null) | EMPTY | `ClearForm()`, `IsEditMode = false` |
| POPULATED/DIRTY | AddNewCommand | EMPTY | `ClearForm()`, `IsEditMode = false` |
| DIRTY | Save success (item found) | POPULATED (refreshed) | `PopulateFormFromDto(refreshedItem.Dto)`, `IsEditMode = true` |
| DIRTY | Save success (item vanished) | EMPTY | `ClearForm()`, `IsEditMode = false` |
| DIRTY | Save failure | DIRTY (unchanged) | Retain user edits, show error |
| POPULATED | Delete success | EMPTY | `OnAddNew()` → `ClearForm()` |

## New Methods (ViewModel)

### `PopulateFormFromDto(ImporterDto dto)`

```
Input: ImporterDto (non-null)
Effect: Sets all 8 form fields from DTO values
  - DisplayName ← dto.DisplayName
  - ReportType ← dto.ReportType
  - SelectedProfile ← AvailableProfiles.FirstOrDefault(p => p.Id == dto.TaxpayerProfileId)
  - SelectedMailbox ← AvailableMailboxes.FirstOrDefault(m => m.Id == dto.MailboxId)
  - FromFilter ← dto.FromFilter
  - SubjectFilter ← dto.SubjectFilter
  - AttachmentRegex ← dto.AttachmentRegex
  - PaymentNotes ← dto.PaymentNotes
```

### `ClearForm()`

```
Input: None
Effect: Resets all 8 form fields + messages to default
  - DisplayName ← string.Empty
  - ReportType ← ReportType.IbkrCsv
  - SelectedProfile ← null
  - SelectedMailbox ← null
  - FromFilter ← string.Empty
  - SubjectFilter ← string.Empty
  - AttachmentRegex ← string.Empty
  - PaymentNotes ← string.Empty
  - ErrorMessage ← null
  - SuccessMessage ← null
```

## Validation Rules (No Changes)

All validation is handled by the existing command handlers:
- `DisplayName`: Required, max 200 chars (Domain entity `Create`/`UpdateDetails`)
- `AttachmentRegex`: Valid regex pattern (Application handler)
- `PaymentNotes`: Max 4000 chars (Domain entity `UpdateDetails`)

No validation logic is being added or changed in the ViewModel.
