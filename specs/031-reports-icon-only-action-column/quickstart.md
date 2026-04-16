# Quickstart: Reports — Icon-Only Action Column

**Feature**: 031-reports-icon-only-action-column  
**Date**: 2025-07-17  
**Prerequisite**: Feature 030 (Filings Action Column Consolidation) must be merged first.

## Prerequisites

Before starting implementation, verify that feature 030 has been completed and the following artifacts exist:

- [ ] `src/Rentier.Desktop/Resources/Icons.axaml` exists and contains `TrashIcon` StreamGeometry
- [ ] `src/Rentier.Desktop/App.axaml` includes `Icons.axaml` via `<ResourceInclude>`
- [ ] The icon-button pattern (PathIcon inside chromeless Button) is working on the Filings page

If feature 030 is not yet merged, branch from `feat/027-031-ux-improvements` to access its changes.

## Step-by-Step Implementation

### Step 1: Add ViewFilingsIcon to Icons.axaml

Open `src/Rentier.Desktop/Resources/Icons.axaml` and add the ViewFilings icon geometry:

```xml
<StreamGeometry x:Key="ViewFilingsIcon">M6 2 L14 8 L6 14</StreamGeometry>
```

Place it alongside the existing icons from feature 030. Ensure the path data produces a right-pointing chevron that visually matches the weight and style of the existing icons.

### Step 2: Add Tooltip Strings to Strings.resx

Open `src/Rentier.Desktop/Resources/Strings.resx` and add two new entries:

| Name | Value |
|---|---|
| `Reports_Tooltip_ViewFilings` | `View linked filings` |
| `Reports_Tooltip_Delete` | `Delete report` |

Place them in the Reports section (after the existing `Reports_Button_*` entries). Rebuild or run the ResX generator to update `Strings.Designer.cs`.

### Step 3: Replace Action Buttons in ReportsView.axaml

Open `src/Rentier.Desktop/Views/ReportsView.axaml`. Locate the action column (lines 111–124) and replace the two text buttons with icon buttons:

**Replace this:**
```xml
<StackPanel Orientation="Horizontal" Spacing="4" Margin="4,2">
  <Button Content="{x:Static res:Strings.Reports_Button_ViewFilings}"
          Command="{Binding DataContext.ViewFilingsCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
          CommandParameter="{Binding Id}" />
  <Button Content="{x:Static res:Strings.Reports_Button_Delete}"
          Command="{Binding DataContext.DeleteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
          CommandParameter="{Binding Id}" />
</StackPanel>
```

**With this:**
```xml
<StackPanel Orientation="Horizontal" Spacing="4" Margin="4,2">
  <Button ToolTip.Tip="{x:Static res:Strings.Reports_Tooltip_ViewFilings}"
          Command="{Binding DataContext.ViewFilingsCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
          CommandParameter="{Binding Id}"
          Padding="4" Background="Transparent" BorderThickness="0">
    <PathIcon Data="{StaticResource ViewFilingsIcon}" Width="16" Height="16" />
  </Button>
  <Button ToolTip.Tip="{x:Static res:Strings.Reports_Tooltip_Delete}"
          Command="{Binding DataContext.DeleteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
          CommandParameter="{Binding Id}"
          Foreground="Red"
          Padding="4" Background="Transparent" BorderThickness="0">
    <PathIcon Data="{StaticResource TrashIcon}" Width="16" Height="16" />
  </Button>
</StackPanel>
```

### Step 4: Update Headless UI Tests

Open `tests/Rentier.UnitTests/Desktop/Views/ReportsViewHeadlessTests.cs`. Add test(s) to verify:

1. **Icon buttons render**: When reports are loaded, the action column contains `PathIcon` controls (not text-labelled buttons).
2. **Tooltip binding**: The icon buttons have `ToolTip.Tip` values matching the expected tooltip strings.
3. **Destructive styling**: The delete button has `Foreground="Red"`.

Example test outline:
```csharp
[AvaloniaFact]
public void ReportsView_ActionColumn_RendersIconButtonsWithTooltips()
{
    // Arrange — load reports into a rendered view
    // Act — find Button controls in the DataGrid action column
    // Assert — verify PathIcon content, ToolTip.Tip text, Foreground colour
}
```

### Step 5: Visual Verification

1. Build and run the app: `dotnet build src/Rentier.Desktop`
2. Navigate to the Reports page with at least one report present
3. Verify:
   - [ ] Two icon-only buttons appear per row (no text labels visible)
   - [ ] Hovering over the left icon shows "View linked filings" tooltip
   - [ ] Hovering over the right icon shows "Delete report" tooltip
   - [ ] The delete icon renders in red
   - [ ] The view-filings icon renders in the default (non-red) foreground
   - [ ] The action column is visibly narrower than before
   - [ ] Clicking the view-filings icon navigates to the Filings page
   - [ ] Clicking the delete icon triggers the delete confirmation dialog
   - [ ] Icon style matches the Filings page (feature 030) visually

### Step 6: Run Tests

```bash
dotnet test tests/Rentier.UnitTests --filter "Category=UI"
dotnet test tests/Rentier.UnitTests
```

Verify all existing tests pass and the new icon-button tests pass.

## Files Changed Summary

| File | Change Type | Description |
|---|---|---|
| `src/Rentier.Desktop/Resources/Icons.axaml` | Modify | Add `ViewFilingsIcon` StreamGeometry |
| `src/Rentier.Desktop/Resources/Strings.resx` | Modify | Add `Reports_Tooltip_ViewFilings`, `Reports_Tooltip_Delete` |
| `src/Rentier.Desktop/Resources/Strings.Designer.cs` | Auto-generated | Updated by ResX generator |
| `src/Rentier.Desktop/Views/ReportsView.axaml` | Modify | Replace text buttons with icon buttons |
| `tests/Rentier.UnitTests/Desktop/Views/ReportsViewHeadlessTests.cs` | Modify | Add icon-button rendering tests |

## What NOT to Change

- **ReportsViewModel.cs** — Commands are unchanged
- **ReportRowViewModel.cs** — Row data is unchanged
- **ReportsView.axaml.cs** — Code-behind is already clean
- **App.axaml** — Icons.axaml already included by feature 030
- **Any Domain/Application/Infrastructure code** — Zero backend impact
