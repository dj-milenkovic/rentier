# Data Model: Reports — Icon-Only Action Column

**Feature**: 031-reports-icon-only-action-column  
**Date**: 2025-07-17  
**Status**: Complete

## Overview

This feature has **no data model changes**. No entities, value objects, DTOs, or database schema are affected. The change is purely cosmetic — replacing text-labelled buttons with icon-only buttons in the Reports DataGrid view.

This document catalogues the **UI resource model**: the icon resources, string resources, and AXAML structure that define the icon-button presentation.

---

## Icon Resources (StreamGeometry)

Added to the shared `Resources/Icons.axaml` resource dictionary established by feature 030.

| Resource Key | Shape | Usage | Source |
|---|---|---|---|
| `TrashIcon` | Trash can / delete bin | Delete report button | **Reused from 030** — no new definition |
| `ViewFilingsIcon` | Right-chevron / arrow | View linked filings navigation button | **New** — added by 031 |

### ViewFilingsIcon Geometry

The `ViewFilingsIcon` is a right-pointing chevron (→) rendered at 16×16 logical pixels. It represents navigation to the linked filings page for a report.

```xml
<StreamGeometry x:Key="ViewFilingsIcon">M6 2 L14 8 L6 14</StreamGeometry>
```

> **Note**: The exact path data will be finalised during implementation to match the visual weight and stroke style of the icons established in feature 030. The geometry above is a representative chevron-right shape.

---

## String Resources (Strings.resx)

Two new tooltip string resources added to `src/Rentier.Desktop/Resources/Strings.resx`:

| Resource Key | Value | Used By |
|---|---|---|
| `Reports_Tooltip_ViewFilings` | `View linked filings` | ToolTip.Tip on the View Filings icon button |
| `Reports_Tooltip_Delete` | `Delete report` | ToolTip.Tip on the Delete icon button |

### Naming Convention

Follows the established `{Page}_{Category}_{Action}` pattern:
- **Page**: `Reports` (consistent with `Reports_Button_*`, `Reports_Col_*`)
- **Category**: `Tooltip` (new category, distinguishing from `Button` and `Col`)
- **Action**: `ViewFilings`, `Delete` (matching existing action names)

### Existing Resources Retained (Not Removed)

The existing button text resources remain in `Strings.resx` for potential future use (e.g., accessible screen reader labels, alternative UI modes):

| Resource Key | Value | Status |
|---|---|---|
| `Reports_Button_ViewFilings` | `View Filings` | Retained (no longer rendered as visible button text) |
| `Reports_Button_Delete` | `Delete` | Retained (no longer rendered as visible button text) |

---

## AXAML Structure: Action Column

### Current Structure (Before)

```xml
<DataGridTemplateColumn Width="Auto">
  <DataGridTemplateColumn.CellTemplate>
    <DataTemplate>
      <StackPanel Orientation="Horizontal" Spacing="4" Margin="4,2">
        <Button Content="{x:Static res:Strings.Reports_Button_ViewFilings}"
                Command="{Binding DataContext.ViewFilingsCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                CommandParameter="{Binding Id}" />
        <Button Content="{x:Static res:Strings.Reports_Button_Delete}"
                Command="{Binding DataContext.DeleteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                CommandParameter="{Binding Id}" />
      </StackPanel>
    </DataTemplate>
  </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

### Target Structure (After)

```xml
<DataGridTemplateColumn Width="Auto">
  <DataGridTemplateColumn.CellTemplate>
    <DataTemplate>
      <StackPanel Orientation="Horizontal" Spacing="4" Margin="4,2">
        <!-- View Filings: navigation icon, default foreground -->
        <Button ToolTip.Tip="{x:Static res:Strings.Reports_Tooltip_ViewFilings}"
                Command="{Binding DataContext.ViewFilingsCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                CommandParameter="{Binding Id}"
                Padding="4" Background="Transparent" BorderThickness="0">
          <PathIcon Data="{StaticResource ViewFilingsIcon}" Width="16" Height="16" />
        </Button>
        <!-- Delete: trash icon, red foreground for destructive action -->
        <Button ToolTip.Tip="{x:Static res:Strings.Reports_Tooltip_Delete}"
                Command="{Binding DataContext.DeleteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                CommandParameter="{Binding Id}"
                Foreground="Red"
                Padding="4" Background="Transparent" BorderThickness="0">
          <PathIcon Data="{StaticResource TrashIcon}" Width="16" Height="16" />
        </Button>
      </StackPanel>
    </DataTemplate>
  </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

### Key Differences

| Aspect | Before | After |
|---|---|---|
| Button content | Text label (`Content="{x:Static ...}"`) | PathIcon (`<PathIcon Data="{StaticResource ...}" />`) |
| Tooltip | None | `ToolTip.Tip="{x:Static res:Strings.Reports_Tooltip_*}"` |
| Delete foreground | Default (inherited) | `Foreground="Red"` |
| Button chrome | Default styled button | `Padding="4" Background="Transparent" BorderThickness="0"` |
| Icon size | N/A | `Width="16" Height="16"` (matching 030 convention) |
| Command binding | Unchanged | Unchanged |
| CommandParameter | Unchanged (`{Binding Id}`) | Unchanged (`{Binding Id}`) |

---

## Entity & ViewModel Impact

### No Changes Required

| Component | File | Impact |
|---|---|---|
| `ReportsViewModel` | `ViewModels/ReportsViewModel.cs` | **None** — `ViewFilingsCommand` and `DeleteCommand` retain identical signatures |
| `ReportRowViewModel` | `ViewModels/ReportRowViewModel.cs` | **None** — row data model is not affected |
| `ReportsView.axaml.cs` | `Views/ReportsView.axaml.cs` | **None** — code-behind is already clean (no event handlers for action buttons) |
| Domain entities | `Rentier.Domain` | **None** — zero domain impact |
| Application handlers | `Rentier.Application` | **None** — zero application impact |
| Infrastructure | `Rentier.Infrastructure` | **None** — zero infrastructure impact |

---

## Validation Rules

No new validation rules. Existing command parameter validation (Guid report ID) is unchanged.

## State Transitions

No state transitions are introduced or modified. The report lifecycle is unaffected.
