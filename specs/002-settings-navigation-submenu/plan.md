# Implementation Plan: Settings Navigation Sub-menu Items in Sidebar

**Branch**: `036-settings-navigation-submenu` | **Date**: 2025-07-15 | **Spec**: [spec.md](../../.specify/specs/036-settings-navigation-submenu/spec.md)
**Input**: Feature specification from `.specify/specs/036-settings-navigation-submenu/spec.md`

## Summary

Replace the single "Settings" sidebar navigation item (which opens a `TabControl`-based `SettingsView`) with a collapsible Settings group containing five child navigation items — Profile, Holidays, Mailboxes, Importers, and Language — each routing directly to its standalone settings view. This is a pure Desktop-layer navigation and presentation refactor with no Domain, Application, or Infrastructure changes.

**Technical approach**: Extend the `NavigationEntry` model to support a hierarchical group concept with `IsGroup`, `IsExpanded`, and `Children` properties. Replace the flat `List<NavigationEntry>` in `MainWindowViewModel` with a structure that includes a Settings group entry plus five child entries. Update the `MainWindow.axaml` sidebar `ListBox` `ItemTemplate` to render group headers (with chevron toggle) and indented child items. Remove the `SettingsView`/`SettingsViewModel` tab container. Each child entry directly references its existing sub-ViewModel (e.g., `ProfileSettingsViewModel`), and the existing `ViewLocator` convention-based resolution automatically maps to the correct View.

## Technical Context

**Language/Version**: C# 14 / .NET 10.0
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, CommunityToolkit.Mvvm, FluentTheme
**Storage**: N/A — no data model changes; navigation state is in-memory only
**Testing**: xUnit + FluentAssertions + NSubstitute + Avalonia.Headless.XUnit
**Target Platform**: Windows + macOS (cross-platform desktop via Avalonia)
**Project Type**: Desktop application (Clean Architecture, MVVM)
**Performance Goals**: Settings group expand/collapse < 0.5 s with no visible rendering lag (SC-002)
**Constraints**: Pure UI refactor — no Domain/Application/Infrastructure changes (FR-011)
**Scale/Scope**: 5 settings sub-pages, 1 collapsible group, ~10 modified files, ~4 new files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - ✅ All changes are in `Rentier.Desktop` (Views, ViewModels, AXAML styles, resources). No Application, Domain, or Infrastructure code is modified.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - ✅ N/A — no monetary values introduced or modified.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - ✅ N/A — no date values introduced or modified.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - ✅ N/A — navigation state is in-memory only, no disk persistence, no new data storage.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - ✅ N/A — no new network calls introduced.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - ✅ Navigation transitions are synchronous property updates (ViewModel swapping via reactive binding). Individual settings views retain their existing async patterns unchanged.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - ✅ No Domain or Application test changes needed. Desktop ViewModel tests require updates for: group navigation, collapse/expand state, active child highlighting, localization label updates.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - ✅ Feature 036 spec exists at `.specify/specs/036-settings-navigation-submenu/spec.md`.

## Project Structure

### Documentation (this feature)

```text
specs/002-settings-navigation-submenu/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Rentier.Desktop/
├── ViewModels/
│   ├── NavigationEntry.cs            # MODIFY — add IsGroup, IsExpanded, Children, IndentLevel
│   ├── MainWindowViewModel.cs        # MODIFY — replace flat Settings entry with group + children
│   ├── SettingsViewModel.cs          # DELETE — tab container no longer needed
│   ├── ProfileSettingsViewModel.cs   # UNCHANGED
│   ├── HolidaySettingsViewModel.cs   # UNCHANGED
│   ├── MailboxSettingsViewModel.cs   # UNCHANGED
│   ├── ImporterSettingsViewModel.cs  # UNCHANGED
│   └── AppearanceSettingsViewModel.cs # UNCHANGED
├── Views/
│   ├── MainWindow.axaml              # MODIFY — sidebar ItemTemplate with group/child rendering
│   ├── SettingsView.axaml            # DELETE — tab container no longer needed
│   ├── SettingsView.axaml.cs         # DELETE — tab container code-behind
│   ├── ProfileSettingsView.axaml     # UNCHANGED
│   ├── HolidaySettingsView.axaml     # UNCHANGED
│   ├── MailboxSettingsView.axaml     # UNCHANGED
│   ├── ImporterSettingsView.axaml    # UNCHANGED
│   └── AppearanceSettingsView.axaml  # UNCHANGED
├── Assets/
│   ├── Icons.axaml                   # MODIFY — add chevron icons for expand/collapse
│   └── Styles/
│       └── Controls.axaml            # MODIFY — add sidebar-nav styles for group headers and child indent
├── Resources/
│   └── Strings.resx                  # MODIFY — add Nav_Settings_Profile, Nav_Settings_Holidays, etc.
├── Composition/
│   └── CompositionRoot.cs            # MODIFY — remove SettingsViewModel registration, register child VMs as needed
└── Services/
    └── ViewLocator.cs                # UNCHANGED — convention already maps *ViewModel → *View

tests/Rentier.UnitTests/
└── Desktop/
    └── MainWindowViewModelTests.cs   # MODIFY — update for group navigation, add collapse/expand tests
```

**Structure Decision**: Existing Clean Architecture project layout. All changes scoped to `Rentier.Desktop` project and its test file. No new projects or layers introduced.

## Complexity Tracking

> No constitution violations. All changes confined to Desktop layer.
