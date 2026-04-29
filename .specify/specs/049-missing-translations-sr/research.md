# Research: Missing Serbian Translations Audit & Fix

**Feature**: 049-missing-translations-sr
**Date**: 2025-07-15

## R-001: Current Localization System Architecture

**Decision**: Reuse existing `LocalizationService` + static dictionary pattern.

**Findings**:
- English strings: `Strings.resx` → `Strings.Designer.cs` (243 keys, auto-generated)
- Serbian strings: `SrLatnStrings.cs` (243 keys, manual static dictionary)
- `LocalizationService` implements `ILocalizationService` with indexer `this[string key]`
- Fallback chain: current culture → sr-Latn → English → `[key]`
- Default culture is `sr-Latn`
- AXAML binding pattern: `{Binding [Key], Source={StaticResource Localizer}}`

**Rationale**: The system is well-established, covers 243 keys, and supports the fallback chain. No reason to introduce a new framework.

**Alternatives considered**: .resx per-culture files (rejected — the project already chose static dictionaries for Serbian; switching would be a larger refactor with no benefit).

## R-002: Identified Hardcoded English Strings in AXAML

**Decision**: Move all hardcoded strings to the resource system.

**Findings** (10 hardcoded English strings across 3 files):

| File | Line(s) | Hardcoded Text | Proposed Key |
|------|---------|---------------|-------------|
| `MainWindow.axaml` | 158 | `Update v{0} available` (StringFormat) | `Update_Available_Format` |
| `MainWindow.axaml` | 161 | `Update Now` | `Update_Now_Button` |
| `MainWindow.axaml` | 164 | `Later` | `Update_Later_Button` |
| `MainWindow.axaml` | 175 | `Downloading update... {0}%` (StringFormat) | `Update_Downloading_Format` |
| `MainWindow.axaml` | 192 | `Update ready. Restart to apply.` | `Update_Ready_Text` |
| `MainWindow.axaml` | 195 | `Restart Now` | `Update_RestartNow_Button` |
| `MainWindow.axaml` | 209 | `Update failed: {0}` (StringFormat) | `Update_Failed_Format` |
| `MainWindow.axaml` | 212 | `Retry` | `Update_Retry_Button` |
| `MainWindow.axaml` | 214 | `Dismiss` | `Update_Dismiss_Button` |
| `HolidaySettingsView.axaml` | 54 | `No holidays configured. Click Add or Fetch from web to get started.` | `Holidays_Empty_Text` |
| `ReportsView.axaml` | 140 | `Actions` | `Reports_Col_Actions` |

**Note**: `AppearanceSettingsView.axaml` has `English` and `Srpski` as language option labels — these are intentionally the native name of each language and should NOT be localized.

**Note**: `ManualFilingView.axaml` placeholders (`e.g. AAPL`, `e.g. 100.00`, `e.g. 85.00`) are format examples; these are language-neutral and do NOT need translation.

**Rationale**: Moving to resource keys follows the constitution requirement that "user-visible strings MUST be in `Resources/Strings.resx`".

## R-003: Missing Enum Translation — ReportStatus.PartialError

**Decision**: Add `ReportStatus_PartialError` key and update converter.

**Findings**:
- `ReportStatus` enum has 4 values: `Init`, `Processed`, `Error`, `PartialError`
- `ReportStatusDisplayConverter` only maps 3 values; `PartialError` falls through to `s.ToString()` → displays "PartialError" in English
- Neither `Strings.Designer.cs` nor `SrLatnStrings.cs` has a `ReportStatus_PartialError` key

**Rationale**: Converter must map all enum values explicitly to avoid English fallthrough.

## R-004: StringFormat Localization Pattern for Update Bar

**Decision**: Move StringFormat text to ViewModel-computed properties using `ILocalizationService`.

**Findings**:
- AXAML `StringFormat` embeds the format string directly in XAML, bypassing the `Localizer` resource
- For localized format strings, the standard Rentier pattern is to compute the formatted string in the ViewModel using `string.Format(localizationService["Key"], args)` and expose it as a reactive property
- Example precedent: `Sync_Complete` key uses `{0}` and `{1}` placeholders and is formatted in `SyncViewModel`

**Rationale**: Keeps all user-visible strings in the localization system; ViewModel formatting is the established pattern.

**Alternatives considered**: MultiBinding with StringFormat (rejected — doesn't support dynamic format strings from the localizer).

## R-005: Key Parity Status

**Decision**: After adding ~12 new keys, both dictionaries must remain in sync.

**Findings**:
- Current state: 243 English keys = 243 Serbian keys ✓
- After this feature: ~255 keys expected (11 new hardcoded strings + 1 PartialError enum)
- A parity test already exists in `LocalizationServiceTests.cs` but only tests specific keys, not comprehensive parity

**Rationale**: A dedicated parity test ensures no future regressions.
