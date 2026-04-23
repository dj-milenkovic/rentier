# Tasks: Settings Navigation Sub-menu Items in Sidebar

**Feature**: 036-settings-navigation-submenu  
**Input**: `.specify/specs/036-settings-navigation-submenu/spec.md`  
**Plan decisions** (inline — no plan.md generated):
- Extend `NavigationEntry` with `IsGroup`, `IsExpanded`, `Children`, `ParentGroup`, `IndentLevel`; no new types
- Delete `SettingsViewModel` + `SettingsView` (tab container eliminated)
- Each settings sub-ViewModel becomes a direct navigation target (singleton lifetime)
- Sidebar stays a `ListBox` with flattened entries; no TreeView switch
- Add 5 localization keys: `Nav_Settings_Profile/Holidays/Mailboxes/Importers/Language`
- Add 6 new Lucide icons: Settings group header + 5 children
- `ViewLocator` unchanged — naming convention covers sub-ViewModels already
- ~10 modified files · ~3 deleted files · ~0 new source files

**Tests**: Included (CA-006 — Desktop layer tests required for navigation ViewModel changes).

---

## Phase 1: Setup

**Purpose**: Confirm scope and pre-conditions before touching production code.

- [X] T001 Verify constitution checklist in `.specify/specs/036-settings-navigation-submenu/checklists/requirements.md` — confirm change is Desktop-only (CA-001), no monetary/date fields introduced (CA-002), no new network calls (CA-004), no disk-persisted state (CA-003)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core model and asset changes that ALL user stories depend on. Must be complete before any story phase.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 Extend `NavigationEntry` with new reactive properties and group support in `src/Rentier.Desktop/ViewModels/NavigationEntry.cs`:
  - Make `IsVisible` mutable and reactive (`this.RaiseAndSetIfChanged`)
  - Add `bool IsGroup { get; init; }` — true for the collapsible Settings group header
  - Add reactive `bool IsExpanded { get; set; }` (RaiseAndSetIfChanged) — controls child visibility
  - Add `IReadOnlyList<NavigationEntry> Children { get; init; }` — child entries owned by a group
  - Add `NavigationEntry? ParentGroup { get; init; }` — back-reference for child entries
  - Add `int IndentLevel { get; init; }` — 0 for top-level, 1 for Settings children
  - Make `ReactiveObject? ViewModel { get; }` nullable — group headers have no content ViewModel
  - Add `ToggleExpanded()` method: flip `IsExpanded`, then set `IsVisible = IsExpanded` on every entry in `Children`
  - Update constructor signature to accept all new properties with sensible defaults

- [X] T003 [P] Add 5 localization keys to `src/Rentier.Desktop/Resources/Strings.resx` — English values:
  - `Nav_Settings_Profile` = "Profile"
  - `Nav_Settings_Holidays` = "Holidays"
  - `Nav_Settings_Mailboxes` = "Mailboxes"
  - `Nav_Settings_Importers` = "Importers"
  - `Nav_Settings_Language` = "Language"
  - Add matching entries in the Serbian (sr-Latn) `.resx` sibling file if it exists alongside the English one

- [X] T004 [P] Add 6 new Lucide-sourced `StreamGeometry` resources to `src/Rentier.Desktop/Assets/Icons.axaml`:
  - `NavSettingsGroupIcon` — sliders / settings-2 icon for the Settings group header row
  - `NavProfileIcon` — user / circle-user icon for the Profile child
  - `NavHolidaysIcon` — calendar icon for the Holidays child
  - `NavMailboxesIcon` — mail / inbox icon for the Mailboxes child
  - `NavImportersIcon` — download / file-input icon for the Importers child
  - `NavLanguageIcon` — globe / languages icon for the Language child
  - Follow the existing Lucide MIT-licensed path data conventions already present in the file

**Checkpoint**: `NavigationEntry` extended, icons and localization keys added — user story phases can begin.

---

## Phase 3: User Story 5 — Removal of Tabbed Settings View (Priority: P1)

**Goal**: Eliminate the `SettingsView` tab container and `SettingsViewModel` wrapper entirely, so no tabbed settings path can be reached.

**Independent Test**: After this phase, building the project with `dotnet build src/Rentier.Desktop` must succeed with zero references to `SettingsViewModel` or `SettingsView`.

- [X] T005 [US5] Delete the tabbed settings view files:
  - `src/Rentier.Desktop/Views/SettingsView.axaml`
  - `src/Rentier.Desktop/Views/SettingsView.axaml.cs`

- [X] T006 [US5] Delete the tabbed settings ViewModel:
  - `src/Rentier.Desktop/ViewModels/SettingsViewModel.cs`

- [X] T007 [US5] Update DI registrations in `src/Rentier.Desktop/Composition/CompositionRoot.cs`:
  - Remove `services.AddTransient<SettingsViewModel>()` (line ~66)
  - Change the 5 settings sub-ViewModel registrations from `AddTransient` to `AddSingleton` to enforce singleton lifetime (per-session state persistence):
    - `ProfileSettingsViewModel` → `AddSingleton`
    - `HolidaySettingsViewModel` → `AddSingleton`
    - `MailboxSettingsViewModel` → `AddSingleton`
    - `ImporterSettingsViewModel` → `AddSingleton`
    - `AppearanceSettingsViewModel` → `AddSingleton`

**Checkpoint**: `SettingsViewModel` and `SettingsView` no longer exist; sub-VMs are registered as singletons. Build will fail until Phase 4 fixes `MainWindowViewModel` usages.

---

## Phase 4: User Story 1 — Navigate Directly to a Settings Sub-page (Priority: P1) 🎯 MVP

**Goal**: Each settings section is accessible with one click from the sidebar via a flattened `ListBox` with a Settings group header and five child entries.

**Independent Test**: Click each child item (Profile, Holidays, Mailboxes, Importers, Language) and confirm the corresponding standalone settings view renders in the content area with no tab strip.

- [X] T008 [US1] Rebuild `MainWindowViewModel` constructor in `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs`:
  - Remove the `SettingsViewModel settingsVm` constructor parameter
  - Resolve the 5 singleton sub-VMs directly from `provider.GetRequiredService<T>()`:
    - `profileVm` = `provider.GetRequiredService<ProfileSettingsViewModel>()`
    - `holidayVm` = `provider.GetRequiredService<HolidaySettingsViewModel>()`
    - `mailboxVm` = `provider.GetRequiredService<MailboxSettingsViewModel>()`
    - `importerVm` = `provider.GetRequiredService<ImporterSettingsViewModel>()`
    - `appearanceVm` = `provider.GetRequiredService<AppearanceSettingsViewModel>()`
  - Construct the Settings group header `NavigationEntry` with:
    - `IsGroup = true`, `IsExpanded = true`, `ViewModel = null`
    - `Icon = NavIcon("NavSettingsGroupIcon")`
    - `label = localizationService["Nav_Settings"]`
  - Construct 5 child `NavigationEntry` instances (one per sub-VM) with:
    - `IndentLevel = 1`, `IsVisible = true`, `ParentGroup = settingsGroupEntry`
    - Icons: `NavProfileIcon`, `NavHolidaysIcon`, `NavMailboxesIcon`, `NavImportersIcon`, `NavLanguageIcon`
    - Labels via new localization keys (`Nav_Settings_Profile`, etc.)
  - Set `Children` on the group header to the 5 child entries (ordered: Profile, Holidays, Mailboxes, Importers, Language)
  - Rebuild `NavigationEntries` as a 10-element flattened list:
    `Dashboard, Filings, Reports, Sync, settingsGroup, profileChild, holidaysChild, mailboxesChild, importersChild, languageChild`

- [X] T009 [US1] Update `WhenAnyValue(SelectedEntry)` subscription and add `_lastContentEntry` tracking in `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs`:
  - Add `private NavigationEntry? _lastContentEntry` field (initialized to Dashboard entry)
  - In the `WhenAnyValue(SelectedEntry)` subscription:
    - If `entry.IsGroup == true`: call `entry.ToggleExpanded()`, then immediately restore `SelectedEntry = _lastContentEntry` (no `CurrentViewModel` update)
    - If `entry.IsGroup == false` and `entry.ViewModel is not null`: set `_lastContentEntry = entry`, then `CurrentViewModel = entry.ViewModel`
  - Update `UpdateNavigationLabels()` — replace `SettingsViewModel → Nav_Settings` with:
    - Group header identified by `e.IsGroup == true` → key `"Nav_Settings"`
    - `ProfileSettingsViewModel` → `"Nav_Settings_Profile"`
    - `HolidaySettingsViewModel` → `"Nav_Settings_Holidays"`
    - `MailboxSettingsViewModel` → `"Nav_Settings_Mailboxes"`
    - `ImporterSettingsViewModel` → `"Nav_Settings_Importers"`
    - `AppearanceSettingsViewModel` → `"Nav_Settings_Language"`

- [X] T010 [US1] Update the sidebar `DataTemplate` in `src/Rentier.Desktop/Views/MainWindow.axaml` to visually differentiate group header rows from child rows:
  - The outermost `Grid` already checks `IsVisible="{Binding IsVisible}"` — keep this unchanged
  - Inside the grid, add two conditionally-visible containers (one for group header, one for regular items):
    - **Group header container** (`IsVisible="{Binding IsGroup}"`): 3-column layout (pipe-placeholder width 4px, icon column 44px, label+chevron column `*`); icon uses `NavSettingsGroupIcon`; label is `TextBlock Text="{Binding Label}"`; chevron is a `PathIcon` with a `RotateTransform` whose `Angle` is bound to `IsExpanded` via a `BoolToDoubleConverter` (false→0°, true→90°); no active pipe
    - **Child / regular item container** (`IsVisible="{Binding !IsGroup}"`): same 3-column layout as current template; icon column gets `Margin="16,0,0,0"` offset for `IndentLevel=1` (use `IndentLevel` × 16 via a converter or hardcode for level 1); active pipe remains `IsVisible="{Binding $parent[ListBoxItem].IsSelected}"`

**Checkpoint**: All five settings child items are clickable and navigate to the correct standalone view. US1 fully functional. US5 validated — no tab strip appears.

---

## Phase 5: User Story 2 — Settings Group Collapse and Expand (Priority: P2)

**Goal**: Clicking the Settings group header toggles the visibility of all five child items.

**Independent Test**: Click the group header when expanded — five children disappear. Click again — five children reappear. Application launch shows expanded group by default.

- [X] T011 [US2] Verify `ToggleExpanded()` on `NavigationEntry` (added in T002) correctly propagates to child `IsVisible` — trace through: group header constructed with `IsExpanded=true`; children constructed with `IsVisible=true`; first `ToggleExpanded()` call sets `IsExpanded=false` and each child `IsVisible=false`; second call restores both — in `src/Rentier.Desktop/ViewModels/NavigationEntry.cs`

- [X] T012 [US2] Add chevron rotation animation/transform in the group header container template in `src/Rentier.Desktop/Views/MainWindow.axaml`:
  - The chevron `PathIcon` added in T010 must visually rotate between 0° (collapsed, pointing right) and 90° (expanded, pointing down)
  - If Avalonia Transitions are desired: add a `Transitions` collection on the `RotateTransform` with a `DoubleTransition` on `Angle` (Duration 150ms) for smooth toggle feel
  - Confirm the binding to `IsExpanded` resolves correctly via the converter registered in application resources

**Checkpoint**: Settings group header toggling works; children appear/disappear; chevron animates.

---

## Phase 6: User Story 3 — Active Sub-page Highlighting (Priority: P2)

**Goal**: The selected settings child item shows the same accent-colored vertical pipe as top-level nav items; the group header never shows the pipe.

**Independent Test**: Click each of the 5 child items in turn — exactly one active pipe visible at all times, always on the child item. Filings selected → no settings pipe visible.

- [X] T013 [US3] Guard the active pipe in `MainWindow.axaml` — update the pipe `Border.IsVisible` binding inside the **child/regular item container** (added in T010) to confirm it reads `IsVisible="{Binding $parent[ListBoxItem].IsSelected}"` and that the **group header container** has NO pipe element at all (group header never shows an active indicator regardless of ListBox selection state)

- [X] T014 [US3] Validate `_lastContentEntry` restore (added in T009) in `MainWindowViewModel` — when the group header is clicked: `SelectedEntry` must revert to `_lastContentEntry` within the same synchronous call frame so the `ListBox` shows no stale highlight on the group header row; if `_lastContentEntry` is null on first click (before any navigation), restore `SelectedEntry` to `NavigationEntries[0]` (Dashboard) — in `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs`

**Checkpoint**: Active indicator tracks correctly. Exactly one item highlighted at all times.

---

## Phase 7: User Story 4 — Navigation State Persistence Within Session (Priority: P3)

**Goal**: Navigating away from a settings sub-page and returning to it within the same session preserves the ViewModel's in-memory state (form inputs, loaded data).

**Independent Test**: Open Profile settings, change a field, navigate to Filings, click Profile again — the changed field value is still present in the form (not reset).

- [X] T015 [US4] Confirm singleton sub-VM lifetime end-to-end in `src/Rentier.Desktop/Composition/CompositionRoot.cs`:
  - Verify all 5 settings sub-VMs are `AddSingleton` (changed in T007)
  - Verify `MainWindowViewModel` holds the same singleton instances in its `NavigationEntries` children (resolved once in T008 via `provider.GetRequiredService<T>()` — the DI container returns the same instance on every call for singleton lifetime)
  - No additional code changes required if T007 and T008 are complete; this task is a verification checkpoint only

**Checkpoint**: Session-scoped state persistence is structurally guaranteed by singleton registration. No disk I/O introduced (CA-003 satisfied).

---

## Phase 8: Tests (Constitution CA-006 Compliance)

**Purpose**: Desktop navigation ViewModel changes must be covered by unit tests before merge.

- [X] T016 [P] Update `tests/Rentier.UnitTests/Desktop/MainWindowViewModelSmokeTests.cs`:
  - Remove all `SettingsViewModel` construction and parameter passing from `CreateProvider()` and test bodies
  - Update `NavigationEntriesHasFiveItems` → assert `Count == 10` (4 top-level + 1 group header + 5 children)
  - Add test: `NavigationEntries_SettingsGroup_IsGroupTrueAndExpandedByDefault` — the 5th entry has `IsGroup=true` and `IsExpanded=true`
  - Add test: `NavigationEntries_SettingsChildren_HaveCorrectIndentLevelAndParentGroup` — entries 6–10 have `IndentLevel=1`, `ParentGroup == NavigationEntries[4]`, `IsVisible=true`
  - Add test: `SelectingGroupHeader_TogglesIsExpanded_DoesNotChangeCurrentViewModel` — set `SelectedEntry = NavigationEntries[4]` (group header); assert `CurrentViewModel` is unchanged (still `DashboardViewModel`); assert group header `IsExpanded` flipped
  - Add test: `SelectingProfileChild_SetCurrentViewModelToProfileSettingsViewModel` — set `SelectedEntry = NavigationEntries[5]`; assert `CurrentViewModel` is `ProfileSettingsViewModel`

- [X] T017 [P] Write `tests/Rentier.UnitTests/Desktop/NavigationEntryGroupTests.cs`:
  - Test: `ToggleExpanded_WhenExpandedTrue_SetsChildrenIsVisibleFalse`
  - Test: `ToggleExpanded_WhenExpandedFalse_SetsChildrenIsVisibleTrue`
  - Test: `ToggleExpanded_FlipsIsExpanded`
  - Test: `ChildEntry_HasCorrectParentGroupReference`
  - Test: `GroupEntry_Children_AreInCorrectOrder` (Profile, Holidays, Mailboxes, Importers, Language by label key)
  - Test: `NavigationEntry_DefaultIsGroup_IsFalse` (backwards-compatibility guard)

**Checkpoint**: All new tests pass; existing tests updated and green.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T018 [P] Grep for any remaining `SettingsViewModel` references in `src/` — run `grep -r "SettingsViewModel" src/Rentier.Desktop` and resolve any orphaned `using` statements, XML comments, or auto-generated designer code that still references the deleted class

- [X] T019 [P] Run `dotnet build Rentier.slnx` from the repository root and confirm zero errors and zero CS warnings related to nullable ViewModel references introduced by making `NavigationEntry.ViewModel` nullable (add null-forgiving operators `!` where the compiler cannot infer non-null for known non-group entries)

- [X] T020 Run app (`dotnet run --project src/Rentier.Desktop`) and manually verify all five US1 acceptance scenarios: click Profile → ProfileSettingsView; click Holidays → HolidaySettingsView; click Mailboxes → MailboxSettingsView; click Importers → ImporterSettingsView; click Language → AppearanceSettingsView — none show a tab strip or tab host

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1; T003 and T004 can run in parallel with each other; T002 must complete before any other task that modifies NavigationEntry consumers
- **Phase 3 (US5)**: Depends on Phase 2 (T002 complete); T005 and T006 can run in parallel; T007 follows T005/T006
- **Phase 4 (US1)**: Depends on Phase 3; T008 must complete before T009; T010 can start in parallel with T009 (different files)
- **Phase 5 (US2)**: Depends on Phase 4 (T008/T009/T010 complete); T011 and T012 can run in parallel
- **Phase 6 (US3)**: Depends on Phase 5; T013 and T014 can run in parallel (different files)
- **Phase 7 (US4)**: Depends on Phase 4 (T007/T008); verification-only, no blocking production changes
- **Phase 8 (Tests)**: T016 and T017 can run in parallel after Phase 6 is complete
- **Phase 9 (Polish)**: Depends on Phase 8 — run after all tests pass

### User Story Dependencies

- **US5 (P1)**: Can start after Foundational (Phase 2) — no dependencies on other stories; UNBLOCKS US1
- **US1 (P1)**: Depends on US5 being complete (SettingsViewModel gone before rebuilding MainWindowViewModel)
- **US2 (P2)**: Depends on US1 (group structure in place)
- **US3 (P2)**: Depends on US2 (expand/collapse must work for selection restore to make sense)
- **US4 (P3)**: Depends on US1 (singleton VMs in place); no new code beyond verification

### Within Each Phase

- Delete tasks (T005, T006) before update tasks (T007, T008)
- `NavigationEntry` extension (T002) before `MainWindowViewModel` rebuild (T008)
- Icons (T004) and localization keys (T003) before `MainWindowViewModel` builds entries (T008)
- ViewModel logic (T009, T014) before tests (T016)

### Parallel Opportunities

```
Phase 2:  T003 ‖ T004          (Strings.resx and Icons.axaml touch different files)
Phase 3:  T005 ‖ T006          (delete SettingsView ‖ delete SettingsViewModel)
Phase 4:  T009 ‖ T010          (MainWindowViewModel logic ‖ MainWindow.axaml template)
Phase 5:  T011 ‖ T012          (NavigationEntry.cs ‖ MainWindow.axaml chevron)
Phase 6:  T013 ‖ T014          (MainWindow.axaml pipe guard ‖ MainWindowViewModel restore)
Phase 8:  T016 ‖ T017          (update smoke tests ‖ new group tests)
Phase 9:  T018 ‖ T019          (grep orphans ‖ dotnet build)
```

---

## Implementation Strategy

### MVP First (US5 + US1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T004) — CRITICAL
3. Complete Phase 3: US5 — Remove tabbed settings (T005–T007)
4. Complete Phase 4: US1 — Wire direct navigation (T008–T010)
5. **STOP and VALIDATE**: Click each of the 5 child items, confirm correct views load, confirm no tab strip
6. Proceed to Phase 5–9 for collapse/expand, highlighting, and tests

### Incremental Delivery

1. Foundation + US5 + US1 → **Settings sub-pages are one-click accessible (MVP!)**
2. Add US2 → Group collapses and expands cleanly
3. Add US3 → Active item always highlighted correctly
4. Add US4 (verification only) → Confirm session state preserved
5. Add Tests → Constitution CA-006 gate satisfied
6. Polish → Clean build, zero orphaned references

---

## Summary

| Phase | Stories | Tasks | Parallelizable |
|-------|---------|-------|----------------|
| 1 – Setup | — | T001 | — |
| 2 – Foundational | — | T002–T004 | T003 ‖ T004 |
| 3 – US5 (P1) | Remove tabbed view | T005–T007 | T005 ‖ T006 |
| 4 – US1 (P1) 🎯 | Direct navigation | T008–T010 | T009 ‖ T010 |
| 5 – US2 (P2) | Collapse/Expand | T011–T012 | T011 ‖ T012 |
| 6 – US3 (P2) | Active highlighting | T013–T014 | T013 ‖ T014 |
| 7 – US4 (P3) | Session persistence | T015 | — |
| 8 – Tests | CA-006 | T016–T017 | T016 ‖ T017 |
| 9 – Polish | Cross-cutting | T018–T020 | T018 ‖ T019 |

**Total tasks**: 20  
**Parallel pairs**: 8  
**Modified files**: ~10 (`NavigationEntry.cs`, `MainWindowViewModel.cs`, `MainWindow.axaml`, `CompositionRoot.cs`, `Strings.resx`, `Icons.axaml`, `MainWindowViewModelSmokeTests.cs`)  
**Deleted files**: 3 (`SettingsView.axaml`, `SettingsView.axaml.cs`, `SettingsViewModel.cs`)  
**New files**: 1 (`NavigationEntryGroupTests.cs`)  
**Suggested MVP scope**: Phase 1 + Phase 2 + Phase 3 (US5) + Phase 4 (US1) = T001–T010

---

## Notes

- `[P]` tasks touch different files — safe to run in parallel within the same phase
- `[US#]` labels map to user stories in `spec.md` for traceability
- `ViewLocator` is intentionally unchanged — the convention `ProfileSettingsViewModel → ProfileSettingsView` already works
- `AppearanceSettingsViewModel` maps to the "Language" child item (carries forward the Feature 035 rename)
- The `IsGroup` guard on active pipe (T013) is the key visual correctness gate — without it the group header would flash highlighted on click before `_lastContentEntry` restore fires
- Singleton sub-VM lifetime (T007) is the structural foundation for US4 — no additional persistence code is needed
