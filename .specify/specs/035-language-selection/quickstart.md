# Quickstart: Language Selection Feature

## What This Feature Does

Adds runtime language switching (English ↔ Serbian Latin) to Rentier. Users pick their language in Settings → Appearance, it applies instantly across all UI, and the choice persists in SQLite.

## Architecture Overview

```text
┌─────────────────────────────────────────────────────────────────┐
│  Rentier.Desktop                                                │
│  ┌──────────────────────┐  ┌──────────────────────────────────┐ │
│  │ AppearanceSettings   │  │ LocalizationService (singleton)  │ │
│  │ ViewModel            │──│  - wraps ResourceManager         │ │
│  │  - SelectedLanguage  │  │  - INotifyPropertyChanged        │ │
│  │  - auto-persist      │  │  - indexer this[key]             │ │
│  └──────────┬───────────┘  │  - CultureChanged observable    │ │
│             │              └───────────────┬──────────────────┘ │
│             │ SetUserPrefCommand           │ PropertyChanged("") │
│             ▼                              ▼                    │
│  ┌──────────────────────┐  ┌──────────────────────────────────┐ │
│  │ All AXAML Views      │  │ {Binding [Key],                  │ │
│  │ (11 files migrated)  │──│  Source={StaticResource Localizer}}│ │
│  └──────────────────────┘  └──────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────────┐
│  Rentier.Application                                            │
│  ┌─────────────────────────┐  ┌──────────────────────────────┐  │
│  │ SetUserPreference       │  │ GetUserPreference            │  │
│  │ CommandHandler          │  │ QueryHandler                 │  │
│  └───────────┬─────────────┘  └──────────────┬───────────────┘  │
│              │                                │                  │
│              ▼                                ▼                  │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │ IUserPreferenceRepository                                   │ │
│  └─────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────────┐
│  Rentier.Infrastructure                                         │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │ UserPreferenceRepository (EF Core → SQLite)                 │ │
│  │  - GetAsync(key) / SaveAsync(preference)                    │ │
│  └─────────────────────────────────────────────────────────────┘ │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │ AppDbContext + UserPreferenceConfiguration                  │ │
│  │  - DbSet<UserPreference> UserPreferences                    │ │
│  └─────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────────┐
│  Rentier.Domain                                                 │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │ UserPreference (Entity)                                     │ │
│  │  - Key: string (PK)                                         │ │
│  │  - Value: string                                            │ │
│  │  - DDD validation in constructor                            │ │
│  └─────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Key Files to Understand First

| File | Why |
|------|-----|
| `src/Rentier.Desktop/Services/IThemeService.cs` | **Pattern model** — the localizer follows the exact same singleton-service pattern |
| `src/Rentier.Desktop/ViewModels/AppearanceSettingsViewModel.cs` | **Extension point** — language section is added here |
| `src/Rentier.Desktop/Views/AppearanceSettingsView.axaml` | **Extension point** — language card is added here |
| `src/Rentier.Desktop/Resources/Strings.resx` | **String source** — all English strings; Serbian `.resx` mirrors this |
| `src/Rentier.Desktop/App.axaml.cs` | **Startup flow** — language pref loaded before UI render |
| `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs` | **Navigation labels** — must become reactive for sidebar lang switch |

## AXAML Migration Pattern (Bulk Refactor)

Every AXAML file with `{x:Static res:Strings.Key}` is converted:

```xml
<!-- BEFORE -->
<TextBlock Text="{x:Static res:Strings.Nav_Dashboard}" />

<!-- AFTER -->
<TextBlock Text="{Binding [Nav_Dashboard], Source={StaticResource Localizer}}" />
```

**Files affected** (11 views, ~125 binding sites):
- MainWindow.axaml (1 binding)
- DashboardView.axaml (15 bindings)
- FilingsView.axaml (19 bindings)
- ReportsView.axaml (17 bindings)
- SyncView.axaml (6 bindings)
- SettingsView.axaml (4 bindings)
- ProfileSettingsView.axaml (8 bindings)
- HolidaySettingsView.axaml (12 bindings)
- ImporterSettingsView.axaml (13 bindings)
- MailboxSettingsView.axaml (8 bindings)
- ManualFilingView.axaml (22 bindings)

**Namespace changes in AXAML headers**:
```xml
<!-- ADD to each view that uses Localizer -->
xmlns:svc="using:Rentier.Desktop.Services"

<!-- REMOVE from views that no longer need static Strings ref -->
xmlns:res="clr-namespace:Rentier.Desktop.Resources"
```

> **Note**: Keep the `res:` namespace in files where code-behind still references `Strings.Key` directly (e.g., `MainWindowViewModel.cs` uses `Strings.Nav_*` for initial label values).

## Serbian Translation Resource

Create `src/Rentier.Desktop/Resources/Strings.sr-Latn.resx` with identical keys to `Strings.resx`, translated to Serbian Latin. Example:

| Key | English (Strings.resx) | Serbian (Strings.sr-Latn.resx) |
|-----|------------------------|-------------------------------|
| `Nav_Dashboard` | Dashboard | Kontrolna tabla |
| `Nav_Filings` | Filings | Prijave |
| `Nav_Reports` | Reports | Izveštaji |
| `Nav_Sync` | Sync | Sinhronizacija |
| `Nav_Settings` | Settings | Podešavanja |

## Testing Strategy

| Layer | What to Test | Tool |
|-------|-------------|------|
| Domain | `UserPreference` constructor validation, `UpdateValue()` constraints | xUnit + FluentAssertions |
| Application | `GetUserPreferenceQueryHandler` returns value/null, `SetUserPreferenceCommandHandler` upserts | xUnit + NSubstitute |
| Infrastructure | `UserPreferenceRepository` CRUD with SQLite in-memory | xUnit + EF Core InMemory |
| Desktop | `AppearanceSettingsViewModel` language selection triggers localizer + persistence | xUnit + NSubstitute |
| Desktop | `LocalizationService` indexer returns correct strings per culture, fallback works | xUnit |

## Common Pitfalls

1. **Don't forget `Source={StaticResource Localizer}`** on every binding — without it, Avalonia looks for `[Key]` on the ViewModel DataContext.
2. **NavigationEntry labels are set in C# code**, not AXAML — they need explicit `CultureChanged` subscription.
3. **AppearanceSettingsView has hardcoded English strings** ("Appearance", "Theme", etc.) that need migration to localizer bindings too.
4. **The localizer singleton in DI must be the same instance as `Application.Resources["Localizer"]`** — create it once, register it both places.
5. **EF migration must run before language preference is read** — the startup sequence in `App.axaml.cs` already runs migrations before seeding.
