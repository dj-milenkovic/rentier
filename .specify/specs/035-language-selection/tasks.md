# Tasks: Language Selection

**Feature**: 035-language-selection  
**Input**: `.specify/specs/035-language-selection/spec.md`, `.specify/specs/035-language-selection/plan.md`  
**Design docs**: `.specify/specs/035-language-selection/` (plan.md, data-model.md, research.md, quickstart.md, contracts/localization-service.md)
**Tests**: Included — required by spec (CA-006) and constitution quality gates

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on each other)
- **[Story]**: Which user story this task belongs to ([US1], [US2], [US3])
- Each task includes an exact file path

---

## Phase 1: Setup

**Purpose**: Orient to the existing codebase and confirm the scope of changes before any code is written.

- [x] T001 Audit all `{x:Static res:Strings.Key}` occurrences across the 11 AXAML views (run: `Select-String -Path "src/Rentier.Desktop/Views/**/*.axaml","src/Rentier.Desktop/MainWindow.axaml" -Pattern "x:Static res:Strings\." -Recurse`) and record count per file to verify the ~125 binding sites listed in quickstart.md
- [x] T002 [P] Read `src/Rentier.Desktop/Services/IThemeService.cs` and `src/Rentier.Desktop/ThemeService.cs` to understand the singleton service pattern that `ILocalizationService` will mirror
- [x] T003 [P] Read `src/Rentier.Desktop/ViewModels/AppearanceSettingsViewModel.cs` to understand the existing Appearance tab structure before extending it with the language section
- [x] T004 [P] Read `src/Rentier.Desktop/NavigationEntry.cs` to understand the current record definition before converting it to a reactive class

**Checkpoint**: Scope confirmed — proceed to foundational infrastructure

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain entity, Application CQRS layer, Infrastructure persistence, and Desktop localizer service — these are shared across all three user stories. No user story work can begin until this phase is complete.

**⚠️ CRITICAL**: All user story phases depend on this phase being complete.

### Domain

- [x] T005 [P] Create `UserPreference` entity with DDD validation (Key/Value constraints, `UpdateValue()` method, private EF constructor) in `src/Rentier.Domain/Entities/UserPreference.cs` — follow data-model.md entity implementation pattern exactly

### Application — Repository Interface & CQRS Records

- [x] T006 [P] Create `IUserPreferenceRepository` interface (`GetAsync(string key, CancellationToken)` returning `Task<UserPreference?>`, `SaveAsync(UserPreference, CancellationToken)` returning `Task`) in `src/Rentier.Application/Repositories/IUserPreferenceRepository.cs`
- [x] T007 [P] Create `GetUserPreferenceQuery` sealed record (`string Key`) in `src/Rentier.Application/Queries/GetUserPreferenceQuery.cs`
- [x] T008 [P] Create `SetUserPreferenceCommand` sealed record (`string Key`, `string Value`) in `src/Rentier.Application/Commands/SetUserPreferenceCommand.cs`

### Application — Handlers (depends on T005–T008)

- [x] T009 Create `GetUserPreferenceQueryHandler` implementing `IQueryHandler<GetUserPreferenceQuery, Result<string?, Error>>` — calls `IUserPreferenceRepository.GetAsync`, returns `null` (not error) when key not found in `src/Rentier.Application/Handlers/GetUserPreferenceQueryHandler.cs`
- [x] T010 Create `SetUserPreferenceCommandHandler` implementing `ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>` — calls `GetAsync`, if found calls `entity.UpdateValue()` then `SaveAsync`, if not found creates new `UserPreference` then `SaveAsync` in `src/Rentier.Application/Handlers/SetUserPreferenceCommandHandler.cs`

### Infrastructure — EF Core (depends on T005)

- [x] T011 Create `UserPreferenceConfiguration` implementing `IEntityTypeConfiguration<UserPreference>` (table: `UserPreferences`, PK: `Key`, max 100; Value: required, max 500) in `src/Rentier.Infrastructure/Persistence/Configurations/UserPreferenceConfiguration.cs`
- [x] T012 Add `DbSet<UserPreference> UserPreferences` to `AppDbContext` and register `UserPreferenceConfiguration` in `OnModelCreating` in `src/Rentier.Infrastructure/Persistence/AppDbContext.cs`
- [x] T013 Generate EF Core migration for the `UserPreferences` table (run: `dotnet ef migrations add 0014_UserPreferences --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop`) — verify migration creates `UserPreferences (Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL)` in `src/Rentier.Infrastructure/Persistence/Migrations/`
- [x] T014 Create `UserPreferenceRepository` implementing `IUserPreferenceRepository` — `GetAsync` uses `FindAsync`, `SaveAsync` uses `Add`/`Update` with upsert logic, `SaveChangesAsync` on both paths in `src/Rentier.Infrastructure/Repositories/UserPreferenceRepository.cs`

### Desktop — Localizer Service Interface & Implementation (no dependencies on T005–T014)

- [x] T015 [P] Create `ILocalizationService` interface (`string this[string key]`, `string CurrentCultureCode`, `void SetCulture(string cultureCode)`, `IObservable<string> CultureChanged`) extending `INotifyPropertyChanged` in `src/Rentier.Desktop/Services/ILocalizationService.cs` — use contracts/localization-service.md verbatim
- [x] T016 Create `LocalizationService` implementing `ILocalizationService` — wraps `Strings.ResourceManager`, default culture `sr-Latn`, indexer fallback chain (current → sr-Latn → neutral → `[key]`), `SetCulture` raises `PropertyChanged("")` and emits on `_cultureChanged` Subject in `src/Rentier.Desktop/Services/LocalizationService.cs` — use contracts/localization-service.md implementation verbatim

### Desktop — Serbian Translations (no dependencies)

- [x] T017 [P] Create `Strings.sr-Latn.resx` with Serbian Latin translations for every key in `Strings.resx` — all nav labels (`Nav_Dashboard` → `Kontrolna tabla`, `Nav_Filings` → `Prijave`, etc.), tab headers, form labels, button text, column headers, validation messages, empty states, dialog strings, tooltips, and placeholder text in `src/Rentier.Desktop/Resources/Strings.sr-Latn.resx`

**Checkpoint**: Foundation complete — all three user stories may now be implemented

---

## Phase 3: User Story 1 — Switch Application Language from Settings (Priority: P1) 🎯 MVP

**Goal**: The reactive language switch mechanism works end-to-end. Selecting a different language in `AppearanceSettingsViewModel` instantly updates all AXAML-bound strings and sidebar navigation labels across the entire application.

**Independent Test**: Programmatically call `localizationService.SetCulture("en")` and assert that `localizationService["Nav_Dashboard"]` returns `"Dashboard"` (English) and all `{Binding [Nav_Dashboard], Source={StaticResource Localizer}}` bindings across AXAML views re-evaluate. Then call `SetCulture("sr-Latn")` and assert the same binding returns `"Kontrolna tabla"`.

### Tests for User Story 1

- [x] T018 [P] [US1] Write `UserPreference` entity tests — valid construction, `Key`/`Value` constraint violations throw `DomainException`, `UpdateValue()` enforces max length, `UpdateValue(null)` throws `ArgumentNullException` in `tests/Rentier.UnitTests/Domain/UserPreferenceTests.cs`
- [x] T019 [P] [US1] Write `GetUserPreferenceQueryHandler` tests — returns value when key exists, returns `null` result when key not found, propagates repository error in `tests/Rentier.UnitTests/Application/GetUserPreferenceQueryHandlerTests.cs`
- [x] T020 [P] [US1] Write `SetUserPreferenceCommandHandler` tests — creates new preference when key absent (inserts), updates existing preference when key present (updates value), validates max length on value before saving in `tests/Rentier.UnitTests/Application/SetUserPreferenceCommandHandlerTests.cs`
- [x] T021 [P] [US1] Write `LocalizationService` unit tests — indexer returns English string when culture is `en`, returns Serbian string when culture is `sr-Latn`, falls back to Serbian for a key missing in English resx, fallback to `[key]` when key missing everywhere, `SetCulture` raises `PropertyChanged("")`, `CultureChanged` observable emits new culture code; **also assert FR-008**: after `SetCulture()` is called, any ViewModel-bound input property value (e.g., a simulated `TextBox` binding or string observable) is unaffected — the switch must not mutate user-entered data in `tests/Rentier.UnitTests/Desktop/LocalizationServiceTests.cs`

### Implementation for User Story 1

- [x] T022 [US1] Convert `NavigationEntry` from a positional `record` to a `ReactiveObject` class with a reactive `string Label` property that can be updated without rebuilding the collection — keep `Route`, `Icon`, and `NavigateTo` delegate as constructor parameters in `src/Rentier.Desktop/NavigationEntry.cs`
- [x] T023 [US1] Inject `ILocalizationService` into `MainWindowViewModel`, initialize sidebar `NavigationEntry` labels from localizer (replace direct `Strings.Nav_*` calls with `localizationService["Nav_*"]`), subscribe to `ILocalizationService.CultureChanged` on `RxApp.MainThreadScheduler` to update each `NavigationEntry.Label` when culture changes, dispose subscription with `DisposeWith` in `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs`
- [x] T024 [US1] Add `Localizer` as an `Application.Resources` entry in `App.axaml` (`<svc:LocalizationService x:Key="Localizer" />`); in `App.axaml.cs` after building the DI container, resolve the `ILocalizationService` singleton and assign it to `Application.Current.Resources["Localizer"]` so the DI instance and the AXAML resource are the same object in `src/Rentier.Desktop/App.axaml` and `src/Rentier.Desktop/App.axaml.cs`
- [x] T025 [P] [US1] Bulk-migrate `{x:Static res:Strings.Key}` → `{Binding [Key], Source={StaticResource Localizer}}` in the high-binding-count views: `ManualFilingView.axaml` (22), `FilingsView.axaml` (19), `ReportsView.axaml` (17), `DashboardView.axaml` (15) — remove `xmlns:res` namespace if no remaining `{x:Static}` references; add `xmlns:svc="using:Rentier.Desktop.Services"` if not present in `src/Rentier.Desktop/Views/ManualFilingView.axaml`, `FilingsView.axaml`, `ReportsView.axaml`, `DashboardView.axaml`
- [x] T026 [P] [US1] Bulk-migrate `{x:Static res:Strings.Key}` → `{Binding [Key], Source={StaticResource Localizer}}` in the remaining seven views: `HolidaySettingsView.axaml` (12), `ImporterSettingsView.axaml` (13), `SyncView.axaml` (6), `MailboxSettingsView.axaml` (8), `ProfileSettingsView.axaml` (8), `SettingsView.axaml` (4), `MainWindow.axaml` (1) — update namespaces as in T025 in `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`, `ImporterSettingsView.axaml`, `SyncView.axaml`, `MailboxSettingsView.axaml`, `ProfileSettingsView.axaml`, `SettingsView.axaml`, `src/Rentier.Desktop/Views/MainWindow.axaml`
- [x] T027 [US1] Add `SelectedLanguage` (`string`) and `LanguageOptions` (`ReadOnlyObservableCollection<(string Code, string DisplayName)>`) reactive properties to `AppearanceSettingsViewModel`, inject `ILocalizationService`, wire `SelectedLanguage` change (via `WhenAnyValue`) to call `localizationService.SetCulture(code)` immediately in `src/Rentier.Desktop/ViewModels/AppearanceSettingsViewModel.cs`

**Checkpoint**: User Story 1 fully functional — `localizationService.SetCulture("en"/"sr-Latn")` instantly switches all AXAML bindings and sidebar labels; all US1 tests pass

---

## Phase 4: User Story 2 — Language Preference Survives App Restart (Priority: P1)

**Goal**: The selected language is written to SQLite when changed and read from SQLite on startup before any view renders. A fresh install defaults to Serbian.

**Independent Test**: (1) Call `SetUserPreferenceCommand("Language", "en")`, close and reopen the application — verify the startup reads `"en"` from the database and calls `localizationService.SetCulture("en")` before `MainWindow` is shown. (2) Wipe the database, reopen — verify Serbian is the default on first launch.

### Tests for User Story 2

- [x] T028 [P] [US2] Write `UserPreferenceRepository` integration tests using SQLite in-memory provider — `GetAsync` returns `null` when table is empty, returns entity when row exists; `SaveAsync` inserts a new preference when key absent (verify row exists in db), `SaveAsync` updates `Value` when key already present (upsert), `SaveAsync` on a detached-then-modified entity persists via `UpdateValue()` in `tests/Rentier.Infrastructure.Tests/Repositories/UserPreferenceRepositoryTests.cs`
- [x] T029 [P] [US2] Write `AppearanceSettingsViewModel` tests — selecting a language option calls `SetUserPreferenceCommand` with `("Language", code)`, selection of the same language twice does not trigger a duplicate command, on ViewModel construction the initial `SelectedLanguage` matches `localizationService.CurrentCultureCode` in `tests/Rentier.UnitTests/Desktop/AppearanceSettingsViewModelTests.cs`

### Implementation for User Story 2

- [x] T030 [US2] Register `IUserPreferenceRepository` → `UserPreferenceRepository`, `GetUserPreferenceQueryHandler`, and `SetUserPreferenceCommandHandler` in the DI container in `src/Rentier.Desktop/Composition/CompositionRoot.cs`
- [x] T031 [US2] Inject `ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>` into `AppearanceSettingsViewModel`; after `localizationService.SetCulture(code)` fires in the `SelectedLanguage` change subscription, also dispatch `SetUserPreferenceCommand("Language", code)` as a `ReactiveCommand.CreateFromTask` in `src/Rentier.Desktop/ViewModels/AppearanceSettingsViewModel.cs`
- [x] T032 [US2] In `App.axaml.cs` startup sequence (after DI build, before `MainWindow` creation): resolve `IQueryHandler<GetUserPreferenceQuery, Result<string?, Error>>`, call `GetUserPreferenceQuery("Language")` async, apply the result with `localizationService.SetCulture(savedCode ?? "sr-Latn")`, then assign the singleton to `Application.Current.Resources["Localizer"]`, then create `MainWindow` — guarantees correct language on the first rendered frame in `src/Rentier.Desktop/App.axaml.cs`

**Checkpoint**: User Story 2 fully functional — language selection persists across restarts; fresh install defaults to Serbian; all US2 tests pass

---

## Phase 5: User Story 3 — Language Section in Settings (Priority: P2)

**Goal**: Opening Settings → Appearance reveals a Language section with two clearly labelled options ("English" and "Srpski"), the current selection is visually indicated, and selecting either option triggers the live switch and persistence from US1/US2.

**Independent Test**: Open the Settings → Appearance tab and confirm: a Language section is visible below the Theme section, exactly two radio buttons are present ("English" and "Srpski"), and the active language has its button selected. Clicking the inactive option immediately changes all visible strings.

### Tests for User Story 3

- [x] T033 [P] [US3] Write `AppearanceSettingsViewModel` tests for the language options display — `LanguageOptions` contains exactly 2 entries with display names `"English"` and `"Srpski"`, each label is in its own language regardless of current culture in `tests/Rentier.UnitTests/Desktop/AppearanceSettingsViewModelTests.cs`

### Implementation for User Story 3

- [x] T034 [US3] Add a Language section card to `AppearanceSettingsView.axaml` below the existing Theme card — two `RadioButton` controls bound to `AppearanceSettingsViewModel.SelectedLanguage` with `GroupName="Language"`, labelled `"English"` and `"Srpski"` in their own language (static labels, not localizer-bound, per FR-006), section header bound to `{Binding [Settings_Language], Source={StaticResource Localizer}}` in `src/Rentier.Desktop/Views/AppearanceSettingsView.axaml`
- [x] T035 [US3] Migrate all remaining hardcoded English strings in `AppearanceSettingsView.axaml` (section headers like "Appearance", "Theme", subsection labels) to use `{Binding [Key], Source={StaticResource Localizer}}` bindings; add corresponding keys to both `Strings.resx` (English) and `Strings.sr-Latn.resx` (Serbian) in `src/Rentier.Desktop/Views/AppearanceSettingsView.axaml`, `src/Rentier.Desktop/Resources/Strings.resx`, `src/Rentier.Desktop/Resources/Strings.sr-Latn.resx`

**Checkpoint**: All three user stories fully functional and independently testable

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Quality gates, DI smoke test, namespace cleanup, and final validation.

- [x] T036 [P] Update DI registration smoke test to assert that `ILocalizationService`, `IUserPreferenceRepository`, `IQueryHandler<GetUserPreferenceQuery, Result<string?, Error>>`, and `ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>` are all resolvable from the container in `tests/Rentier.UnitTests/Application/DiRegistrationSmokeTests.cs`
- [x] T037 [P] Remove orphaned `xmlns:res="clr-namespace:Rentier.Desktop.Resources"` namespace declarations from any AXAML view files that no longer contain `{x:Static res:Strings.*}` bindings after the bulk migration — verify no build warnings remain in all migrated `.axaml` files
- [x] T038 [P] Add `Settings_Language` key and any other keys introduced in T034 to both `src/Rentier.Desktop/Resources/Strings.resx` (English value) and `src/Rentier.Desktop/Resources/Strings.sr-Latn.resx` (Serbian value), then verify `Strings.Designer.cs` is regenerated (rebuild project)
- [x] T039 Run all test projects and verify: Domain coverage is 100% for `UserPreference` rule paths, Application handler coverage is ≥90%, Infrastructure `UserPreferenceRepositoryTests` pass with SQLite in-memory, Desktop `LocalizationServiceTests` and `AppearanceSettingsViewModelTests` pass — run: `dotnet test --no-build`
- [x] T040 Execute the quickstart.md manual validation: (1) launch app — confirm Serbian is shown; (2) open Settings → Appearance, select English — confirm all sidebar labels, tab headers, and view strings switch instantly; (3) close and reopen — confirm English loads from first frame; (4) select Srpski, close, reopen — confirm Serbian loads from first frame; (5) verify no blank or `[key]` strings appear in either locale

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately; T002, T003, T004 are parallel reads
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
  - T005–T008 can run in parallel (no cross-dependencies)
  - T009 depends on T005 + T007; T010 depends on T005 + T008
  - T011 depends on T005; T012 depends on T005 + T011; T013 depends on T012; T014 depends on T013
  - T015 and T017 are independent of T005–T014 and run in parallel; T016 depends on T015 (must compile ILocalizationService before implementing LocalizationService)
- **US1 (Phase 3)**: Depends on Phase 2 complete
  - T018–T021 tests can run in parallel; T022 is independent of T015–T017 (no dependency — may run in parallel with Phase 2 foundational work); T023 depends on T022 + T016; T025 and T026 are parallel to each other; T027 depends on T015+T016
- **US2 (Phase 4)**: Depends on Phase 3 complete (needs `AppearanceSettingsViewModel` from T027)
  - T028 depends on T014; T029 depends on T027; T030 depends on T009+T010; T031 depends on T027+T030; T032 depends on T030+T031
- **US3 (Phase 5)**: Depends on Phase 3 + Phase 4 complete (AppearanceSettingsViewModel fully wired)
  - T033 depends on T027; T034 depends on T027; T035 depends on T034+T017
- **Polish (Phase 6)**: Depends on all story phases complete

### User Story Dependencies

- **US1 (P1)**: Depends on Phase 2 foundational — no dependency on US2 or US3
- **US2 (P1)**: Depends on US1 (`AppearanceSettingsViewModel.SelectedLanguage` wired) — adds persistence layer on top
- **US3 (P2)**: Depends on US1 + US2 being complete (consumes fully wired ViewModel) — adds Settings UI

### Parallel Opportunities per Story

```
Phase 2:
  Parallel group A: T005, T006, T007, T008, T015, T016, T017
  Sequential: T005 → T011 → T012 → T013 → T014
              T007 → T009; T008 → T010

Phase 3:
  Parallel group B (tests): T018, T019, T020, T021
  Parallel group C (migration): T025, T026
  Sequential: T022 (no Phase 2 dependency — parallel with Phase 2) → T023 (after T022 + T016) → T024

Phase 4:
  Parallel group D (tests): T028, T029
  Sequential: T030 → T031 → T032

Phase 5:
  Parallel group E (test): T033
  Sequential: T034 → T035
```

---

## Implementation Strategy

### MVP First (US1 only — core language switch)

1. Complete Phase 1: Setup (T001–T004)
2. Complete Phase 2: Foundational — T005–T017 (critical block)
3. Complete Phase 3: US1 — T018–T027
4. **STOP and VALIDATE**: Language switch works live in all views; US1 tests pass
5. Merge / demo — the app can switch languages, even without persistence

### Incremental Delivery

1. **Setup + Foundational** → domain, CQRS, infrastructure, localizer service ready
2. **Add US1** → live language switch works; test independently → Demo: instant language toggle
3. **Add US2** → persistence works; test independently → Demo: language survives restart
4. **Add US3** → Settings UI polished; test independently → Demo: full user journey
5. **Polish** → coverage gates, DI smoke test, namespace cleanup

### Single-Developer Sequence (Recommended Order)

`T001 → T002+T003+T004 (parallel) → T005+T006+T007+T008+T015+T016+T017 (parallel) → T009+T010+T011 → T012 → T013 → T014 → T018+T019+T020+T021 (parallel) → T022 → T023 → T024 → T025+T026 (parallel) → T027 → T028+T029 (parallel) → T030 → T031 → T032 → T033 → T034 → T035 → T036+T037+T038 (parallel) → T039 → T040`

---

## Notes

- **`[P]` tasks** = different files, no incomplete dependencies — safe to run in parallel
- **`[US#]` label** maps each task to its user story for traceability to spec.md
- **AXAML migration (T025, T026)**: Use PowerShell `Select-String` first to enumerate all sites, then batch-replace using a script or IDE multi-file replace to avoid misses. Pattern: `\{x:Static res:Strings\.(\w+)\}` → `{Binding [$1], Source={StaticResource Localizer}}`
- **Localizer singleton identity (T024, T032)**: The `LocalizationService` instance created by DI and the `Application.Resources["Localizer"]` entry MUST be the same object. Never construct `LocalizationService` directly in AXAML — assign the DI-resolved singleton to the resource dictionary after DI builds.
- **NavigationEntry labels (T023)**: Navigation labels are set in C# code, not AXAML — they will NOT benefit from the AXAML migration and require the explicit `CultureChanged` subscription.
- **"English"/"Srpski" labels (T034)**: Per FR-006 these two labels are intentionally hardcoded (not localizer-bound) so users can identify their language regardless of the active locale.
- **Migration numbering (T013)**: Follow existing convention — the next migration is `0014_UserPreferences`. Use `dotnet ef migrations add` to generate; inspect the output to ensure only `UserPreferences` table is added.
- Each user story is independently completable and testable — stop at any phase checkpoint to validate before proceeding.
