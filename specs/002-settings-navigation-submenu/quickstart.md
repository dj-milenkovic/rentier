# Quickstart: Settings Navigation Sub-menu Items in Sidebar

**Feature**: 036-settings-navigation-submenu  
**Date**: 2025-07-15

## Prerequisites

- .NET 10.0 SDK installed
- Repository cloned and on feature branch `036-settings-navigation-submenu`
- IDE with Avalonia tooling (Rider, VS with Avalonia extension, or VS Code)

## Build & Run

```powershell
# From repository root
dotnet build Rentier.slnx
dotnet run --project src/Rentier.Desktop
```

## Run Tests

```powershell
# All tests
dotnet test Rentier.slnx

# Desktop ViewModel tests only (fastest feedback loop)
dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~MainWindowViewModel"
```

## Implementation Order

This feature is a pure Desktop-layer refactor. Follow this order:

### Step 1: Extend NavigationEntry Model
**File**: `src/Rentier.Desktop/ViewModels/NavigationEntry.cs`

Add `IsGroup`, `IsExpanded` (reactive), `Children`, `ParentGroup`, `IndentLevel` properties. The existing `ViewModel` property becomes nullable (null for group headers). All existing tests should still compile after this change since new fields have sensible defaults.

### Step 2: Add Resource Strings and Icons
**Files**: `src/Rentier.Desktop/Resources/Strings.resx`, `src/Rentier.Desktop/Assets/Icons.axaml`

Add `Nav_Settings_Profile`, `Nav_Settings_Holidays`, `Nav_Settings_Mailboxes`, `Nav_Settings_Importers`, `Nav_Settings_Language` resource keys. Add Lucide icon `StreamGeometry` entries for child items and the chevron toggle.

### Step 3: Update MainWindowViewModel Navigation Structure
**File**: `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs`

Replace the single `SettingsViewModel` entry with a group entry + 5 child entries. Wire the `IsExpanded` toggle to add/remove children from the flattened `NavigationEntries` collection. Update `UpdateNavigationLabels()` to cover child entries.

### Step 4: Update DI Registrations
**File**: `src/Rentier.Desktop/Composition/CompositionRoot.cs`

Remove `SettingsViewModel` registration. Change settings sub-ViewModel lifetimes from `Transient` to `Singleton`.

### Step 5: Update Sidebar AXAML
**File**: `src/Rentier.Desktop/Views/MainWindow.axaml`

Update the `ListBox.ItemTemplate` to handle group headers (chevron icon, non-selectable, click toggles expand) and child items (indented, smaller icon). Add sidebar styles for group/child visual differentiation.

### Step 6: Update Sidebar Styles
**File**: `src/Rentier.Desktop/Assets/Styles/Controls.axaml`

Add styles for indented child items and group header appearance (font weight, chevron positioning).

### Step 7: Delete Tab Container
**Files**: Delete `SettingsView.axaml`, `SettingsView.axaml.cs`, `SettingsViewModel.cs`

### Step 8: Update Tests
**File**: `tests/Rentier.UnitTests/Desktop/MainWindowViewModelTests.cs`

Update `BuildSettingsVm()` helper (now builds individual VMs, not the aggregate). Add tests for:
- Navigation entry count (10 when expanded, 5 when collapsed)
- Group toggle behavior
- Child selection → correct CurrentViewModel
- Active indicator moves to child items
- Localization label updates for child entries

## Verification Checklist

- [ ] App launches with Settings group expanded showing 5 child items
- [ ] Clicking each child item navigates to the correct standalone settings view
- [ ] Clicking Settings group header collapses/expands child items
- [ ] Active pipe indicator shows on the selected child item
- [ ] No other top-level items show active when a settings child is active
- [ ] Switching to Dashboard and back to a settings child preserves state
- [ ] Top-level navigation items (Dashboard, Filings, Reports, Sync) work unchanged
- [ ] All existing tests pass after updates
- [ ] New group navigation tests pass
- [ ] No compiler warnings (`TreatWarningsAsErrors=true`)
