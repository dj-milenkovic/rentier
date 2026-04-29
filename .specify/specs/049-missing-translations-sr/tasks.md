# Tasks: Missing Serbian Translations Audit & Fix

**Input**: Design documents from `.specify/specs/049-missing-translations-sr/`
**Feature Branch**: `049-missing-translations-sr`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/localization-contract.md ✓

**Tests**: Included for Work Package 5 (parity) and Work Package 6 (ViewModel) — spec explicitly requires a parity test (FR-008, CA-006) and the plan defines ViewModel update tests.

**Organization**: Foundational resource-key work gates all story phases. US3 (Reports) and US4 (Dashboard/Settings) are the primary implementation stories. US1 (Sync) and US2 (Filings) are verified by the foundation + parity test — research confirmed no additional translation work is needed for those pages beyond the 243 existing keys.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US3, US4, US5)
- All paths are relative to repository root

---

## Phase 1: Setup

**Purpose**: Confirm the build baseline is clean before making additive changes.

- [X] T001 Verify baseline build with `dotnet build Rentier.slnx` and confirm zero errors and zero warnings; note current test count for regression tracking

---

## Phase 2: Foundational — Resource Key Additions

**Purpose**: Add all 12 new resource keys (English + Serbian) so every downstream AXAML change and converter fix has the string it needs. **No story-phase task may begin until T002–T004 are done.**

⚠️ **CRITICAL**: This phase blocks all user story implementation — complete it first.

**Complete key list** (from data-model.md):

| Key | English Value | Serbian (Latin) Value |
|-----|-------------|----------------------|
| `Update_Available_Format` | `Update v{0} available` | `Dostupno ažuriranje v{0}` |
| `Update_Now_Button` | `Update Now` | `Ažuriraj sada` |
| `Update_Later_Button` | `Later` | `Kasnije` |
| `Update_Downloading_Format` | `Downloading update... {0}%` | `Preuzimanje ažuriranja... {0}%` |
| `Update_Ready_Text` | `Update ready. Restart to apply.` | `Ažuriranje spremno. Restartujte da primenite.` |
| `Update_RestartNow_Button` | `Restart Now` | `Restartuj sada` |
| `Update_Failed_Format` | `Update failed: {0}` | `Ažuriranje neuspešno: {0}` |
| `Update_Retry_Button` | `Retry` | `Ponovi` |
| `Update_Dismiss_Button` | `Dismiss` | `Odbaci` |
| `Holidays_Empty_Text` | `No holidays configured. Click Add or Fetch from web to get started.` | `Nema konfigurisanih praznika. Kliknite Dodaj ili Preuzmi sa weba da počnete.` |
| `Reports_Col_Actions` | `Actions` | `Akcije` |
| `ReportStatus_PartialError` | `Partial Error` | `Delimična greška` |

- [X] T002 Add all 12 new `<data>` entries to `src/Rentier.Desktop/Resources/Strings.resx` using the English values from the key table above; maintain alphabetical grouping within the file
- [X] T003 Update `src/Rentier.Desktop/Resources/Strings.Designer.cs` to add matching `public static string` properties for all 12 new keys (regenerate via MSBuild custom tool, or add manually following the existing auto-generated property pattern)
- [X] T004 [P] Add all 12 new Serbian translation entries to the static dictionary in `src/Rentier.Desktop/Resources/SrLatnStrings.cs` using the Serbian (Latin) values from the key table above; maintain key-order parity with English

> ⚠️ T004 can be authored in parallel with T002 since both files are independent — but T003 must follow T002.

**Checkpoint**: All 12 keys present in both `Strings.Designer.cs` and `SrLatnStrings.cs`. Build must pass before proceeding.

---

## Phase 3: User Story 3 — Reports Page Fully Translated (Priority: P1)

**Goal**: Every user-visible string on the Reports page renders in Serbian (Latin script) — specifically the `Actions` column header and the `PartialError` status badge.

**Independent Test**: Run the app with sr-Latn locale, navigate to the Reports page, confirm the "Actions" column header reads "Akcije" and any filing/report in `PartialError` state shows "Delimična greška" instead of "PartialError".

**Dependencies**: Requires T002–T004 complete (resource keys must exist).

### Implementation for User Story 3

- [X] T005 [P] [US3] Add the missing `ReportStatus.PartialError` case to the switch expression in `src/Rentier.Desktop/Converters/ReportStatusDisplayConverter.cs` mapping it to `Strings.ReportStatus_PartialError`; ensure no enum value falls through to `.ToString()`
- [X] T006 [P] [US3] Replace the hardcoded `Header="Actions"` with a localized binding `Header="{Binding [Reports_Col_Actions], Source={StaticResource Localizer}}"` in the Actions `DataGridTemplateColumn` in `src/Rentier.Desktop/Views/ReportsView.axaml` (research: line ~140)

> T005 and T006 touch different files and have no dependency on each other — run in parallel.

**Checkpoint**: Reports page fully Serbian. User Story 3 independently testable and complete.

---

## Phase 4: User Story 4 — Dashboard and Settings Pages Fully Translated (Priority: P2)

**Goal**: The update notification bar (visible globally) and the Holidays empty-state message both display fully localized Serbian text. All update bar states (available, downloading, ready, error) are covered.

**Independent Test**: Run the app with sr-Latn locale. (a) Trigger or mock each update bar state and confirm all button labels and messages are Serbian. (b) Navigate to Settings → Holidays with no holidays configured and confirm the empty-state reads "Nema konfigurisanih praznika. Kliknite Dodaj ili Preuzmi sa weba da počnete."

**Dependencies**: Requires T002–T004 complete.

### Implementation for User Story 4

- [X] T007 [US4] Add three reactive computed string properties to `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs`:
  - `UpdateAvailableText` → `string.Format(_localizationService["Update_Available_Format"], AvailableVersion)`
  - `DownloadingText` → `string.Format(_localizationService["Update_Downloading_Format"], DownloadProgress)`
  - `UpdateFailedText` → `string.Format(_localizationService["Update_Failed_Format"], UpdateErrorMessage)`

  Each property must raise `PropertyChanged` (via `WhenAnyValue` or `ObservableAsProperty`) whenever its inputs change, following the existing reactive property pattern in `MainWindowViewModel`.

- [X] T008 [US4] Replace all 9 hardcoded English strings in the update notification bar in `src/Rentier.Desktop/Views/MainWindow.axaml` with localized bindings (research: lines ~158–214):
  - `StringFormat='Update v{0} available'` → `Text="{Binding UpdateAvailableText}"`
  - `Content="Update Now"` → `Content="{Binding [Update_Now_Button], Source={StaticResource Localizer}}"`
  - `Content="Later"` → `Content="{Binding [Update_Later_Button], Source={StaticResource Localizer}}"`
  - `StringFormat='Downloading update... {0}%'` → `Text="{Binding DownloadingText}"`
  - `Text="Update ready. Restart to apply."` → `Text="{Binding [Update_Ready_Text], Source={StaticResource Localizer}}"`
  - `Content="Restart Now"` → `Content="{Binding [Update_RestartNow_Button], Source={StaticResource Localizer}}"`
  - `StringFormat='Update failed: {0}'` → `Text="{Binding UpdateFailedText}"`
  - `Content="Retry"` → `Content="{Binding [Update_Retry_Button], Source={StaticResource Localizer}}"`
  - `Content="Dismiss"` → `Content="{Binding [Update_Dismiss_Button], Source={StaticResource Localizer}}"`

  > T008 depends on T007 (ViewModel properties must exist for the three format-string bindings).

- [X] T009 [P] [US4] Replace the hardcoded `Text="No holidays configured..."` with `Text="{Binding [Holidays_Empty_Text], Source={StaticResource Localizer}}"` in the empty-state `TextBlock` in `src/Rentier.Desktop/Views/HolidaySettingsView.axaml` (research: line ~54)

  > T009 is independent of T007/T008 — different file, no shared state. Can be authored in parallel with T007.

**Checkpoint**: Update bar and Holidays empty state display Serbian text. User Story 4 independently testable and complete.

---

## Phase 5: User Story 5 — Complete Translation Parity Audit (Priority: P2)

**Goal**: A failing test proves missing translations; a passing test guarantees 100% key parity between English and Serbian dictionaries. ViewModel format-string properties are covered by unit tests.

**Independent Test**: `dotnet test --filter "LocalizationServiceTests|MainWindowViewModel_UpdateTests"` — all tests pass.

**Dependencies**: Requires T002–T004 complete (all 12 keys must exist before parity test can pass). T010 and T011 are independent of each other.

### Tests for User Story 5

- [X] T010 [P] [US5] Add a comprehensive `[Fact]` test `TranslationParity_AllEnglishKeysHaveSerbianTranslations` to `tests/Rentier.UnitTests/Desktop/LocalizationServiceTests.cs` that:
  1. Collects all English key names via reflection over `Strings` class public static string properties
  2. Collects all Serbian keys from `SrLatnStrings.All.Keys`
  3. Asserts the two sets are equal using `FluentAssertions` (`Should().BeEquivalentTo(...)`) with a descriptive failure message listing any missing keys in either direction

- [X] T011 [P] [US5] Add or update tests in `tests/Rentier.UnitTests/Desktop/MainWindowViewModel_UpdateTests.cs` for the three new localized format-string properties:
  - `UpdateAvailableText` returns a string containing the version number and formatted from `Update_Available_Format`
  - `DownloadingText` returns a string containing the progress percentage and formatted from `Update_Downloading_Format`
  - `UpdateFailedText` returns a string containing the error message and formatted from `Update_Failed_Format`
  
  Tests must work with a test double / mock `ILocalizationService` that returns the format string templates. Follow the existing test setup pattern in `MainWindowViewModel_UpdateTests.cs`.

**Checkpoint**: Both test classes green. Zero missing translation keys. User Story 5 complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, build clean-up, and regression check.

- [X] T012 [P] Run `dotnet build Rentier.slnx` and confirm zero errors and zero new warnings compared to T001 baseline
- [X] T013 [P] Run `dotnet test Rentier.slnx` and confirm all tests pass (including new parity and ViewModel tests from Phase 5); confirm English locale is unaffected by checking that the `Strings.Designer.cs` static properties still return English values
- [X] T014 Follow the quickstart verification steps in `.specify/specs/049-missing-translations-sr/quickstart.md`: launch app in sr-Latn, navigate all five sections (Dashboard, Sync, Filings, Reports, Settings), confirm zero English text or raw resource keys are visible
- [X] T015 [P] Switch app to English locale in Settings → Appearance and verify all text reverts to English with no regressions (FR-011)

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)
    └─► Phase 2 (Foundational — Resource Keys)  ← BLOCKS EVERYTHING
            ├─► Phase 3 (US3 — Reports, P1)
            │       T005 [P]  ReportStatusDisplayConverter.cs
            │       T006 [P]  ReportsView.axaml
            ├─► Phase 4 (US4 — Dashboard/Settings, P2)
            │       T007      MainWindowViewModel.cs
            │       T008      MainWindow.axaml          (after T007)
            │       T009 [P]  HolidaySettingsView.axaml (parallel to T007)
            └─► Phase 5 (US5 — Parity Audit, P2)
                    T010 [P]  LocalizationServiceTests.cs
                    T011 [P]  MainWindowViewModel_UpdateTests.cs
Phase 6 (Polish) ← after Phases 3–5
```

### User Story Independence

- **US3 (P1)**: Independently testable after T005 + T006. No dependency on US4/US5.
- **US4 (P2)**: Independently testable after T007 + T008 + T009. No dependency on US3 (different files).
- **US5 (P2)**: Independently verifiable after T010 + T011 pass. Requires resource keys (Phase 2) to be complete.
- **US1 (Sync) / US2 (Filings)**: Already covered by the 243 existing keys (confirmed by research). No new implementation tasks — verified green by the US5 parity test (T010).

### Parallel Opportunities

```bash
# Phase 2 — resource key authors can split work:
Task: "Add English keys to Strings.resx"            (T002)
Task: "Add Serbian translations to SrLatnStrings.cs" (T004, parallel with T002)
# Then T003 (Strings.Designer.cs) after T002.

# Phase 3 — both US3 tasks are fully parallel:
Task: "Fix ReportStatusDisplayConverter.cs"  (T005)
Task: "Fix ReportsView.axaml Actions column" (T006)

# Phase 4 — T009 parallel with T007:
Task: "Add ViewModel format properties"         (T007)
Task: "Fix HolidaySettingsView.axaml"           (T009, parallel with T007)
# Then T008 after T007.

# Phase 5 — both test tasks are fully parallel:
Task: "Add parity test to LocalizationServiceTests.cs"  (T010)
Task: "Update MainWindowViewModel_UpdateTests.cs"        (T011)

# Phase 6 — T012, T013, T015 are parallel:
Task: "dotnet build verification"   (T012)
Task: "dotnet test verification"    (T013)
Task: "English locale regression"   (T015)
```

---

## Implementation Strategy

### MVP First (Highest-impact P1 work only)

1. Complete Phase 2: Foundational (resource keys — ~30 min)
2. Complete Phase 3: US3 Reports (converter + AXAML — ~15 min)
3. **STOP and VALIDATE**: Reports page fully Serbian, parity test written
4. Merge as MVP — Reports P1 story is shippable independently

### Incremental Delivery

1. Phase 2 (Foundation) → Phase 3 (US3, Reports) → **Demo/Validate**
2. Phase 4 (US4, Dashboard/Settings update bar + Holidays) → **Demo/Validate**
3. Phase 5 (US5, Parity tests) → **Green tests gate merge**
4. Phase 6 (Polish) → **Ship**

### Single-Developer Fast Path

The entire feature is ~6 small, well-scoped changes across 7 files. A single developer can complete all phases in one session:

```
T001 → T002 → T003
             ↕ (parallel author)
             T004
→ T005, T006 (parallel)
→ T007 → T008
   ↕ (parallel author)
   T009
→ T010, T011 (parallel)
→ T012, T013, T014, T015
```

---

## Notes

- **[P]** = different files, no incomplete-task dependency — safe to parallelize
- **[USn]** label maps each task to its user story for traceability
- Format placeholders (`{0}`, `{1}`) MUST be preserved verbatim in all translated strings (FR-010)
- Serbian text MUST use Latin script only — no Cyrillic characters (Assumption: sr-Latn target)
- Do NOT rename or remove existing resource keys (FR-009)
- `AppearanceSettingsView.axaml` language labels ("English", "Srpski") are intentionally native — do NOT localize them (research R-002)
- `ManualFilingView.axaml` placeholder examples ("e.g. AAPL") are language-neutral — do NOT localize them (research R-002)
