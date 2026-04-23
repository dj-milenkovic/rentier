# Research: Language Selection

## R1: Reactive Localization Strategy in Avalonia

**Decision**: Hybrid approach — `.resx` files for string storage + reactive `ILocalizationService` singleton with `INotifyPropertyChanged` indexer for AXAML bindings.

**Rationale**:
- The existing codebase already uses `Strings.resx` with ~125 `{x:Static res:Strings.Key}` bindings across 11 AXAML views.
- `{x:Static}` evaluates once at control load time — it cannot support runtime culture switching.
- A reactive localizer service wrapping `ResourceManager` with an indexer `this[string key]` and `PropertyChanged("")` notification causes all `{Binding [Key]}` bindings to re-evaluate instantly when the culture changes.
- This preserves compile-time key safety via the generated `Strings.Designer.cs` class for code-behind references while enabling hot-swap in XAML.
- The constitution mandates "User-visible strings MUST be in `Resources/Strings.resx`" — this approach honors that rule.

**Alternatives considered**:
1. **AXAML DynamicResource dictionaries** — rejected because no compile-time key verification, diverges from existing .resx convention, and duplicate key management across dictionaries.
2. **JSON files per language** — rejected because non-standard for .NET, no IDE completion, requires custom loader.
3. **Full DynamicResource migration** — rejected because losing compile-time safety and existing investment in typed `Strings.` accessors.

## R2: AXAML Binding Migration Strategy

**Decision**: Bulk one-shot refactor — all `{x:Static res:Strings.Key}` bindings replaced with `{Binding [Key], Source={StaticResource Localizer}}` in a single PR.

**Rationale**:
- There are ~125 `{x:Static}` binding sites across 11 AXAML files. The migration is mechanical (search-and-replace pattern).
- A gradual migration would leave some strings unable to switch at runtime, creating an inconsistent UX — some labels switch language while others stay frozen.
- The spec requires FR-002: "all user-visible strings across the entire application immediately upon selection".
- Doing it in one PR avoids intermediate broken states and simplifies testing.

**Alternatives considered**:
1. **Gradual view-by-view migration** — rejected because inconsistent UX during transition (some strings switch, others don't).
2. **Markup extension wrapper** — adds complexity without benefit; the `{Binding [Key]}` pattern is standard Avalonia and well-documented.

## R3: UserPreference Storage Model

**Decision**: New dedicated `UserPreferences` table — `(Key TEXT PK, Value TEXT)` — reusable for all future user-level preferences.

**Rationale**:
- The existing ThemeService uses a JSON file (`ui.json`) for theme persistence, which is outside the SQLite database.
- The spec explicitly requires SQLite persistence for the language preference.
- A generic KV table allows future preferences (date format, number format, etc.) without schema changes.
- The `UserPreference` entity is a simple Domain entity with key validation.
- EF Core configuration is straightforward: single table, text PK, text value.

**Alternatives considered**:
1. **JSON file like ThemeService** — rejected because spec requires SQLite and the constitution mandates local SQLite storage.
2. **Typed columns per preference** — rejected because every new preference requires a migration; the KV approach is more extensible.
3. **Storing in TaxpayerProfile** — rejected because language is not taxpayer-related; violates single responsibility.

## R4: Localizer Service DI Lifetime

**Decision**: `ILocalizationService` registered as a **singleton** in the DI container.

**Rationale**:
- The localizer must maintain a single shared culture state across all ViewModels and views.
- If transient, each ViewModel would get its own instance with potentially stale culture state.
- Matches the `IThemeService` pattern which is already a singleton.
- The `PropertyChanged("")` notification from a singleton reaches all bound views simultaneously.
- Thread-safe: culture changes happen on UI thread; `ResourceManager` is thread-safe for reads.

**Alternatives considered**:
1. **Transient** — rejected because multiple instances create culture-state divergence.
2. **Scoped** — N/A in desktop app (no request scope).

## R5: Missing Translation Key Fallback

**Decision**: Fall back to the **Serbian string** (the app default locale).

**Rationale**:
- Serbian is the primary/default locale for the target audience (Serbian taxpayers).
- .NET `ResourceManager` already has built-in fallback: if a key is missing in `Strings.sr-Latn.resx`, it falls back to the neutral `Strings.resx`.
- Since `Strings.resx` currently contains English and will remain the "neutral" resource, we need to set the default culture appropriately.
- **Implementation**: `Strings.resx` becomes the Serbian (default) resource. `Strings.en.resx` is created for English. This way the .NET fallback chain naturally falls back to Serbian.
- **Alternative approach**: Keep `Strings.resx` as English (current state), create `Strings.sr-Latn.resx` for Serbian. The `LocalizationService.this[key]` indexer explicitly falls back to Serbian if the key returns null/empty for the current culture.

**Final decision**: Keep `Strings.resx` as English (preserving existing investment and `Strings.Designer.cs` references), create `Strings.sr-Latn.resx` for Serbian translations. The `LocalizationService` indexer implements explicit fallback: if `ResourceManager.GetString(key, currentCulture)` returns null, fall back to `ResourceManager.GetString(key, new CultureInfo("sr-Latn"))`. Since the neutral resource is English, and Serbian is the app default, the fallback ensures Serbian is shown for any missing key in the Serbian resource, and English is always complete (it IS the neutral resource).

**Wait — correction**: The spec says "fall back to Serbian". Since `Strings.resx` is English (neutral/default), the `.resx` fallback chain would fall back to English, not Serbian. To honor the spec:
- The `LocalizationService` indexer must explicitly handle fallback: try current culture → try Serbian → fall back to neutral (English).
- In practice, since both locales will have complete translations, this only matters for edge cases (e.g., a key added to English but not yet to Serbian).

**Alternatives considered**:
1. **Show raw key name** — rejected; spec explicitly requires SR-009.
2. **Show empty string** — rejected; worse UX than fallback.
3. **Reorganize .resx files to make Serbian the neutral resource** — rejected because it would break all existing `Strings.Designer.cs` references and code-behind usage that assumes English.

## R6: Language Section Placement in Settings

**Decision**: Add Language section to the existing **Appearance tab** alongside theme selection.

**Rationale**:
- The spec says "either as part of the Appearance tab or as a dedicated new tab. The final placement is a UX decision."
- The Appearance tab currently has only theme selection (3 radio buttons) — it has room.
- Language and theme are both "look & feel" settings — grouping them is a common UX pattern.
- Adding a new tab increases sidebar/tab navigation complexity for a single-purpose control.
- The `AppearanceSettingsView.axaml` already has a card-based layout that can accommodate a second card.

**Alternatives considered**:
1. **Dedicated Language tab** — rejected because over-engineering for two radio buttons.
2. **General / Preferences tab** — rejected because no such tab exists and creating one fragments settings unnecessarily.

## R7: Navigation Label Reactivity

**Decision**: `NavigationEntry.Label` must become a reactive `string` property (not a `record` positional parameter) that subscribes to `ILocalizationService` culture changes.

**Rationale**:
- Currently `NavigationEntry` is an immutable `record` with a `string Label` positional parameter. Labels are set once from `Strings.Nav_*` in the `MainWindowViewModel` constructor.
- For runtime language switching, sidebar labels must update when culture changes.
- Two approaches:
  1. Make `NavigationEntry` mutable with `INotifyPropertyChanged` on `Label` — subscribe to `ILocalizationService.CultureChanged`.
  2. Keep `NavigationEntry` immutable; rebuild the `NavigationEntries` list on culture change.
- **Chosen**: Option 1 — convert `NavigationEntry` to a class implementing `ReactiveObject` with a reactive `Label` property. The `MainWindowViewModel` subscribes to `ILocalizationService.CultureChanged` and updates each entry's `Label`.

**Alternatives considered**:
1. **Rebuild NavigationEntries list** — rejected because it would break `SelectedEntry` binding and require re-wiring all navigation closures on every language switch.
