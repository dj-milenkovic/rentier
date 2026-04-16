# Data Model: Header Checkbox for Select All / Clear All

**Feature**: 028-header-checkbox-select-all
**Date**: 2025-07-17

## Overview

This feature is **Desktop-layer only** — no Domain, Application, or Infrastructure changes. All modifications are to existing ViewModels and Views. No new entities, DTOs, repositories, or database changes are required.

## ViewModel Property Changes

### FilingsViewModel (existing)

| Property | Type | Change | Description |
|----------|------|--------|-------------|
| `IsAllSelected` | `bool?` | **NEW** | Tri-state header checkbox binding: `true` = all selected, `false` = none, `null` = indeterminate |
| `SelectedCount` | `int` | Unchanged | Existing property, now also drives `IsAllSelected` computation |
| `HasItems` | `bool` | Unchanged | Existing property, now also drives header checkbox `IsEnabled` |
| `HasSelection` | `bool` | Unchanged | Existing property, still drives Delete Selected button visibility |
| `SelectAllCommand` | `ReactiveCommand` | Unchanged | Retained per FR-014, invoked from `IsAllSelected` setter |
| `ClearSelectionCommand` | `ReactiveCommand` | Unchanged | Retained per FR-014, invoked from `IsAllSelected` setter |

### ReportsViewModel (existing)

| Property | Type | Change | Description |
|----------|------|--------|-------------|
| `IsAllSelected` | `bool?` | **NEW** | Tri-state header checkbox binding: `true` = all selected, `false` = none, `null` = indeterminate |
| `SelectedCount` | `int` | Unchanged | Existing property, now also drives `IsAllSelected` computation |
| `HasItems` | `bool` | Unchanged | Existing property, now also drives header checkbox `IsEnabled` |
| `HasSelection` | `bool` | Unchanged | Existing property, still drives Delete Selected button visibility |
| `SelectAllCommand` | `ReactiveCommand` | Unchanged | Retained per FR-014, invoked from `IsAllSelected` setter |
| `ClearSelectionCommand` | `ReactiveCommand` | Unchanged | Retained per FR-014, invoked from `IsAllSelected` setter |

### FilingRowViewModel (existing) — No Changes

| Property | Type | Change |
|----------|------|--------|
| `IsSelected` | `bool` | Unchanged |

### ReportRowViewModel (existing) — No Changes

| Property | Type | Change |
|----------|------|--------|
| `IsSelected` | `bool` | Unchanged |

## State Machine: IsAllSelected

```text
               SelectedCount == 0
                    ┌──────┐
                    │      │
                    ▼      │
         ┌──────────────────┐
  click  │   false (none)   │  all rows unchecked
  ────► │                  │◄──── empty state (disabled)
         └────────┬─────────┘
                  │ click (sets all)
                  ▼
         ┌──────────────────┐
         │   true (all)     │  all rows checked
         │                  │
         └────────┬─────────┘
                  │ click (clears all)
                  ▼
         ┌──────────────────┐
         │   false (none)   │
         └──────────────────┘

  Individual row toggle (0 < count < total):
         ┌──────────────────┐
         │   null (partial) │  indeterminate state
         │                  │
         └────────┬─────────┘
                  │ click (selects all)
                  ▼
         ┌──────────────────┐
         │   true (all)     │
         └──────────────────┘
```

### State Transitions

| Current State | Trigger | New State |
|---------------|---------|-----------|
| `false` (none) | User clicks header checkbox | `true` (all) — via SelectAllCommand |
| `null` (partial) | User clicks header checkbox | `true` (all) — via SelectAllCommand |
| `true` (all) | User clicks header checkbox | `false` (none) — via ClearSelectionCommand |
| any | Individual row toggled → `0 < SelectedCount < Rows.Count` | `null` (partial) |
| any | Individual row toggled → `SelectedCount == Rows.Count` | `true` (all) |
| any | Individual row toggled → `SelectedCount == 0` | `false` (none) |
| any | Rows reloaded (after delete/navigation) | Recomputed from new `SelectedCount` |
| any | `Rows.Count == 0` | `false` (disabled) |

## Reactive Data Flow

```text
User clicks row checkbox
  │
  ▼
row.IsSelected changes (RaiseAndSetIfChanged)
  │
  ▼
RebuildRowSubscriptions subscription fires
  │
  ▼
SelectedCount = Rows.Count(r => r.IsSelected)
  │
  ├──► HasSelection recomputes (SelectedCount > 0)
  ├──► DeleteSelectedLabel recomputes
  └──► IsAllSelected recomputes:
        if (Rows.Count == 0) → false
        else if (SelectedCount == 0) → false
        else if (SelectedCount == Rows.Count) → true
        else → null

User clicks header checkbox
  │
  ▼
IsAllSelected setter called with value from Avalonia:
  │
  ├─ value == true  → SelectAllCommand.Execute()
  ├─ value == false → ClearSelectionCommand.Execute()
  └─ value == null  → ignored (reactive recompute handles it)
```

## View Changes

### FilingsView.axaml

| Element | Change |
|---------|--------|
| `DataGridTemplateColumn` (selection, Width="40") | **ADD** `HeaderTemplate` with tri-state `CheckBox` bound to `IsAllSelected` |
| Toolbar "Select All" button | **REMOVE** |
| Toolbar "Clear Selection" button | **REMOVE** |
| Toolbar "Delete Selected" button | Unchanged |

### ReportsView.axaml

| Element | Change |
|---------|--------|
| `DataGridTemplateColumn` (selection, Width="40") | **ADD** `HeaderTemplate` with tri-state `CheckBox` bound to `IsAllSelected` |
| Toolbar "Select All" button | **REMOVE** |
| Toolbar "Clear Selection" button | **REMOVE** |
| Toolbar "Delete Selected" button | Unchanged |

## Resource String Changes

| Key | Change |
|-----|--------|
| `BulkDelete_SelectAll_Button` | Mark as unused (do not delete — may be needed for accessibility/tooltip later) |
| `BulkDelete_ClearSelection_Button` | Mark as unused (do not delete — may be needed for accessibility/tooltip later) |

## No-Change Inventory

The following are explicitly **not changed**:

- `Rentier.Domain` — no domain entities, value objects, or rules affected
- `Rentier.Application` — no commands, queries, handlers, or DTOs affected
- `Rentier.Infrastructure` — no repositories, parsers, or external services affected
- `FilingRowViewModel.IsSelected` — existing property, unchanged
- `ReportRowViewModel.IsSelected` — existing property, unchanged
- `SelectAllCommand` / `ClearSelectionCommand` — retained and reused per FR-014
- `BulkDeleteCommand` — unchanged
- Database schema — no migrations
