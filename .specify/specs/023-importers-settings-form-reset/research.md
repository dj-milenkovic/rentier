# Research: Importers Settings Form Reset on Save & Navigation

**Feature**: 023-importers-settings-form-reset  
**Date**: 2025-07-15

## Research Task 1: Root Cause Analysis — Stale Form Fields After Save

### Decision
The root cause is that the post-save re-selection path in `OnSaveAsync` writes to the backing field `_selectedImporter` directly, bypassing the `SelectedImporter` property setter that contains the form population logic.

### Rationale

**Current code (edit-mode save, lines 227-231):**
```csharp
var savedId = SelectedImporter.Id;
await ReloadImportersAsync(ct);
_selectedImporter = ImporterItems.FirstOrDefault(i => i.Id == savedId);  // backing field!
this.RaisePropertyChanged(nameof(SelectedImporter));  // notifies UI binding
SuccessMessage = Strings.Importers_Saved_Confirmation;
```

The issue: `_selectedImporter = ...` sets the backing field without executing the property setter. The setter (lines 98-109) is where form field population lives:
```csharp
set
{
    this.RaiseAndSetIfChanged(ref _selectedImporter, value);
    if (value != null)
    {
        DisplayName = value.Dto.DisplayName;
        ReportType = value.Dto.ReportType;
        // ... 6 more fields
    }
}
```

Because the setter is bypassed, the form fields retain whatever the user typed before saving — not the freshly persisted DTO values. `RaisePropertyChanged` only notifies the Avalonia `ListBox` binding that `SelectedImporter` changed (so the list selection highlights correctly), but the form fields are never repopulated.

The same pattern exists for add-mode save (lines 253-258).

### Alternatives Considered

1. **Use property setter instead of backing field**: Could write `SelectedImporter = ImporterItems.FirstOrDefault(...)` instead of `_selectedImporter = ...`. This would trigger the setter and form population. However, this relies on `RaiseAndSetIfChanged` not short-circuiting when the reference changes (it won't because items are new objects after reload). This approach works but couples the save logic to the setter behavior.

2. **Extract populate/clear methods and call explicitly** (CHOSEN): Extract `PopulateFormFromDto(ImporterDto)` and `ClearForm()` methods. Call them both in the setter and explicitly after save. This is more explicit, testable, and resilient to future changes in the setter.

---

## Research Task 2: Root Cause Analysis — Form Not Cleared on Deselect

### Decision
The `SelectedImporter` property setter has no `else` clause for when `value` is `null`. When the selection becomes null (deselect), form fields retain the previously selected importer's data.

### Rationale

**Current setter:**
```csharp
set
{
    this.RaiseAndSetIfChanged(ref _selectedImporter, value);
    if (value != null)
    {
        // populate fields from DTO
    }
    // NO else clause — form not cleared on null
}
```

The `OnAddNew()` method does clear the form, but it's only called from `AddNewCommand` and after delete. If `SelectedImporter` becomes null via any other path (programmatic deselect, item removed from list, ListBox two-way binding writing back null), the form shows orphaned data.

### Alternatives Considered

1. **Add else clause to setter with inline clearing**: Quick fix but duplicates the clearing logic that already exists in `OnAddNew()`.
2. **Extract `ClearForm()` and call from both setter and `OnAddNew()`** (CHOSEN): Eliminates duplication and ensures all deselect paths clear the form.

---

## Research Task 3: ReactiveUI Best Practices for Form State Management

### Decision
Use extracted private methods for form state transitions. Keep `RaiseAndSetIfChanged` in the property setter but add explicit populate/clear calls after the base setter.

### Rationale

ReactiveUI `RaiseAndSetIfChanged` is designed for property change notification optimization — it only fires if the value actually changed (reference equality for objects). In our case, after `ReloadImportersAsync`, the `ImporterItems` collection is rebuilt from fresh DTOs, so the item references are always new objects. This means `RaiseAndSetIfChanged` will always fire.

However, the recommended pattern for dependent state changes (form population from a selection) is to use `WhenAnyValue` observables or explicit method calls rather than overloading the property setter. Since the existing codebase uses setter-based population, the lowest-risk fix is to extract the population logic into a method and call it from both the setter and the save path.

### Alternatives Considered

1. **Use `this.WhenAnyValue(x => x.SelectedImporter).Subscribe(...)`**: More "reactive" but would require restructuring the existing ViewModel pattern. Higher risk for a bugfix.
2. **Keep setter-based approach with extracted methods** (CHOSEN): Minimal structural change, explicit, easy to test.

---

## Research Task 4: Post-Save Edge Case — Item Vanished from List

### Decision
After save and reload, if the saved item is not found in the refreshed list (e.g., concurrent deletion by another process), the form must clear and deselect.

### Rationale

Current code: `_selectedImporter = ImporterItems.FirstOrDefault(i => i.Id == savedId)`. If the item is gone, `FirstOrDefault` returns `null`. The backing field becomes `null`, `RaisePropertyChanged` fires, and the ListBox shows no selection — but the form retains stale data (because the setter's populate logic is bypassed, and there's no null handling).

The fix: After the `FirstOrDefault` lookup, check if the result is null. If null, call `ClearForm()` and set `IsEditMode = false`. If non-null, call `PopulateFormFromDto(item.Dto)`.

### Alternatives Considered
None — this is the straightforward correct behavior per FR-007 in the spec.

---

## Research Task 5: Test Strategy for Form Reset

### Decision
Add three focused test methods to `ImporterSettingsViewModelTests.cs`, each asserting all 8 editable fields.

### Rationale

The existing test `SelectImporter_PopulatesFormFields` checks 6 of 8 fields (misses `SelectedProfile` and `SelectedMailbox` detailed assertions because the test setup doesn't populate `AvailableProfiles`/`AvailableMailboxes`). The new tests should:

1. **`SaveCommand_EditMode_RepopulatesFormFieldsFromRefreshedDto`**: Save an importer, mock the reload to return a DTO with different (server-normalized) values, assert all 8 fields match the refreshed DTO.
2. **`SelectDifferentImporter_PopulatesAllFieldsFromNewImporter`**: Select Importer A, edit some fields, select Importer B, assert all 8 fields match Importer B's DTO with zero bleed from A.
3. **`DeselectImporter_ClearsAllFields`**: Select an importer (all fields populated), set `SelectedImporter = null`, assert all 8 fields are empty/default.
4. **`SaveCommand_EditMode_ItemVanishedAfterReload_ClearsForm`**: Save, reload returns empty list, assert form cleared and `IsEditMode` is false.

### Alternatives Considered
None — these tests directly map to the spec's acceptance scenarios (FR-001 through FR-008).

---

## Summary of Design Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | Extract `PopulateFormFromDto(ImporterDto dto)` private method | Single source of truth for DTO→form field mapping; eliminates setter-bypass bug |
| 2 | Extract `ClearForm()` private method | Single source of truth for clearing; eliminates missing-else-clause bug |
| 3 | Call `PopulateFormFromDto` in setter (value != null) and after save re-selection | Covers both navigation and post-save paths |
| 4 | Call `ClearForm()` in setter (value == null), in `OnAddNew()`, and when saved item vanished | Covers all deselect paths |
| 5 | Keep `_selectedImporter` direct writes in save paths | Avoids re-triggering setter side effects; populate/clear called explicitly |
| 6 | Add 4 new test methods covering all 8 fields | Maps 1:1 to spec acceptance scenarios; prevents regression |
