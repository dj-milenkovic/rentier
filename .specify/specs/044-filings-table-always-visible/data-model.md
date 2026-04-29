# Data Model: 044-filings-table-always-visible

**Date**: 2025-07-15

## Overview

This is a UI-only feature. No domain entities, value objects, or database schema changes are required. The data model impact is limited to view-layer bindings.

## Affected ViewModel Properties

| Property | Type | Current Usage | Change |
|----------|------|---------------|--------|
| `HasItems` | `bool` (computed) | Controls DataGrid `IsVisible`; controls select-all checkbox `IsEnabled` | **Remove** DataGrid `IsVisible` binding; keep checkbox `IsEnabled` binding |
| `IsEmpty` | `bool` (computed) | Controls full-page empty-state `TextBlock` visibility | Repurpose to control subtle in-table empty-state message |
| `IsLoading` | `bool` | Controls `ProgressBar` visibility | No change |

## View Changes (FilingsView.axaml)

### Element: DataGrid `FilingsGrid`
- **Before**: `IsVisible="{Binding HasItems}"` — hidden when no rows
- **After**: Remove `IsVisible` binding entirely — always visible

### Element: Empty-state TextBlock (line ~90)
- **Before**: `DockPanel.Dock="Top"` positioned above DataGrid, centered full-page
- **After**: Repositioned below DataGrid or removed in favor of a subtle message below the grid area. Uses same `Filings_Empty` localization key. Styled smaller/muted (secondary text color, smaller font).

## No Domain/Application/Infrastructure Changes

Per FR-007, this feature is strictly limited to `Rentier.Desktop`:
- No entity modifications
- No repository changes
- No command/query changes
- No migration needed
