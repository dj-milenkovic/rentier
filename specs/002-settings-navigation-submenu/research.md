# Research: Settings Navigation Sub-menu Items in Sidebar

**Feature**: 036-settings-navigation-submenu  
**Date**: 2025-07-15

## R1: Hierarchical Navigation in Avalonia ListBox

**Question**: How to implement a collapsible group with child items inside an Avalonia `ListBox` sidebar?

**Decision**: Extend `NavigationEntry` with group semantics (`IsGroup`, `IsExpanded`, `Children`) and use a flattened observable collection bound to the `ListBox`. Group expand/collapse toggles visibility of child entries in the flat list.

**Rationale**: The current sidebar uses a `ListBox` with `ItemsSource="{Binding NavigationEntries}"` and a single `DataTemplate`. Avalonia's `ListBox` doesn't natively support `TreeView`-style hierarchy. Two patterns were evaluated:

1. **TreeView replacement**: Replace `ListBox` with Avalonia `TreeView`. Supports hierarchy natively but requires completely reworking sidebar styles (`sidebar-nav` class, `ListBoxItem` selectors, active pipe indicator, hover/selected states). High risk of visual regressions. `TreeView` item selection behavior differs from `ListBox` — would break the `SelectedItem` two-way binding pattern.

2. **Flattened list with group awareness** *(chosen)*: Keep the `ListBox` but introduce an `IsGroup` flag on `NavigationEntry`. The `ItemTemplate` uses an `ItemTemplateSelector` or conditional visibility within a single template to render group headers differently from child items. Child entries have an `IndentLevel` property for visual indentation. The `NavigationEntries` collection is a `ReadOnlyObservableCollection` backed by a source list. When a group is toggled, child entries are added/removed from the flat list.

**Alternatives considered**:
- `TreeView`: Rejected — too much sidebar style rework, selection model mismatch, and visual regression risk.
- `ItemsControl` with nested `ListBox`: Rejected — nested ListBox complicates single-selection semantics across groups.
- `Expander` control wrapping child `StackPanel`: Rejected — would break the unified `SelectedItem` binding and active pipe indicator.

---

## R2: Single-Selection Across Group Hierarchy

**Question**: How to maintain exactly one active item (top-level or settings child) at any time, with the active pipe indicator working correctly?

**Decision**: All navigable entries — top-level pages and settings children — participate in the same `ListBox.SelectedItem` binding. Group header entries are non-selectable (clicking toggles expand/collapse but does not set `SelectedEntry`). The existing `SelectedEntry` → `CurrentViewModel` reactive subscription works unchanged.

**Rationale**: The current pattern (`SelectedItem="{Binding SelectedEntry}"` → `WhenAnyValue(x => x.SelectedEntry).Subscribe(...)`) is clean and proven. Keeping all entries in one flat list preserves this. Group headers are filtered out from selection by handling the click in the ViewModel (toggling `IsExpanded`) rather than through `ListBox` selection. The AXAML template for group headers uses a `Button` or `ToggleButton` overlay (transparent, fills the row) so the `ListBoxItem` is never selected.

**Alternatives considered**:
- Separate `SelectedItem` per group: Rejected — would require complex multi-selection coordination.
- `SelectionChanged` event handler in code-behind: Rejected — violates MVVM; all logic stays in ViewModel.

---

## R3: Expand/Collapse Chevron Affordance

**Question**: What visual pattern for the expand/collapse toggle on the Settings group header?

**Decision**: Use a rotating chevron icon (Lucide `chevron-down` / `chevron-right`) rendered via `PathIcon` with a `RotateTransform` bound to the `IsExpanded` state. When expanded, the chevron points down (0° rotation); when collapsed, it points to the right (−90° rotation). The icon animates via an Avalonia `Transitions` property on the `RotateTransform`.

**Rationale**: Lucide icons are already used throughout the app (all nav icons are Lucide). Rotation is a standard UX pattern for disclosure triangles. Avalonia supports `RenderTransform` animations natively.

**Alternatives considered**:
- Two separate icons (swap visible): Simpler but no animation, feels janky.
- Text-based indicator (`▸`/`▾`): Too small, inconsistent with icon-based sidebar.

---

## R4: ViewLocator Compatibility with Direct Sub-ViewModel Navigation

**Question**: Will the existing `ViewLocator` convention correctly resolve sub-ViewModels to their Views when used as direct `CurrentViewModel` targets?

**Decision**: Yes — the existing `ViewLocator` already handles this correctly. No changes needed.

**Rationale**: The convention-based `ViewLocator` replaces `"ViewModels"` → `"Views"` and `"ViewModel"` → `"View"` in the fully-qualified type name:
- `Rentier.Desktop.ViewModels.ProfileSettingsViewModel` → `Rentier.Desktop.Views.ProfileSettingsView` ✅
- `Rentier.Desktop.ViewModels.HolidaySettingsViewModel` → `Rentier.Desktop.Views.HolidaySettingsView` ✅
- `Rentier.Desktop.ViewModels.MailboxSettingsViewModel` → `Rentier.Desktop.Views.MailboxSettingsView` ✅
- `Rentier.Desktop.ViewModels.ImporterSettingsViewModel` → `Rentier.Desktop.Views.ImporterSettingsView` ✅
- `Rentier.Desktop.ViewModels.AppearanceSettingsViewModel` → `Rentier.Desktop.Views.AppearanceSettingsView` ✅

All these Views already exist as `ReactiveUserControl<T>`. They are currently used as embedded children inside `SettingsView.axaml` with explicit `DataContext` binding. When they become standalone `CurrentViewModel` targets, the `ContentControl` in `MainWindow.axaml` will invoke `ViewLocator.Build()` which creates them via `Activator.CreateInstance()`. This is the same pattern used for `DashboardView`, `FilingsView`, etc.

**Alternatives considered**: None — existing convention works perfectly.

---

## R5: DI Registration Changes

**Question**: What changes are needed in `CompositionRoot.cs` for the new navigation structure?

**Decision**: Remove the `SettingsViewModel` registration. Change settings sub-ViewModel registrations from `Transient` to `Singleton` lifetime so navigation state (form data, scroll position) persists within the session.

**Rationale**: Currently, `SettingsViewModel` is registered as `Transient` and aggregates all five tab VMs. Since the tab container is being removed, `SettingsViewModel` is no longer needed. The five sub-ViewModels are currently `Transient` (created fresh each time). For the new navigation model, they must be `Singleton` so that:
1. Navigation state persists when switching between pages (FR-009)
2. Unsaved form data is not lost when clicking to another settings page and back
3. The same VM instance is used every time the user clicks the sidebar item

`MainWindowViewModel` is already `Singleton` — it will hold references to the sub-VMs, which aligns with `Singleton` lifetime.

**Alternatives considered**:
- Keep Transient + cache in MainWindowViewModel: Works but is redundant when DI already provides singletons.
- Scoped lifetime: Avalonia DI doesn't use scopes in the same way; Singleton is the correct session-level lifetime.

---

## R6: Localization Strategy for Child Navigation Labels

**Question**: How to localize the five new child navigation labels and handle runtime culture changes?

**Decision**: Add five new resource keys (`Nav_Settings_Profile`, `Nav_Settings_Holidays`, `Nav_Settings_Mailboxes`, `Nav_Settings_Importers`, `Nav_Settings_Language`) to `Strings.resx`. The `UpdateNavigationLabels()` method in `MainWindowViewModel` is extended to update child entry labels when culture changes. The group header label uses the existing `Nav_Settings` key.

**Rationale**: The existing localization pattern uses `ILocalizationService[key]` for initial labels and `CultureChanged` observable for runtime updates. The same pattern applies to child entries. New keys follow the existing naming convention (`Nav_` prefix + section name).

**Alternatives considered**:
- Reuse existing `Settings_*_TabHeader` keys: These say "Profile", "Holidays", etc. — usable but semantically they belong to the tab concept. New keys provide cleaner separation and allow different wording if desired (e.g., sidebar labels may be shorter).

---

## R7: Settings Group Default Expanded State

**Question**: Should the Settings group start expanded or collapsed? How is the state persisted?

**Decision**: Expanded by default on application launch (FR-004). Expand/collapse state is in-memory only — not persisted across restarts. The `IsExpanded` property on the group `NavigationEntry` is initialized to `true`.

**Rationale**: Per spec FR-004: "The Settings group MUST be expanded by default when the application launches." Per Assumptions: "Restarting the application resets to the default expanded Settings group with no pre-selected child item." No persistence mechanism is needed.

**Alternatives considered**:
- Persist state via `UserPreference` (already exists for theme/language): Over-engineering for a simple UI preference. Can be added as a follow-up if users request it.
