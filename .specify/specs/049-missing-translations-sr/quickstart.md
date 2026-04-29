# Quickstart: Missing Serbian Translations Audit & Fix

**Feature**: 049-missing-translations-sr

## Prerequisites

- .NET 8 SDK
- The Rentier solution builds: `dotnet build Rentier.slnx`

## What This Feature Changes

This is a **Desktop-layer-only** change that:
1. Adds ~12 new resource keys (English + Serbian) for previously hardcoded strings
2. Moves hardcoded English text from AXAML views to the localization system
3. Fixes `ReportStatus.PartialError` enum display (was falling through to `.ToString()`)
4. Adds ViewModel-computed localized format strings for the update notification bar

## Files to Modify

### Resource files (add new keys)
- `src/Rentier.Desktop/Resources/Strings.resx` — add 12 English keys
- `src/Rentier.Desktop/Resources/Strings.Designer.cs` — regenerated from .resx
- `src/Rentier.Desktop/Resources/SrLatnStrings.cs` — add 12 Serbian keys

### AXAML views (replace hardcoded text with bindings)
- `src/Rentier.Desktop/Views/MainWindow.axaml` — update notification bar (9 strings)
- `src/Rentier.Desktop/Views/HolidaySettingsView.axaml` — empty state message (1 string)
- `src/Rentier.Desktop/Views/ReportsView.axaml` — "Actions" column header (1 string)

### Converters (fix missing case)
- `src/Rentier.Desktop/Converters/ReportStatusDisplayConverter.cs` — add PartialError

### ViewModels (add localized format properties)
- `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs` — add computed properties for update bar format strings

### Tests
- `tests/Rentier.UnitTests/Desktop/LocalizationServiceTests.cs` — add parity test
- `tests/Rentier.UnitTests/Desktop/MainWindowViewModel_UpdateTests.cs` — update for new properties

## How to Verify

```bash
# Build
dotnet build Rentier.slnx

# Run tests
dotnet test Rentier.slnx

# Manual verification
# 1. Launch app (defaults to sr-Latn)
# 2. Navigate through all pages — no English text should appear
# 3. Switch to English locale in Settings > Appearance — all English text should appear
# 4. Switch back to Serbian — all Serbian text should appear
```

## Key Patterns to Follow

### Adding a resource key (English)
In `Strings.resx`, add a `<data>` entry:
```xml
<data name="Update_Now_Button" xml:space="preserve">
  <value>Update Now</value>
</data>
```

### Adding a Serbian translation
In `SrLatnStrings.cs`, add to the dictionary:
```csharp
["Update_Now_Button"] = "Ažuriraj sada",
```

### Binding localized text in AXAML
```xml
<Button Content="{Binding [Update_Now_Button], Source={StaticResource Localizer}}" />
```

### Format strings (use ViewModel, not StringFormat)
```csharp
// In ViewModel
public string UpdateAvailableText => string.Format(
    _localizationService["Update_Available_Format"], AvailableVersion);
```
