# Quick Start: Importers Settings Form Reset

**Feature**: 023-importers-settings-form-reset  
**Date**: 2025-07-15

## What This Feature Does

Fixes the `ImporterSettingsViewModel` so that all 8 editable form fields are always consistent with the selected importer. After saving, switching importers, or deselecting, the form reflects the correct state — no stale values.

## Files to Change

| File | Change Type | Description |
|------|-------------|-------------|
| `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs` | Modify | Extract `PopulateFormFromDto` + `ClearForm`, fix save + setter paths |
| `tests/Rentier.Desktop.Tests/ImporterSettingsViewModelTests.cs` | Modify | Add 4 new test methods covering all reset scenarios |

**Total**: 2 files modified, 0 files created, 0 files deleted.

## Step-by-Step Implementation

### Step 1: Extract `PopulateFormFromDto` method

Add a new private method to `ImporterSettingsViewModel`:

```csharp
private void PopulateFormFromDto(ImporterDto dto)
{
    DisplayName = dto.DisplayName;
    ReportType = dto.ReportType;
    SelectedProfile = AvailableProfiles.FirstOrDefault(p => p.Id == dto.TaxpayerProfileId);
    SelectedMailbox = AvailableMailboxes.FirstOrDefault(m => m.Id == dto.MailboxId);
    FromFilter = dto.FromFilter;
    SubjectFilter = dto.SubjectFilter;
    AttachmentRegex = dto.AttachmentRegex;
    PaymentNotes = dto.PaymentNotes;
}
```

### Step 2: Extract `ClearForm` method

Add a new private method:

```csharp
private void ClearForm()
{
    DisplayName = string.Empty;
    ReportType = ReportType.IbkrCsv;
    SelectedProfile = null;
    SelectedMailbox = null;
    FromFilter = string.Empty;
    SubjectFilter = string.Empty;
    AttachmentRegex = string.Empty;
    PaymentNotes = string.Empty;
    ErrorMessage = null;
    SuccessMessage = null;
}
```

### Step 3: Update `SelectedImporter` setter

Replace the inline population with method calls and add deselect handling:

```csharp
public ImporterItemViewModel? SelectedImporter
{
    get => _selectedImporter;
    set
    {
        this.RaiseAndSetIfChanged(ref _selectedImporter, value);
        if (value != null)
        {
            PopulateFormFromDto(value.Dto);
            IsEditMode = true;
        }
        else
        {
            ClearForm();
            IsEditMode = false;
        }
    }
}
```

### Step 4: Update `OnAddNew` to use `ClearForm`

```csharp
private void OnAddNew()
{
    _selectedImporter = null;
    this.RaisePropertyChanged(nameof(SelectedImporter));
    ClearForm();
    IsEditMode = false;
}
```

### Step 5: Update `OnSaveAsync` edit-mode path

After reload, repopulate from the fresh DTO:

```csharp
// Edit-mode save (inside OnSaveAsync, success branch)
var savedId = SelectedImporter.Id;
await ReloadImportersAsync(ct);
_selectedImporter = ImporterItems.FirstOrDefault(i => i.Id == savedId);
this.RaisePropertyChanged(nameof(SelectedImporter));
if (_selectedImporter != null)
{
    PopulateFormFromDto(_selectedImporter.Dto);
    SuccessMessage = Strings.Importers_Saved_Confirmation;
}
else
{
    // Item vanished after save (concurrent deletion)
    ClearForm();
    IsEditMode = false;
}
```

### Step 6: Update `OnSaveAsync` add-mode path

Same pattern for new importer creation:

```csharp
// Add-mode save (inside OnSaveAsync, success branch)
var newId = result.Value;
await ReloadImportersAsync(ct);
_selectedImporter = ImporterItems.FirstOrDefault(i => i.Id == newId);
this.RaisePropertyChanged(nameof(SelectedImporter));
if (_selectedImporter != null)
{
    PopulateFormFromDto(_selectedImporter.Dto);
    IsEditMode = true;
    SuccessMessage = Strings.Importers_Saved_Confirmation;
}
else
{
    ClearForm();
    IsEditMode = false;
}
```

### Step 7: Write tests

Add to `ImporterSettingsViewModelTests.cs`:

1. **`SaveCommand_EditMode_RepopulatesFormFromRefreshedDto`** — Save with edited values, reload returns a DTO with server-normalized values (e.g., trimmed DisplayName), assert all 8 fields match the refreshed DTO.

2. **`SelectDifferentImporter_PopulatesAllFieldsFromNewSelection`** — Select A, edit some fields, select B, assert all 8 fields match B.

3. **`DeselectImporter_ClearsAllFormFields`** — Select, then set `SelectedImporter = null`, assert all 8 fields are empty/default.

4. **`SaveCommand_EditMode_ItemVanishedAfterReload_ClearsForm`** — Save succeeds, reload returns empty list, assert form cleared and `IsEditMode` false.

## Build & Test

```bash
# Build
dotnet build Rentier.slnx

# Run affected tests
dotnet test tests/Rentier.Desktop.Tests/Rentier.Desktop.Tests.csproj --filter "ImporterSettingsViewModel"

# Run full test suite
dotnet test Rentier.slnx
```

## Verification Checklist

- [ ] After edit save: all 8 fields show persisted (refreshed DTO) values
- [ ] After add save: all 8 fields show the newly created importer's values
- [ ] After switching importers: all 8 fields show the new importer's values
- [ ] After deselect: all 8 fields are empty/default
- [ ] After save when item vanished: form cleared, IsEditMode = false
- [ ] Save failure: form retains user's edits (unchanged behavior)
- [ ] All 4 new tests pass
- [ ] All existing tests still pass
- [ ] No compiler warnings
