# UI Contracts: Bulk Delete Selection and Toolbar

**Feature**: 025-bulk-delete-fillings-reports  
**Layer**: Desktop (Rentier.Desktop)

## Row ViewModel Contract

Both `FilingRowViewModel` and `ReportRowViewModel` gain an `IsSelected` property:

```csharp
// Addition to existing row ViewModels
public bool IsSelected
{
    get => _isSelected;
    set => this.RaiseAndSetIfChanged(ref _isSelected, value);
}
private bool _isSelected;
```

**Binding**: `{Binding IsSelected, Mode=TwoWay}` on checkbox in DataGrid template column.

---

## Parent ViewModel Contract

### New Observable Properties (both FilingsViewModel and ReportsViewModel)

| Property | Type | Source | UI Binding |
|----------|------|--------|------------|
| `SelectedCount` | `int` | Reactive aggregation of `Rows.Where(r => r.IsSelected).Count()` | `TextBlock` in toolbar |
| `HasSelection` | `bool` | `SelectedCount > 0` | `IsVisible` on "Delete Selected" button |
| `DeleteSelectedLabel` | `string` | `string.Format(Strings.BulkDelete_Button_Template, SelectedCount)` | `Content` on "Delete Selected" button |

### New Commands (both ViewModels)

| Command | Signature | CanExecute | Behaviour |
|---------|-----------|------------|-----------|
| `SelectAllCommand` | `ReactiveCommand<Unit, Unit>` | `HasItems` observable | Sets `IsSelected = true` on all `Rows` |
| `ClearSelectionCommand` | `ReactiveCommand<Unit, Unit>` | `HasSelection` observable | Sets `IsSelected = false` on all `Rows` |
| `BulkDeleteCommand` | `ReactiveCommand<Unit, Unit>` | `HasSelection` observable | Shows confirmation → dispatches command → clears selection → reloads |

### Command Execution Flow: BulkDeleteCommand

```text
1. Collect selected IDs: Rows.Where(r => r.IsSelected).Select(r => r.Id).ToList()
2. Show confirmation dialog via injected Func<string, string, Task<bool>>
   - Filings: title=BulkDelete_Filings_Confirmation_Title, msg=format(BulkDelete_Filings_Confirmation_Message, count)
   - Reports: title=BulkDelete_Reports_Confirmation_Title, msg=format(BulkDelete_Reports_Confirmation_Message, count)
3. If cancelled → return (selection unchanged)
4. Set IsLoading = true
5. Dispatch command: _bulkDeleteHandler.HandleAsync(command, ct)
6. If error → set ErrorMessage
7. Handle page adjustment (Filings only: if all current page items deleted on non-first page, decrement)
8. Reload list (LoadPageAsync / LoadReportsAsync)
9. Selection is automatically cleared because reload replaces Rows collection
10. Set IsLoading = false
```

---

## View Contract (AXAML)

### Checkbox Column (first column in both DataGrids)

```xml
<DataGridTemplateColumn Width="40">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <CheckBox IsChecked="{Binding IsSelected, Mode=TwoWay}"
                      HorizontalAlignment="Center" />
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

### Toolbar Buttons (added to existing toolbar StackPanel)

```xml
<!-- Bulk selection toolbar — visible when HasItems -->
<Button Content="{x:Static res:Strings.BulkDelete_SelectAll_Button}"
        Command="{Binding SelectAllCommand}"
        IsVisible="{Binding HasItems}" />

<Button Content="{x:Static res:Strings.BulkDelete_ClearSelection_Button}"
        Command="{Binding ClearSelectionCommand}"
        IsVisible="{Binding HasItems}" />

<Button Content="{Binding DeleteSelectedLabel}"
        Command="{Binding BulkDeleteCommand}"
        IsVisible="{Binding HasSelection}"
        Foreground="Red" />
```

### Destructive Button Style

The "Delete Selected (N)" button uses `Foreground="Red"` per spec FR-007. No new style resource is needed — the existing application styling conventions support inline foreground overrides.

---

## Confirmation Dialog Contract

### Filings Bulk Delete Dialog

| Element | Value Source |
|---------|-------------|
| Title | `Strings.BulkDelete_Filings_Confirmation_Title` |
| Message | `string.Format(Strings.BulkDelete_Filings_Confirmation_Message, count)` |
| Confirm | `Strings.BulkDelete_Confirm_Button` |
| Cancel | `Strings.BulkDelete_Cancel_Button` |

### Reports Bulk Delete Dialog

| Element | Value Source |
|---------|-------------|
| Title | `Strings.BulkDelete_Reports_Confirmation_Title` |
| Message | `string.Format(Strings.BulkDelete_Reports_Confirmation_Message, count)` |
| Confirm | `Strings.BulkDelete_Confirm_Button` |
| Cancel | `Strings.BulkDelete_Cancel_Button` |

Both dialogs use `ConfirmDialogHelper.ShowAsync(title, message, confirmText, cancelText)` — no new dialog infrastructure needed.

---

## Selection Lifecycle Contract

| Event | Behaviour |
|-------|-----------|
| Page load / navigation enter | Selection cleared (fresh `Rows` collection) |
| Page reload after delete | Selection cleared (fresh `Rows` collection) |
| Navigate away | Selection lost (ViewModel disposed or reactivated) |
| Select All | All items in `Rows` set `IsSelected = true` |
| Clear Selection | All items in `Rows` set `IsSelected = false` |
| Individual checkbox toggle | Single row `IsSelected` changes, `SelectedCount` reactively updates |
| Filter/sort change (Filings) | Triggers reload → selection cleared |
