# Feature Specification: Language Selection

**Feature Branch**: `035-language-selection`  
**Created**: 2025-07-23  
**Status**: Draft  
**Input**: User description: "The user can choose the application display language — English or Serbian (Latin) — from a Settings sub-view. The selected language is persisted in SQLite and applied immediately across the entire UI without requiring an app restart. All visible strings switch to the chosen locale."

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Switch Application Language from Settings (Priority: P1)

A user opens Settings and navigates to the Language section. The selector displays two language options. The user picks a different language from the one currently active. All visible labels, headers, buttons, tooltips, validation messages, and navigation items across the entire application instantly update to the selected language — without restarting the app.

**Why this priority**: This is the core value of the feature. Without instant language switching, there is no feature at all. Everything else (persistence, defaults) is secondary to the live switch experience.

**Independent Test**: Can be fully tested by opening Settings → Language, selecting "English" (when Serbian is active), and visually confirming that all sidebar navigation labels, Settings tab headers, form labels, button text, and column headers across Dashboard, Filings, Reports, Sync, and Settings views switch to English immediately.

**Acceptance Scenarios**:

1. **Given** the application is running with Serbian active, **When** the user opens Settings and navigates to the Language section, **Then** the language selector is visible showing "Srpski" as the current selection and "English" as the alternative.
2. **Given** the language selector shows "Srpski" as the current language, **When** the user selects "English", **Then** every user-visible string in the application — sidebar labels (e.g., "Podešavanja" → "Settings"), tab headers, form labels, button text, dialog prompts, placeholder text, validation messages, empty-state messages, and column headers — updates to English immediately without any flicker, reload, or restart.
3. **Given** the user has switched to English, **When** the user navigates to other views (Dashboard, Filings, Reports, Sync) and returns to Settings, **Then** all views consistently display strings in English.

---

### User Story 2 — Language Preference Survives App Restart (Priority: P1)

A user selects their preferred language and closes the application. When they reopen the app later, the entire UI loads in the previously selected language from the first screen onward.

**Why this priority**: Without persistence, users must re-select their language every session. This is equally critical to the switch itself because it makes the choice permanent.

**Independent Test**: Can be fully tested by selecting English, closing the application, reopening it, and confirming the splash/initial view, sidebar, and all subsequent views display in English from the moment they appear.

**Acceptance Scenarios**:

1. **Given** the user selected English as their language, **When** the user closes and reopens the application, **Then** the application loads with all strings in English from the first visible frame.
2. **Given** the application is opened for the first time (fresh install, no prior preference stored), **When** the main window appears, **Then** the application displays all strings in Serbian (the default language).
3. **Given** the user selected English, closed the app, and the SQLite database file exists on disk, **When** the app starts, **Then** the persisted language preference is read before any view is rendered, and the correct language is applied from startup.

---

### User Story 3 — Language Section Appears in Settings with Clear Options (Priority: P2)

A user who wants to change their language opens the Settings area and can easily locate the Language section. The two language options are clearly labelled and the current selection is visually indicated.

**Why this priority**: Discoverability is important but secondary to the actual switching mechanism. Without this, power users could still benefit from a default; with it, all users can self-serve.

**Independent Test**: Can be fully tested by opening Settings and verifying the Language section is visible, shows exactly two options with clear labels, and the currently active language is visually distinguished (e.g., selected radio button or highlighted item).

**Acceptance Scenarios**:

1. **Given** the user opens the Settings view, **When** they look for language options, **Then** a dedicated "Language" section is visible within the Settings area (either as a distinct tab or as a section within the Appearance tab).
2. **Given** the Language section is displayed, **When** the user views the available options, **Then** exactly two choices are shown: "English" and "Srpski" (Serbian in Latin script), each labelled in its own language for recognition regardless of the currently active locale.
3. **Given** Serbian is the currently active language, **When** the Language section is displayed, **Then** the "Srpski" option is visually marked as the active selection.

---

### Edge Cases

- What happens when the SQLite database is corrupted or the language preference row is missing? The application falls back to Serbian (the default language) and re-creates the preference record.
- What happens when a string key exists in the primary language file but is missing from the secondary language file? The application displays the fallback language string (Serbian) for that specific key rather than showing a blank or a raw key name.
- What happens if the user rapidly toggles between languages multiple times? Each toggle applies immediately; the last selection wins and is persisted. No queuing, debouncing, or race conditions occur.
- What happens to user-entered data (e.g., a partially filled form) when the language is switched mid-edit? All system-provided labels and placeholders change; user-typed content in text fields remains untouched.
- What happens to open dialogs or confirmation prompts when the language switches? Any currently open dialog updates its system-provided strings to the new language.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a language selection control within the Settings area offering exactly two options: English and Serbian (Latin script).
- **FR-002**: System MUST apply the selected language to all user-visible strings across the entire application immediately upon selection, without requiring a restart, reload, or re-navigation.
- **FR-003**: System MUST persist the selected language preference to the SQLite database so it survives application restarts.
- **FR-004**: System MUST read the persisted language preference during startup and apply it before any view is rendered to the user.
- **FR-005**: System MUST default to Serbian when no language preference has been persisted (fresh install or missing/corrupt preference).
- **FR-006**: System MUST display each language option labelled in its own language — "English" for English and "Srpski" for Serbian — so the user can identify their language regardless of the currently active locale.
- **FR-007**: System MUST cover all user-visible strings with translations in both languages, including but not limited to: sidebar navigation labels, Settings tab headers, form field labels, button text, validation and error messages, placeholder/hint text, empty-state messages, dialog titles and body text, column headers in data grids, and tooltip text.
- **FR-008**: System MUST NOT alter user-entered data (form field values, text input) when the language is switched; only system-provided strings change.
- **FR-009**: System MUST fall back to the Serbian string for any individual key that is missing a translation in the selected language, rather than displaying a blank or a raw key identifier.
- **FR-010**: System MUST update strings in any currently open dialogs or overlays when the language changes, maintaining consistency across all visible surfaces.

### Localization Storage Recommendation

The feature description asked for a best-practices recommendation on where and how to store language key-value pairs. The following analysis considers the project's existing patterns and constraints:

#### Options Evaluated

| Approach | Pros | Cons |
| -------- | ---- | ---- |
| **.resx satellite assemblies** (Strings.resx + Strings.sr-Latn.resx) | Already used for English strings; standard .NET pattern; compile-time key checking via generated class; tooling support in IDEs; works with ResourceManager | `{x:Static}` bindings evaluate once at load time — runtime switching requires replacing the binding strategy with a reactive localizer wrapper |
| **AXAML DynamicResource dictionaries** (one ResourceDictionary per language) | Avalonia-native hot-swap by replacing merged dictionary at runtime; simple `{DynamicResource Key}` bindings | No compile-time key verification; duplicate key management across dictionaries; less IDE support; diverges from existing .resx convention |
| **JSON files** (one .json per language, loaded at runtime) | Easy to edit; human-readable; simple structure | No compile-time safety; no IDE completion for keys; requires custom loader; non-standard for .NET |
| **Hybrid: .resx storage + reactive localizer service** | Keeps compile-time safety and existing .resx investment; uses a reactive service (implementing INotifyPropertyChanged) as an indexer `[key]` binding source in AXAML; changing CultureInfo + raising PropertyChanged triggers all bindings to re-evaluate | Requires refactoring existing `{x:Static}` bindings to `{Binding [Key], Source={StaticResource Localizer}}`; one-time migration cost |

#### Recommendation

The **hybrid approach** (`.resx` files for string storage + a reactive localizer service for runtime binding) is the recommended path because:

1. **Preserves existing investment** — all current strings in `Strings.resx` remain valid and the generated `Strings.Designer.cs` class still works for code-behind references.
2. **Compile-time safety** — missing keys produce build errors, not runtime blanks.
3. **Standard .NET pattern** — satellite assemblies (`Strings.sr-Latn.resx`) are the established .NET mechanism for multi-language support.
4. **Runtime hot-swap** — a reactive localizer service that wraps `ResourceManager` and raises `PropertyChanged("")` when the culture changes causes all `{Binding [Key]}` bindings to re-evaluate instantly.
5. **Constitution alignment** — the constitution mandates "User-visible strings MUST be in `Resources/Strings.resx`", and this approach honours that rule.

The one-time migration cost (changing `{x:Static res:Strings.Key}` bindings to `{Binding [Key], Source={StaticResource Localizer}}`) is bounded and mechanical — each AXAML file's string references are updated with a predictable search-and-replace pattern.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: The language preference entity belongs in Domain. The persistence implementation (EF Core configuration, repository) belongs in Infrastructure. The localizer service and ViewModel changes belong in Desktop. Application layer gains a query/command for reading and writing the preference. All Clean Architecture boundaries remain valid — no layer skips.
- **CA-002 (Money and Dates)**: This feature introduces no monetary or date fields. No `decimal` or `DateOnly` implications.
- **CA-003 (Privacy and Security)**: Language preference is non-sensitive data. It is stored locally in SQLite — consistent with local-first policy. No secrets involved.
- **CA-004 (Network Scope)**: This feature makes zero outbound network calls. No new endpoints.
- **CA-005 (Async and UI)**: Reading the preference on startup must be async. Writing the preference on change must be async. The localizer service notifies the UI thread via `INotifyPropertyChanged` — no blocking. Consistent with `ReactiveCommand.CreateFromTask` pattern used in the existing `AppearanceSettingsViewModel`.
- **CA-006 (Testing Impact)**:
  - **Domain**: Test the language preference entity defaults and validation.
  - **Application**: Test the get/set preference command and query handlers.
  - **Infrastructure**: Integration test the preference repository with SQLite in-memory provider.
  - **Desktop**: Test the `LanguageSettingsViewModel` reactive property changes and that selecting a language triggers the localizer and persistence. Test the localizer service returns correct strings for each culture.

### Key Entities *(include if feature involves data)*

- **UserPreference**: Represents a single user preference as a key-value pair. Key attributes: a unique preference key (e.g., "Language"), and a string value (e.g., "sr-Latn" or "en"). This is a generic preference entity that can be reused for future user-level settings. The language preference is the first use case. Only one record per preference key exists at any time.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can switch the application language and see all visible strings update within 1 second, without any restart or re-navigation.
- **SC-002**: 100% of user-visible strings across all views (Dashboard, Filings, Reports, Sync, Settings including all sub-tabs, dialogs, and empty states) are available in both English and Serbian.
- **SC-003**: The selected language preference persists across application restarts — users who choose English see English from the first frame on every subsequent launch until they change it.
- **SC-004**: On a fresh install with no prior preference, the application defaults to Serbian on first launch.
- **SC-005**: A missing translation for any individual key falls back to Serbian rather than displaying a blank or key identifier — zero untranslated strings are visible to the user.

## Assumptions

- The two supported languages are English (`en`) and Serbian Latin (`sr-Latn`). Cyrillic Serbian and other languages are out of scope for this feature.
- The existing `Strings.resx` file currently contains English strings and will serve as the English locale file. A new `Strings.sr-Latn.resx` file will provide Serbian translations.
- The Language section will be integrated into the existing Settings tab structure — either as part of the Appearance tab (alongside theme selection) or as a dedicated new tab. The final placement is a UX decision to be resolved during planning.
- The application currently uses `{x:Static res:Strings.Key}` bindings for localized strings. These bindings will need to be migrated to a reactive binding pattern to support runtime language switching. This migration is a one-time mechanical change and is included in the scope of this feature.
- User-entered data (form field text, search queries) is never translated or modified when the language switches.
- The `UserPreference` entity is designed as a generic key-value store to allow future preference expansion (e.g., date format, number format) without schema changes. The language preference is its first consumer.
- The existing `ThemeService` stores theme preference in a JSON file (`ui.json`). This feature persists the language preference in SQLite as specified. Migrating the theme preference to SQLite is out of scope but the `UserPreference` entity design accommodates it for a future effort.
- Locale-sensitive data formatting (dates, numbers, currency) is out of scope. This feature only controls which UI strings are displayed. Any future locale-aware formatting is a separate feature.
