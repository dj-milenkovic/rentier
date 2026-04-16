# Quickstart: Header Checkbox for Select All / Clear All

**Feature**: 028-header-checkbox-select-all
**Branch**: `feat/027-031-ux-improvements`

## What This Feature Does

Replaces the standalone "Select All" and "Clear Selection" toolbar buttons on the Filings and Reports pages with a single tri-state checkbox in the DataGrid header. The checkbox reflects the current selection state (none / some / all) and allows one-click select-all or clear-all.

## Prerequisites

- .NET 8 SDK
- Feature branch: `feat/027-031-ux-improvements`
- No database migrations or new NuGet packages required

## Files to Modify

### ViewModels (2 files)

1. **`src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`**
   - Add `bool? IsAllSelected` property with getter (computed from `SelectedCount` and `Rows.Count`) and setter (dispatches `SelectAllCommand` or `ClearSelectionCommand`)
   - Add `_isUpdatingSelection` guard field to prevent re-entrant updates
   - Wire `IsAllSelected` into the reactive chain in `RebuildRowSubscriptions()`

2. **`src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`**
   - Same changes as FilingsViewModel (identical `IsAllSelected` pattern)

### Views (2 files)

3. **`src/Rentier.Desktop/Views/FilingsView.axaml`**
   - Add `DataGridTemplateColumn.Header` with tri-state `CheckBox` to the selection column
   - Remove "Select All" and "Clear Selection" buttons from toolbar
   - Keep "Delete Selected (N)" button

4. **`src/Rentier.Desktop/Views/ReportsView.axaml`**
   - Same changes as FilingsView

### Tests (2 files)

5. **`tests/Rentier.UnitTests/Desktop/FilingsViewModelBulkDeleteTests.cs`**
   - Add tests for `IsAllSelected` tri-state computation and setter behavior

6. **`tests/Rentier.UnitTests/Desktop/ReportsViewModelBulkDeleteTests.cs`**
   - Add tests for `IsAllSelected` tri-state computation and setter behavior

## Key Implementation Pattern

```csharp
// In ViewModel — computed tri-state property
private bool _isUpdatingSelection;

public bool? IsAllSelected
{
    get
    {
        if (Rows.Count == 0) return false;
        if (SelectedCount == 0) return false;
        if (SelectedCount == Rows.Count) return true;
        return null; // indeterminate
    }
    set
    {
        if (_isUpdatingSelection) return;
        _isUpdatingSelection = true;
        try
        {
            if (value == true)
                SelectAllCommand.Execute().Subscribe();
            else if (value == false)
                ClearSelectionCommand.Execute().Subscribe();
            // null → ignore; reactive pipeline recomputes
        }
        finally
        {
            _isUpdatingSelection = false;
            this.RaisePropertyChanged(nameof(IsAllSelected));
        }
    }
}

// In RebuildRowSubscriptions — add notification
row.WhenAnyValue(r => r.IsSelected)
    .Subscribe(_ =>
    {
        SelectedCount = Rows.Count(r => r.IsSelected);
        this.RaisePropertyChanged(nameof(IsAllSelected));
    });
```

```xml
<!-- In View — header checkbox -->
<DataGridTemplateColumn Width="40">
  <DataGridTemplateColumn.Header>
    <CheckBox IsThreeState="True"
              IsChecked="{Binding DataContext.IsAllSelected,
                          RelativeSource={RelativeSource AncestorType=DataGrid},
                          Mode=TwoWay}"
              IsEnabled="{Binding DataContext.HasItems,
                          RelativeSource={RelativeSource AncestorType=DataGrid}}"
              HorizontalAlignment="Center" VerticalAlignment="Center" />
  </DataGridTemplateColumn.Header>
  <DataGridTemplateColumn.CellTemplate>
    <DataTemplate>
      <CheckBox IsChecked="{Binding IsSelected, Mode=TwoWay}"
                HorizontalAlignment="Center" VerticalAlignment="Center" />
    </DataTemplate>
  </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

## Build & Test

```bash
# Build
dotnet build Rentier.slnx

# Run tests
dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~BulkDelete"
```

## Verification

1. Open Filings page with rows → header checkbox should be unchecked
2. Click header checkbox → all rows selected, checkbox fully checked
3. Click header checkbox again → all rows deselected, checkbox unchecked
4. Select 2 of 5 rows manually → header shows indeterminate (dash)
5. Click header while indeterminate → all rows selected
6. Empty page → header checkbox visible but disabled
7. Toolbar should show only "Delete Selected (N)" — no "Select All" or "Clear Selection" buttons
