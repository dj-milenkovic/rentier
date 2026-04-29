# Implementation Plan: Missing Serbian Translations Audit & Fix

**Branch**: `049-missing-translations-sr` | **Date**: 2025-07-15 | **Spec**: `.specify/specs/049-missing-translations-sr/spec.md`
**Input**: Feature specification from `.specify/specs/049-missing-translations-sr/spec.md`

## Summary

Audit and fix all missing Serbian (Latin script) translations across the Rentier desktop application. The primary issues are: (1) ~11 hardcoded English strings in AXAML views (update notification bar, holiday empty state, reports actions column), (2) one missing enum display mapping (`ReportStatus.PartialError`), and (3) AXAML `StringFormat` patterns that bypass the localization system. All fixes are confined to the Desktop layer — no Domain, Application, or Infrastructure changes.

## Technical Context

**Language/Version**: C# 12 / .NET 8
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, FluentTheme
**Storage**: N/A (no data model changes)
**Testing**: xUnit + FluentAssertions
**Target Platform**: Windows + macOS (cross-platform desktop)
**Project Type**: Desktop application (Avalonia)
**Performance Goals**: N/A (translation lookups are synchronous in-memory dictionary reads)
**Constraints**: Desktop-layer only; no changes to Domain, Application, or Infrastructure
**Scale/Scope**: ~12 new resource keys, 3 AXAML files modified, 1 converter fixed, 1 ViewModel updated

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only). **All changes are in Rentier.Desktop only.**
- [x] All monetary/rate/percentage values are modeled as `decimal`. **N/A — no monetary changes.**
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified. **N/A — no date changes.**
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry. **N/A — translation strings contain no sensitive data.**
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified. **N/A — no network calls.**
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow. **N/A — translation lookups are synchronous in-memory reads.**
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%). **No Domain/Application changes. Desktop parity test added.**
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`. **Mapped to 049-missing-translations-sr.**

## Project Structure

### Documentation (this feature)

```text
.specify/specs/049-missing-translations-sr/
├── plan.md              # This file
├── research.md          # Phase 0: localization system analysis, hardcoded string audit
├── data-model.md        # Phase 1: new resource keys and translations
├── quickstart.md        # Phase 1: developer guide for this feature
├── contracts/
│   └── localization-contract.md  # Phase 1: key parity and enum display contracts
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/Rentier.Desktop/
├── Resources/
│   ├── Strings.resx                          # +12 new English keys
│   ├── Strings.Designer.cs                   # Regenerated from .resx
│   └── SrLatnStrings.cs                      # +12 new Serbian keys
├── Views/
│   ├── MainWindow.axaml                      # Update bar: hardcoded → localized bindings
│   ├── HolidaySettingsView.axaml             # Empty state: hardcoded → localized binding
│   └── ReportsView.axaml                     # Actions column: hardcoded → localized binding
├── Converters/
│   └── ReportStatusDisplayConverter.cs       # Add PartialError case
└── ViewModels/
    └── MainWindowViewModel.cs                # Add localized format string properties

tests/Rentier.UnitTests/Desktop/
├── LocalizationServiceTests.cs               # Add comprehensive parity test
└── MainWindowViewModel_UpdateTests.cs        # Update for new localized properties
```

**Structure Decision**: All changes are within existing `Rentier.Desktop` project and its test project. No new projects or directories needed.

## Detailed Change Plan

### Work Package 1: Resource Keys (P1 — Foundation)

**Goal**: Add all missing resource keys to both English and Serbian dictionaries.

**Changes**:
1. **`Strings.resx`** — Add 12 new `<data>` entries with English values
2. **`Strings.Designer.cs`** — Regenerate from .resx (or manually add matching static properties)
3. **`SrLatnStrings.cs`** — Add 12 new dictionary entries with Serbian translations

**New keys** (see `data-model.md` for complete table):
- `Update_Available_Format`, `Update_Now_Button`, `Update_Later_Button`
- `Update_Downloading_Format`, `Update_Ready_Text`, `Update_RestartNow_Button`
- `Update_Failed_Format`, `Update_Retry_Button`, `Update_Dismiss_Button`
- `Holidays_Empty_Text`, `Reports_Col_Actions`, `ReportStatus_PartialError`

**Acceptance**: All 12 keys exist in both English and Serbian with correct values.

### Work Package 2: Fix ReportStatus Converter (P1)

**Goal**: Ensure all `ReportStatus` enum values have explicit localized mappings.

**Changes**:
1. **`ReportStatusDisplayConverter.cs`** — Add `ReportStatus.PartialError => Strings.ReportStatus_PartialError` to the switch expression

**Acceptance**: No enum value falls through to `.ToString()`.

### Work Package 3: Localize Update Notification Bar (P1)

**Goal**: Replace all hardcoded English in the update notification bar with localized bindings.

**Changes**:
1. **`MainWindowViewModel.cs`** — Add reactive computed properties:
   - `UpdateAvailableText` — `string.Format(localizer["Update_Available_Format"], AvailableVersion)`
   - `DownloadingText` — `string.Format(localizer["Update_Downloading_Format"], DownloadProgress)`
   - `UpdateFailedText` — `string.Format(localizer["Update_Failed_Format"], UpdateErrorMessage)`
2. **`MainWindow.axaml`** — Replace:
   - `StringFormat='Update v{0} available'` → `Text="{Binding UpdateAvailableText}"`
   - `Content="Update Now"` → `Content="{Binding [Update_Now_Button], Source={StaticResource Localizer}}"`
   - `Content="Later"` → `Content="{Binding [Update_Later_Button], Source={StaticResource Localizer}}"`
   - `StringFormat='Downloading update... {0}%'` → `Text="{Binding DownloadingText}"`
   - `Text="Update ready. Restart to apply."` → `Text="{Binding [Update_Ready_Text], Source={StaticResource Localizer}}"`
   - `Content="Restart Now"` → `Content="{Binding [Update_RestartNow_Button], Source={StaticResource Localizer}}"`
   - `StringFormat='Update failed: {0}'` → `Text="{Binding UpdateFailedText}"`
   - `Content="Retry"` → `Content="{Binding [Update_Retry_Button], Source={StaticResource Localizer}}"`
   - `Content="Dismiss"` → `Content="{Binding [Update_Dismiss_Button], Source={StaticResource Localizer}}"`

**Acceptance**: Update bar shows Serbian text in sr-Latn locale across all states (available, downloading, ready, error).

### Work Package 4: Localize Remaining Hardcoded Strings (P1)

**Goal**: Replace remaining hardcoded English in other views.

**Changes**:
1. **`HolidaySettingsView.axaml`** — Replace:
   - `Text="No holidays configured..."` → `Text="{Binding [Holidays_Empty_Text], Source={StaticResource Localizer}}"`
2. **`ReportsView.axaml`** — Replace:
   - `Header="Actions"` → `Header="{Binding [Reports_Col_Actions], Source={StaticResource Localizer}}"`

**Acceptance**: No hardcoded English visible on Holidays or Reports pages.

### Work Package 5: Parity Test (P2)

**Goal**: Ensure all English keys have Serbian translations and vice versa.

**Changes**:
1. **`LocalizationServiceTests.cs`** — Add test:
   ```csharp
   [Fact]
   public void TranslationParity_AllEnglishKeysHaveSerbianTranslations()
   {
       // Get English keys from Strings.Designer.cs via reflection
       // Get Serbian keys from SrLatnStrings.All
       // Assert sets are equal
   }
   ```

**Acceptance**: Test passes with zero missing keys in either direction.

### Work Package 6: Update ViewModel Tests (P2)

**Goal**: Update existing MainWindowViewModel tests for new localized properties.

**Changes**:
1. **`MainWindowViewModel_UpdateTests.cs`** — Add/update tests for:
   - `UpdateAvailableText` returns formatted Serbian when version is set
   - `DownloadingText` returns formatted Serbian when progress changes
   - `UpdateFailedText` returns formatted Serbian when error occurs

**Acceptance**: All existing tests pass; new tests cover localized format string properties.

## Complexity Tracking

> No Constitution Check violations — no justifications needed.

| Aspect | Assessment |
|--------|-----------|
| Architecture impact | None — Desktop layer only |
| New dependencies | None |
| Migration needed | None |
| Risk | Low — additive resource changes only, no key renames |
