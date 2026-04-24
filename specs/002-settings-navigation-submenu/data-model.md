# Data Model: Settings Navigation Sub-menu Items in Sidebar

**Feature**: 036-settings-navigation-submenu  
**Date**: 2025-07-15  
**Prerequisite**: [research.md](research.md) complete

## Overview

This feature introduces no new persistent entities, database changes, or domain objects. All changes are in the Desktop layer's in-memory navigation model. This document describes the ViewModel-level data structures that enable hierarchical sidebar navigation.

## Modified Entity: NavigationEntry

**File**: `src/Rentier.Desktop/ViewModels/NavigationEntry.cs`  
**Current state**: Flat navigation item with Label, ViewModel, IsVisible, Icon.  
**New state**: Extended with group/hierarchy semantics.

### Current Fields

| Field | Type | Description |
|-------|------|-------------|
| `Label` | `string` (reactive) | Display text in sidebar |
| `ViewModel` | `ReactiveObject` | Content ViewModel shown when selected |
| `IsVisible` | `bool` | Whether entry appears in sidebar (false = transient sub-page) |
| `Icon` | `StreamGeometry?` | Lucide icon path data |

### New Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `IsGroup` | `bool` | `false` | When `true`, entry is a collapsible group header (not navigable) |
| `IsExpanded` | `bool` (reactive) | `true` | Expand/collapse state for group entries. Raises `PropertyChanged`. |
| `Children` | `IReadOnlyList<NavigationEntry>` | `[]` | Child entries belonging to this group (empty for leaf entries) |
| `ParentGroup` | `NavigationEntry?` | `null` | Back-reference to parent group (for child entries only) |
| `IndentLevel` | `int` | `0` | Visual indent depth: `0` for top-level, `1` for settings children |

### Validation Rules

- If `IsGroup` is `true`, `ViewModel` MUST be `null` (group headers are not navigable).
- If `IsGroup` is `true`, `Children` MUST have at least one entry.
- If `IsGroup` is `false` and `ParentGroup` is not `null`, the entry is a child item.
- `IndentLevel` is derived: `0` for top-level entries, `1` for children of a group.
- `IsVisible` semantics are unchanged — hidden entries (like ManualFiling transient page) continue to work.

### State Transitions

```text
Group header click:
  IsExpanded = true  ──(click)──>  IsExpanded = false  (children hidden)
  IsExpanded = false ──(click)──>  IsExpanded = true   (children shown)

Child item click:
  SelectedEntry = oldEntry ──(click child)──> SelectedEntry = childEntry
  CurrentViewModel = child.ViewModel
```

## Flattened Navigation List Model

The `MainWindowViewModel.NavigationEntries` changes from a static `IReadOnlyList<NavigationEntry>` to a computed flat list derived from the hierarchical structure. The source of truth is:

```text
TopLevelEntries:
  [0] Dashboard        (leaf, IndentLevel=0)
  [1] Filings          (leaf, IndentLevel=0)
  [2] Reports          (leaf, IndentLevel=0)
  [3] Sync             (leaf, IndentLevel=0)
  [4] Settings         (group, IndentLevel=0, IsExpanded=true)
       ├── [4.0] Profile    (child, IndentLevel=1)
       ├── [4.1] Holidays   (child, IndentLevel=1)
       ├── [4.2] Mailboxes  (child, IndentLevel=1)
       ├── [4.3] Importers  (child, IndentLevel=1)
       └── [4.4] Language   (child, IndentLevel=1)
```

When `Settings.IsExpanded = true`, the flat list contains all 10 entries (4 top-level + 1 group header + 5 children).  
When `Settings.IsExpanded = false`, the flat list contains 5 entries (4 top-level + 1 group header).

## Localization Keys

### New Keys (Strings.resx)

| Key | English Value | Purpose |
|-----|--------------|---------|
| `Nav_Settings_Profile` | `Profile` | Settings child: taxpayer profile |
| `Nav_Settings_Holidays` | `Holidays` | Settings child: holiday configuration |
| `Nav_Settings_Mailboxes` | `Mailboxes` | Settings child: IMAP mailbox configuration |
| `Nav_Settings_Importers` | `Importers` | Settings child: report importer definitions |
| `Nav_Settings_Language` | `Language` | Settings child: language & appearance |

### Existing Keys (unchanged)

| Key | Value | Usage |
|-----|-------|-------|
| `Nav_Settings` | `Settings` | Group header label |
| `Nav_Dashboard` | `Dashboard` | Unchanged top-level |
| `Nav_Filings` | `Filings` | Unchanged top-level |
| `Nav_Reports` | `Reports` | Unchanged top-level |
| `Nav_Sync` | `Sync` | Unchanged top-level |

## Icon Resources

### New Icons (Icons.axaml)

| Key | Icon | Source | Purpose |
|-----|------|--------|---------|
| `NavChevronDownIcon` | Lucide `chevron-down` | 24×24 viewport | Expand/collapse affordance (rotated via `RenderTransform`) |
| `NavProfileIcon` | Lucide `user` | 24×24 viewport | Profile settings child |
| `NavHolidaysIcon` | Lucide `calendar` | 24×24 viewport | Holidays settings child |
| `NavMailboxesIcon` | Lucide `mail` | 24×24 viewport | Mailboxes settings child |
| `NavImportersIcon` | Lucide `download` | 24×24 viewport | Importers settings child |
| `NavLanguageIcon` | Lucide `globe` | 24×24 viewport | Language settings child |

## DI Registration Changes

### Removed

| Registration | Lifetime | Reason |
|-------------|----------|--------|
| `SettingsViewModel` | Transient | Tab container eliminated |

### Changed Lifetime

| Registration | Old Lifetime | New Lifetime | Reason |
|-------------|-------------|-------------|--------|
| `ProfileSettingsViewModel` | Transient | Singleton | Session-scoped state persistence (FR-009) |
| `HolidaySettingsViewModel` | Transient | Singleton | Session-scoped state persistence |
| `MailboxSettingsViewModel` | Transient | Singleton | Session-scoped state persistence |
| `ImporterSettingsViewModel` | Transient | Singleton | Session-scoped state persistence |
| `AppearanceSettingsViewModel` | Transient | Singleton | Session-scoped state persistence |

## Files Deleted

| File | Reason |
|------|--------|
| `src/Rentier.Desktop/Views/SettingsView.axaml` | TabControl container replaced by direct navigation |
| `src/Rentier.Desktop/Views/SettingsView.axaml.cs` | Code-behind for deleted view |
| `src/Rentier.Desktop/ViewModels/SettingsViewModel.cs` | Tab aggregator no longer needed |
