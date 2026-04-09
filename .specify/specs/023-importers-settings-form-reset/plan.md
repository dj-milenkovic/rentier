# Implementation Plan: Importers Settings Form Reset on Save & Navigation

**Branch**: `002-importer-form-reset` | **Date**: 2025-07-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `.specify/specs/023-importers-settings-form-reset/spec.md`

## Summary

Fix the `ImporterSettingsViewModel` so that form fields are always consistent with the selected importer state. The root cause is twofold: (1) after save, the ViewModel re-selects the saved importer by writing to the backing field `_selectedImporter` directly, bypassing the property setter that contains the form population logic — so form fields retain stale user-typed values instead of the freshly persisted DTO values; (2) when `SelectedImporter` is set to `null` (deselect), the property setter has no `else` clause to clear form fields, leaving orphaned data displayed.

The fix extracts two reusable private methods — `PopulateFormFromDto(ImporterDto)` and `ClearForm()` — and calls them from: the `SelectedImporter` setter (populate on non-null, clear on null), the post-save re-selection path, and the post-save "item vanished" path. Existing `OnAddNew()` is refactored to delegate to `ClearForm()`. New ViewModel tests cover all three scenarios across all eight editable fields.

## Technical Context

**Language/Version**: C# 12, .NET 8  
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, CommunityToolkit.Mvvm, EF Core 8 (SQLite)  
**Storage**: SQLite (local-first, no changes needed for this feature)  
**Testing**: xUnit + FluentAssertions + NSubstitute  
**Target Platform**: Windows + macOS (cross-platform desktop)  
**Project Type**: Desktop application (Avalonia)  
**Performance Goals**: UI remains responsive during save/reload; no perceptible delay on form repopulation  
**Constraints**: Offline-capable, local-first data, no telemetry  
**Scale/Scope**: Single-user desktop app, ~8 editable form fields on the affected ViewModel

## Constitution Check (Pre-Design)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **Clean Architecture boundary preserved**: All changes are confined to `Rentier.Desktop` (ViewModel layer). No modifications to Domain, Application, or Infrastructure. The ViewModel consumes existing `ImporterDto` records and existing command/query handlers without any new cross-layer dependencies.
- [x] **Monetary/rate/percentage values as `decimal`**: No monetary fields involved. The importer form fields are strings, enums, and nullable GUIDs. No decimal values affected.
- [x] **Business dates as `DateOnly`**: No date fields on the importer form. Not applicable.
- [x] **Security/privacy constraints**: No changes to data storage, credential handling, or network access. All importer data remains local-first in SQLite.
- [x] **External network usage**: No new network calls introduced. The save and reload paths use existing local repository operations.
- [x] **Async and UI responsiveness**: `OnSaveAsync` is already `async Task`. The post-save `ReloadImportersAsync` is async. Form repopulation (`PopulateFormFromDto`) is synchronous property assignment — appropriate because it's just in-memory DTO field mapping with no I/O.
- [x] **Tests and coverage impact**: New ViewModel unit tests required in `Rentier.Desktop.Tests`. Covers save→re-populate, select-different→populate, deselect→clear across all 8 fields. No Domain or Application test changes needed (no changes in those layers).
- [x] **Feature work mapped to spec task**: This plan traces to spec at `.specify/specs/023-importers-settings-form-reset/spec.md`. Tasks will be generated via `/speckit.tasks`.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/023-importers-settings-form-reset/
├── plan.md              # This file
├── research.md          # Phase 0: root cause analysis and design decisions
├── data-model.md        # Phase 1: form field inventory and state transitions
├── quickstart.md        # Phase 1: implementation quick-start guide
├── checklists/          # Pre-existing checklists
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Rentier.Domain/
│   └── Entities/Importer.cs                          # Entity (NO changes needed)
├── Rentier.Application/
│   ├── DTOs/ImporterDto.cs                            # DTO record (NO changes needed)
│   ├── Commands/UpdateImporterCommand.cs              # Command (NO changes needed)
│   └── Handlers/UpdateImporterCommandHandler.cs       # Handler (NO changes needed)
├── Rentier.Desktop/
│   ├── ViewModels/
│   │   ├── ImporterSettingsViewModel.cs               # PRIMARY CHANGE: form reset fix
│   │   └── ImporterItemViewModel.cs                   # Item VM (NO changes needed)
│   └── Views/
│       └── ImporterSettingsView.axaml                 # View (NO changes needed)
└── Rentier.Infrastructure/                            # (NO changes needed)

tests/
├── Rentier.Desktop.Tests/
│   └── ImporterSettingsViewModelTests.cs              # EXPANDED: new test cases
└── [other test projects unchanged]
```

**Structure Decision**: Clean Architecture four-project layout. All source changes are in `Rentier.Desktop` (presentation layer only). Test changes are in `Rentier.Desktop.Tests`. No structural additions — this is a bugfix within the existing ViewModel.

## Complexity Tracking

> No constitution violations. All changes are confined to the ViewModel presentation layer with no architectural boundary crossings, no new dependencies, and no new external interfaces.

## Constitution Check (Post-Design)

*Re-evaluated after Phase 1 design completion.*

- [x] **Clean Architecture boundary preserved**: Confirmed. `PopulateFormFromDto` and `ClearForm` are private methods on the ViewModel that read from `ImporterDto` (an Application-layer record). No new references added.
- [x] **Monetary values**: Confirmed. No monetary fields touched.
- [x] **Business dates**: Confirmed. No date fields touched.
- [x] **Security/privacy**: Confirmed. No data flow changes.
- [x] **Network usage**: Confirmed. No new network calls.
- [x] **Async/UI**: Confirmed. Form population is synchronous (property assignment only). Async save and reload paths unchanged. `ReactiveCommand.CreateFromTask` pattern preserved.
- [x] **Tests and coverage**: Confirmed. 3+ new test methods covering all 8 fields in save→repopulate, switch→populate, and deselect→clear scenarios.
- [x] **Spec traceability**: Confirmed. Traces to `.specify/specs/023-importers-settings-form-reset/spec.md`.
