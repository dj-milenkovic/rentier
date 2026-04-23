# Implementation Plan: Language Selection

**Branch**: `035-language-selection` | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `.specify/specs/035-language-selection/spec.md`

## Summary

Add runtime language switching (English ↔ Serbian Latin) to the Rentier desktop application. Users select their preferred language from a new Language section in the Settings Appearance tab. The selection is applied instantly across all UI surfaces via a reactive `ILocalizationService` singleton backed by `.resx` satellite assemblies. The preference is persisted in a new `UserPreferences` SQLite table and restored on startup before any view renders. All existing `{x:Static res:Strings.Key}` AXAML bindings are bulk-migrated to `{Binding [Key], Source={StaticResource Localizer}}` to enable hot-swap.

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, EF Core 8 (SQLite), Microsoft.Extensions.DependencyInjection  
**Storage**: SQLite — new `UserPreferences` table (Key TEXT PK, Value TEXT)  
**Testing**: xUnit + FluentAssertions + NSubstitute  
**Target Platform**: Windows + macOS (cross-platform desktop)  
**Project Type**: Desktop application (Avalonia, Clean Architecture)  
**Performance Goals**: Language switch completes in <1 second across all visible strings  
**Constraints**: Local-first (no network), offline-capable, no telemetry  
**Scale/Scope**: 12 AXAML views, ~125 `{x:Static}` binding sites to migrate, 2 locales (en, sr-Latn)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - `UserPreference` entity → Domain
  - `IUserPreferenceRepository` → Application
  - `UserPreferenceRepository` (EF Core) → Infrastructure
  - `ILocalizationService` / `LocalizationService` → Desktop (UI-only concern, same pattern as `IThemeService`)
  - `LanguageSettingsViewModel` → Desktop
  - `GetUserPreferenceQuery` / `SetUserPreferenceCommand` + handlers → Application
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - N/A — this feature introduces no monetary or date fields.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - N/A — no date fields introduced.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - Language preference is non-sensitive, stored locally in SQLite. No secrets, no network.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - Zero outbound network calls.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - Preference read on startup: `async Task`. Preference write on change: `async Task` via `ReactiveCommand.CreateFromTask`. Localizer notifies UI via `INotifyPropertyChanged` — no blocking.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - Domain: `UserPreference` entity validation tests (100% rule coverage).
  - Application: `GetUserPreferenceQueryHandler` and `SetUserPreferenceCommandHandler` tests (≥90%).
  - Infrastructure: `UserPreferenceRepository` integration test with SQLite in-memory.
  - Desktop: `LanguageSettingsViewModel` reactive property tests; `LocalizationService` culture-switch tests.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - Spec: `.specify/specs/035-language-selection/spec.md`

## Project Structure

### Documentation (this feature)

```text
specs/001-language-selection/
├── plan.md              # This file
├── research.md          # Phase 0 — technology decisions
├── data-model.md        # Phase 1 — entity & table design
├── quickstart.md        # Phase 1 — developer onboarding
├── contracts/           # Phase 1 — ILocalizationService contract
│   └── localization-service.md
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Rentier.Domain/
│   └── Entities/
│       └── UserPreference.cs                          # NEW — generic KV preference entity
│
├── Rentier.Application/
│   ├── Commands/
│   │   └── SetUserPreferenceCommand.cs                # NEW
│   ├── Queries/
│   │   └── GetUserPreferenceQuery.cs                  # NEW
│   ├── Handlers/
│   │   ├── SetUserPreferenceCommandHandler.cs         # NEW
│   │   └── GetUserPreferenceQueryHandler.cs           # NEW
│   └── Repositories/
│       └── IUserPreferenceRepository.cs               # NEW — interface
│
├── Rentier.Infrastructure/
│   ├── Persistence/
│   │   ├── AppDbContext.cs                            # MODIFIED — add DbSet<UserPreference>
│   │   ├── Configurations/
│   │   │   └── UserPreferenceConfiguration.cs         # NEW — EF Core fluent config
│   │   └── Migrations/
│   │       └── YYYYMMDD_AddUserPreferences.cs         # NEW — EF migration
│   └── Repositories/
│       └── UserPreferenceRepository.cs                # NEW — EF Core implementation
│
├── Rentier.Desktop/
│   ├── Resources/
│   │   ├── Strings.resx                               # EXISTING — English (default resource)
│   │   └── Strings.sr-Latn.resx                       # NEW — Serbian Latin translations
│   ├── Services/
│   │   ├── ILocalizationService.cs                    # NEW — reactive localizer interface
│   │   └── LocalizationService.cs                     # NEW — ResourceManager + INotifyPropertyChanged
│   ├── Composition/
│   │   └── CompositionRoot.cs                         # MODIFIED — register ILocalizationService singleton
│   ├── App.axaml                                      # MODIFIED — add Localizer static resource
│   ├── App.axaml.cs                                   # MODIFIED — load language pref before UI render
│   ├── ViewModels/
│   │   ├── MainWindowViewModel.cs                     # MODIFIED — subscribe to ILocalizationService for nav labels
│   │   ├── SettingsViewModel.cs                       # MODIFIED — no new tab; language section in AppearanceTab
│   │   └── AppearanceSettingsViewModel.cs             # MODIFIED — add language selection properties
│   └── Views/
│       ├── AppearanceSettingsView.axaml               # MODIFIED — add Language section card
│       ├── MainWindow.axaml                           # MODIFIED — migrate {x:Static} → {Binding}
│       └── [all 11 .axaml files]                      # MODIFIED — bulk migrate {x:Static} → {Binding}

tests/
├── Rentier.UnitTests/
│   ├── Domain/
│   │   └── UserPreferenceTests.cs                     # NEW
│   └── Application/
│       ├── GetUserPreferenceQueryHandlerTests.cs      # NEW
│       └── SetUserPreferenceCommandHandlerTests.cs    # NEW
├── Rentier.Infrastructure.Tests/
│   └── Repositories/
│       └── UserPreferenceRepositoryTests.cs           # NEW
└── Rentier.Scenarios.Tests/
    └── ViewModels/
        └── AppearanceSettingsViewModelTests.cs        # NEW / MODIFIED
```

**Structure Decision**: Follows existing 4-project Clean Architecture layout. No new projects needed. The `UserPreference` entity is placed in Domain alongside existing entities. The `ILocalizationService` is a Desktop-layer UI concern (mirrors `IThemeService` pattern exactly). Language section is added to the existing Appearance tab rather than a new Settings tab to keep the tab count manageable.

## Complexity Tracking

No constitution violations. All design decisions align with existing patterns.
