# UI Contract: Reports Pagination Controls

**Feature**: 029-pagination-30-items-reports-pagination  
**Date**: 2025-07-16

## Reports Page Pagination Bar

### Layout

The pagination bar is a horizontal stack panel docked to the bottom of the `ReportsView`, centred horizontally, with 8px spacing between children and 8px margin. It mirrors the existing Filings pagination bar identically.

```text
┌──────────────────────────────────────────────────────────────┐
│                     [Reports DataGrid]                       │
│                          ...                                 │
├──────────────────────────────────────────────────────────────┤
│          [← Previous]   Page 1 of 3   [Next →]              │
└──────────────────────────────────────────────────────────────┘
```

### Visibility Rules

| Condition | Pagination Bar Visible | Empty State Visible |
|-----------|----------------------|---------------------|
| Reports exist (`HasItems = true`) | Yes | No |
| No reports (`IsEmpty = true`) | No | Yes |
| Loading (`IsLoading = true`) | Yes (buttons disabled) | No |

### Bindings

| Control | Binding | Source |
|---------|---------|--------|
| Previous Button `Content` | `{x:Static res:Strings.Reports_Page_Previous}` | Resource |
| Previous Button `Command` | `{Binding PreviousPageCommand}` | ViewModel |
| Page Indicator `Text` | `{Binding PageIndicator}` | ViewModel |
| Next Button `Content` | `{x:Static res:Strings.Reports_Page_Next}` | Resource |
| Next Button `Command` | `{Binding NextPageCommand}` | ViewModel |

### Command Enable/Disable States

| Command | Enabled When | Disabled When |
|---------|-------------|---------------|
| `PreviousPageCommand` | `CurrentPage > 1 && !IsLoading` | `CurrentPage == 1 \|\| IsLoading` |
| `NextPageCommand` | `CurrentPage < TotalPages && !IsLoading` | `CurrentPage == TotalPages \|\| IsLoading` |

### Page Indicator Format

`"Page {0} of {1}"` where `{0}` = `CurrentPage`, `{1}` = `TotalPages`.

Sourced from `Strings.Reports_Page_Indicator` resource.

## Filings Page — Page Size Change

The Filings page pagination bar layout and bindings remain unchanged. The only visible effect is that each page now displays up to 30 items instead of 20.

## AXAML Structure (Reports Pagination Bar)

```xml
<!-- Pagination bar -->
<StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="8"
            Margin="8" HorizontalAlignment="Center"
            IsVisible="{Binding HasItems}">
  <Button Content="{x:Static res:Strings.Reports_Page_Previous}"
          Command="{Binding PreviousPageCommand}" />
  <TextBlock Text="{Binding PageIndicator}" VerticalAlignment="Center" />
  <Button Content="{x:Static res:Strings.Reports_Page_Next}"
          Command="{Binding NextPageCommand}" />
</StackPanel>
```

**Placement**: Docked to bottom, before the DataGrid (same as FilingsView pattern). The DataGrid fills remaining space.
