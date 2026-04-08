---
description: "Task list for feature 020: Holidays Settings Repair"
---

# Tasks: 020 — Holidays Settings Repair

**Input**: Design documents from `specs/feature/020-holidays-settings-repair/`
**Spec**: `.specify/specs/020-holidays-settings-repair/spec.md`
**Branch**: `feature/020-holidays-settings-repair`

**Tests**: Included — spec.md CA-006 mandates tests for Infrastructure (parser), Application (handler error codes), and Desktop (ViewModel state + converter).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no shared state)
- **[Story]**: Which user story this task belongs to (US1–US4 per spec.md)
- Exact file paths included in all descriptions

---

## Phase 1: Setup

**Purpose**: Confirm test fixture is accessible and working. No new projects needed — existing Clean Architecture 4-project structure is used as-is.

- [X] T001 Verify `holiday-scraped.txt` is accessible at repo root and note absolute path `F:\Projects\Rentier\rentier\holiday-scraped.txt` for use as inline string content in `tests/Rentier.Infrastructure.Tests/Parsers/TimeAndDateHolidayScraperTests.cs` (read via `File.ReadAllText` in tests)

---

## Phase 2: Foundational — Error Code Alignment

**Purpose**: Align Infrastructure error codes with spec-defined codes before writing new tests. This is a pre-requisite because existing `ImportHolidaysFromWebCommandHandlerTests.cs` references the old codes and must be updated before new parser tests reference the new ones.

**⚠️ CRITICAL**: Complete T002–T003 before starting any US2 test tasks (T008–T009). The scraper changes in T002 will break existing handler tests until T003 is applied.

- [X] T002 Update error code string literals in `src/Rentier.Infrastructure/Scraping/TimeAndDateHolidayScraper.cs`: rename `"FETCH_FAILED"` → `"HOLIDAY_IMPORT_FAILED"`, `"PARSE_FAILED"` → `"HOLIDAY_PARSE_ERROR"`, `"NO_HOLIDAYS_FOUND"` → `"HOLIDAY_NOT_FOUND"` (three string replacements, no logic changes)
- [X] T003 [P] Update error code assertions in `tests/Rentier.Application.Tests/ImportHolidaysFromWebCommandHandlerTests.cs`: replace all `"FETCH_FAILED"`, `"PARSE_FAILED"`, `"NO_HOLIDAYS_FOUND"` assertion strings with the new codes from T002; run `dotnet test tests/Rentier.Application.Tests --filter ImportHolidaysFromWeb` to confirm green

**Checkpoint**: All existing holiday handler tests pass with new error code strings → Foundation ready.

---

## Phase 3: User Story 1 — Edit Holiday Date Reliably (Priority: P1) 🎯 MVP

**Goal**: Users can double-click any date cell in the Holidays DataGrid, type a new `yyyy-MM-dd` date, and commit with Enter/Tab or cancel with Escape. Invalid input is rejected with feedback and the original value is restored.

**Independent Test**: Open Settings → Holidays → double-click a date cell → type `2025-03-15` → press Enter → cell shows `2025-03-15` and `HasUnsavedChanges` is `true`. Press Escape on a different cell → original value is restored.

### Tests for User Story 1 ⚠️

> **Write these tests FIRST — they must FAIL before T006/T007 are implemented.**

- [X] T004 [P] [US1] Create `tests/Rentier.Desktop.Tests/Converters/DateOnlyToStringConverterTests.cs`: test `Convert(new DateOnly(2025,1,1), ...)` returns `"2025-01-01"`, test `ConvertBack("2025-03-15", ...)` returns `new DateOnly(2025,3,15)`, test `ConvertBack("abc", ...)` returns `BindingNotification` with `BindingErrorType.DataValidationError`, test `ConvertBack(null, ...)` returns error notification, test `Convert(null, ...)` returns `string.Empty`
- [X] T005 [P] [US1] Extend `tests/Rentier.Desktop.Tests/ViewModels/HolidaySettingsViewModelTests.cs`: add test that mutating `HolidayEntryViewModel.Date` to a new `DateOnly` changes the property value (two-way binding contract), add test that `HasUnsavedChanges` becomes `true` after modifying an entry's `Date` property via the ViewModel edit flow

### Implementation for User Story 1

- [X] T006 [P] [US1] Create `src/Rentier.Desktop/Converters/DateOnlyToStringConverter.cs`: `public sealed class DateOnlyToStringConverter : IValueConverter` with `public static readonly DateOnlyToStringConverter Instance = new();`, `Convert` formats `DateOnly` → `"yyyy-MM-dd"` returning `string.Empty` for null/non-DateOnly, `ConvertBack` calls `DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d)` returning `d` on success or `new BindingNotification(new FormatException("Invalid date. Use yyyy-MM-dd."), BindingErrorType.DataValidationError)` on failure; add `using Avalonia.Data;`, `using System.Globalization;`
- [X] T007 [US1] Update `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`: (a) add `<UserControl.Resources><local:DateOnlyToStringConverter x:Key="DateOnlyConverter"/>` (or reference static `Instance`), (b) replace `<DataGridTextColumn Header="Date" Binding="{Binding Date}" .../>` with `<DataGridTemplateColumn Header="Date" Width="*"><DataGridTemplateColumn.CellTemplate><DataTemplate><TextBlock Text="{Binding Date, Converter={StaticResource DateOnlyConverter}}"/></DataTemplate></DataGridTemplateColumn.CellTemplate><DataGridTemplateColumn.CellEditingTemplate><DataTemplate><TextBox Text="{Binding Date, Converter={StaticResource DateOnlyConverter}, Mode=TwoWay, UpdateSourceTrigger=LostFocus}"/></DataTemplate></DataGridTemplateColumn.CellEditingTemplate></DataGridTemplateColumn>`; add `xmlns:local="clr-namespace:Rentier.Desktop.Converters"` if not already present

**Checkpoint**: `dotnet test tests/Rentier.Desktop.Tests --filter DateOnlyToStringConverter` passes. Double-clicking a date cell in manual verification shows a TextBox and Enter commits.

---

## Phase 4: User Story 2 — Import Holidays from Web (Priority: P1)

**Goal**: Clicking Import for any year correctly fetches Serbian national holidays from `https://www.timeanddate.com/holidays/serbia/{YEAR}?hol=1`, extracts dates from `<th>` elements, names from anchor text in the 2nd `<td>`, filters to `showrow` rows with "National Holiday" type, and replaces grid entries. For year 2016 the sample data yields exactly 13 national holidays (SC-003).

**Independent Test**: Use `holiday-scraped.txt` as a fixture and call the scraper with fake HTTP. Verify 13 entries returned, first entry is `{Date: 2016-01-01, Name: "Western New Year's Day"}`. Verifying the ViewModel populates `Entries` with the returned DTOs and sets `HasUnsavedChanges = true`.

### Tests for User Story 2 ⚠️

> **Write these tests FIRST — they must FAIL before T010/T011 are implemented.**

- [X] T008 [P] [US2] Create `tests/Rentier.Infrastructure.Tests/Parsers/TimeAndDateHolidayScraperTests.cs`: load `holiday-scraped.txt` via `File.ReadAllText("../../../../holiday-scraped.txt")` (adjust relative path as needed), stub `HttpClient` to return the file content, call `ImportAsync(2016)`, assert `result.IsSuccess == true`, assert `result.Value.Count == 13`, assert first entry `Date == new DateOnly(2016, 1, 1)` and `Name == "Western New Year's Day"`, assert no entry has type "National Holiday" in name (type column excluded from name), assert `result.Value` contains `new DateOnly(2016, 2, 15)` with name `"Statehood Day of the Republic of Serbia"`, assert `result.Value` contains `new DateOnly(2016, 11, 11)` with name `"Armistice Day"`; add negative tests: HTML with missing `#holidays-table` returns `Error("HOLIDAY_PARSE_ERROR", ...)`, HTML with only `hiderow` rows returns `Error("HOLIDAY_NOT_FOUND", ...)`
- [X] T009 [P] [US2] Extend `tests/Rentier.Desktop.Tests/ViewModels/HolidaySettingsViewModelTests.cs`: (a) import success test — mock `ImportHolidaysFromWebCommandHandler` to return 3 `HolidayEntryDto` items, call `ImportCommand`, assert `Entries.Count == 3`, `HasUnsavedChanges == true`, `ErrorMessage == null`; (b) import failure test — mock handler returns `Error("HOLIDAY_IMPORT_FAILED", "network error")`, pre-populate Entries with 2 items, call `ImportCommand`, assert `Entries.Count == 2` (preserved), `ErrorMessage` contains "HOLIDAY_IMPORT_FAILED", `HasUnsavedChanges` unchanged

### Implementation for User Story 2

- [X] T010 [US2] Fix `src/Rentier.Infrastructure/Scraping/TimeAndDateHolidayScraper.cs`: (a) change date extraction: replace `row.QuerySelectorAll("td")[0]` with `row.QuerySelector("th")?.TextContent.Trim()`, (b) change name extraction: replace `row.QuerySelector("td.ce")` with `row.QuerySelectorAll("td")[1].QuerySelector("a")?.TextContent.Trim() ?? row.QuerySelectorAll("td")[1].TextContent.Trim()` (anchor text in 2nd `<td>`, not 1st), (c) add national holiday row filter: skip rows where `row.ClassList` does not contain `"showrow"` or where `row.QuerySelectorAll("td")` length < 3 or where `row.QuerySelectorAll("td")[2].TextContent` does not contain `"National Holiday"`, (d) update date parse format from any incorrect format to `"d MMM"` using `DateTime.ParseExact(dateText, "d MMM", CultureInfo.InvariantCulture)` then `new DateOnly(year, parsed.Month, parsed.Day)`; skip separator rows with empty `id` or no `<th>` child
- [X] T011 [US2] Fix `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs` import command handler: (a) in the `result.IsSuccess` branch, after `Entries.Clear()` and `foreach (var dto in result.Value) Entries.Add(...)`, add `HasUnsavedChanges = true;`, (b) confirm that `Entries.Clear()` is called ONLY inside the `IsSuccess` branch — not before checking the result — so that `Entries` is preserved when import fails

**Checkpoint**: `dotnet test tests/Rentier.Infrastructure.Tests --filter Holiday` passes with 13 entries. Import in manual verification populates the grid for year 2016/2025.

---

## Phase 5: User Story 3 — Year Fields Fully Visible (Priority: P2)

**Goal**: Start Year, End Year, and Import Year `NumericUpDown` controls show full 4-digit values without clipping at any supported window width. Import Year is a new control added to the toolbar.

**Independent Test**: Open Settings → Holidays at minimum window width → verify both year fields show 4 digits and spinner arrows are accessible. Resize window — fields remain fully visible.

### Tests for User Story 3 ⚠️

- [X] T012 [P] [US3] Extend `tests/Rentier.Desktop.Tests/ViewModels/HolidaySettingsViewModelTests.cs`: assert `ImportYear` property exists as `int`, defaults to `DateTime.Today.Year`, is publicly gettable and settable (property accessor test); assert that setting `ImportYear = 2030` reflects back when read

### Implementation for User Story 3

- [X] T013 [US3] Update `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`: (a) on the StartYear `NumericUpDown`: replace `Width="100"` with `MinWidth="120"` and add `FormatString="0"`, (b) on the EndYear `NumericUpDown`: same replacement, (c) add new `NumericUpDown` for Import Year before the Import button in the toolbar row: `<NumericUpDown Value="{Binding ImportYear}" MinWidth="120" FormatString="0" Minimum="2020" Maximum="2099"/>` with a preceding `<TextBlock Text="Year:" VerticalAlignment="Center" Margin="8,0,4,0"/>` label; ensure `ImportYear` is the `CommandParameter` for `ImportCommand` binding if currently using `{Binding ImportYear}` as parameter

**Checkpoint**: Start Year, End Year, and Import Year all show 4-digit values. Import Year field is visible in the toolbar.

---

## Phase 6: User Story 4 — Clear State Feedback (Priority: P2)

**Goal**: `IsLoading` gates all commands during async operations. `ErrorMessage` and `SuccessMessage` are cleared before each new operation. `HasItems` reflects `Entries.Count > 0`. An empty-state message is shown when no holidays are configured.

**Independent Test**: Mock all commands to run slowly (async delay). Verify `IsLoading = true` during execution and commands return `CanExecute = false`. After completion verify `IsLoading = false` and messages reflect outcome. Verify empty-state message visibility toggles with `HasItems`.

### Tests for User Story 4 ⚠️

- [X] T014 [P] [US4] Extend `tests/Rentier.Desktop.Tests/ViewModels/HolidaySettingsViewModelTests.cs`: add command gating tests — assert `AddRowCommand.CanExecute` is `false` when `IsLoading = true`, `DeleteRowCommand.CanExecute` is `false` when `IsLoading = true`, `SaveCommand.CanExecute` is `false` when `IsLoading = true`, `ImportCommand.CanExecute` is `false` when `IsLoading = true`; assert `ErrorMessage` and `SuccessMessage` are both `null` or cleared to `null` at the start of each async command execution before the async work begins
- [X] T015 [US4] Extend `tests/Rentier.Desktop.Tests/ViewModels/HolidaySettingsViewModelTests.cs`: assert `HasItems == false` when `Entries` is empty (initial state), assert `HasItems == true` after adding one entry to `Entries`, assert `HasItems` raises `PropertyChanged` when `Entries.Count` changes from 0 to 1 and from 1 to 0

### Implementation for User Story 4

- [X] T016 [US4] Update `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs`: (a) add `public bool HasItems => Entries.Count > 0;` computed property; subscribe to `Entries.CollectionChanged` to call `RaisePropertyChanged(nameof(HasItems))`, (b) for each of `AddRowCommand`, `DeleteRowCommand`, `SaveCommand`, `ImportCommand`: wire `CanExecute` to `!IsLoading` (using `ReactiveCommand.CreateFromTask(..., this.WhenAnyValue(x => x.IsLoading).Select(l => !l))` or equivalent `ObservesCanExecute` pattern), (c) at the start of each async command body (load, save, import) set `ErrorMessage = null; SuccessMessage = null;` before any await; confirm `IsLoading = true` is set before the await and `IsLoading = false` in a `finally` block
- [X] T017 [US4] Update `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`: add empty-state `TextBlock` inside or below the `DataGrid` container: `<TextBlock Text="No holidays configured. Click Add or Import to get started." IsVisible="{Binding !HasItems}" HorizontalAlignment="Center" VerticalAlignment="Center" Opacity="0.6" Margin="0,16"/>` (use `!` binding or a `BoolNegationConverter` if Avalonia version requires it); ensure `DataGrid` and the empty-state `TextBlock` are mutually exclusive via `IsVisible`

**Checkpoint**: `dotnet test tests/Rentier.Desktop.Tests --filter Holiday` passes. Import/Load buttons are visually disabled during async. Empty-state message appears when grid is empty.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, DI registration check, and quickstart verification across all layers.

- [X] T018 [P] Review `tests/Rentier.Application.Tests/DiRegistrationSmokeTests.cs`: confirm `IHolidayImporter` → `TimeAndDateHolidayScraper` binding is present in the DI smoke test; if `DiRegistrationSmokeTests.cs` does not already assert this binding, add `services.GetRequiredService<IHolidayImporter>()` assertion; run `dotnet test tests/Rentier.Application.Tests --filter DiRegistration` to confirm green
- [X] T019 Run quickstart.md validation: execute `dotnet build Rentier.slnx --no-restore`, then `dotnet test tests/Rentier.Infrastructure.Tests --filter Holiday`, `dotnet test tests/Rentier.Application.Tests --filter Holiday`, `dotnet test tests/Rentier.Desktop.Tests --filter Holiday`; all three test runs must pass; confirm SC-003 (exactly 13 entries from `holiday-scraped.txt`) is asserted and green
- [X] T020 [P] Verify constitution quality gate: run `dotnet test Rentier.slnx` (full suite) and confirm no regressions introduced; specifically verify `HolidayRepositoryTests.cs` in `tests/Rentier.Infrastructure.Tests` still passes (no side effects from scraper error code changes on repository layer)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1; **BLOCKS** US2 test tasks (T008–T009)
- **US1 (Phase 3)**: Depends on Phase 1 only; independent of Phase 2 (DateOnly converter has no dependency on error codes)
- **US2 (Phase 4)**: Depends on Phase 2 completion (error codes aligned before parser tests reference new codes)
- **US3 (Phase 5)**: Depends on Phase 1 only; independent of US1 and US2
- **US4 (Phase 6)**: Depends on US2 completion (command gating tests reference `IsLoading` state set during import)
- **Polish (Phase 7)**: Depends on all story phases complete

### User Story Dependencies

```
Phase 1 (Setup)
    │
    ├─→ Phase 2 (Foundational: error codes)
    │       │
    │       └─→ Phase 4 (US2: import fix) ──→ Phase 6 (US4: state feedback)
    │
    ├─→ Phase 3 (US1: date editing) ─────────────────────────────→ Phase 7 (Polish)
    │
    └─→ Phase 5 (US3: layout fix) ───────────────────────────────→ Phase 7 (Polish)
```

### Within Each User Story

1. Tests written first (TDD — must FAIL before implementation)
2. Implementation tasks after tests are confirmed failing
3. Story validated independently at checkpoint before proceeding

### Parallel Opportunities

- **US1 and US3** can be worked in parallel after Phase 1 (no shared files between `DateOnlyToStringConverter` and `NumericUpDown` layout changes)
- **US1 and US2** can be parallelized after their respective prerequisites: T004/T005/T006 [P] within US1; T008/T009 [P] within US2
- **Within US1**: T004, T005, T006 are all [P] — converter tests, ViewModel tests, and converter implementation can proceed simultaneously
- **Within US2**: T008 and T009 are [P] — scraper tests and ViewModel import tests can be authored simultaneously
- **Within US4**: T014 and T016 are [P] relative to T015 (different test concerns, same file)

---

## Parallel Example: User Story 1

```text
# All three tasks can start simultaneously after Phase 2:
Task T004: "Create DateOnlyToStringConverterTests.cs in tests/Rentier.Desktop.Tests/Converters/"
Task T005: "Extend HolidaySettingsViewModelTests for edit commit/cancel behavior"
Task T006: "Create DateOnlyToStringConverter.cs in src/Rentier.Desktop/Converters/"

# Then sequentially:
Task T007: "Update HolidaySettingsView.axaml with DataGridTemplateColumn" (depends on T006)
```

## Parallel Example: User Story 2

```text
# Both scraper and ViewModel tests can be authored simultaneously:
Task T008: "Create TimeAndDateHolidayScraperTests.cs using holiday-scraped.txt fixture"
Task T009: "Extend HolidaySettingsViewModelTests for import state transitions"

# Then implementation in either order (different files, no conflict):
Task T010: "Fix TimeAndDateHolidayScraper.cs parser logic"
Task T011: "Fix HolidaySettingsViewModel.cs import path"
```

---

## Implementation Strategy

### MVP First (US1 + US2 — Both P1)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Error code alignment (T002–T003)
3. Complete Phase 3: US1 — DateOnly editing fix (T004–T007)
4. Complete Phase 4: US2 — Import parser fix (T008–T011)
5. **STOP and VALIDATE**: Run `dotnet test` with Holiday filter — both critical bugs resolved
6. Demo: Edit a date inline + Import 2025 Serbian holidays → grid populated

### Incremental Delivery

1. Setup + Foundational → Error codes aligned, existing tests green
2. US1 → Date cell editing works → demo to stakeholder
3. US2 → Import works, returns 13 holidays → demo import flow
4. US3 → Year fields no longer clip → visual polish
5. US4 → Loading states, empty-state, command gating → UX completeness
6. Polish → Full suite green, DI verified, quickstart confirmed

### Single-Developer Suggested Order

```
T001 → T002 → T003 → T006 → T004 → T005 → T007 →
T010 → T008 → T009 → T011 →
T013 → T012 →
T016 → T014 → T015 → T017 →
T018 → T019 → T020
```

---

## Notes

- **[P]** = different files, no competing writes — safe to parallelize
- **[Story]** label maps task to spec.md user story for traceability
- `holiday-scraped.txt` contains Serbia 2016 HTML — 13 national holiday rows with `class="showrow"`
- Error codes (T002/T003) MUST precede US2 test tasks — old code strings in tests would create false-passing state
- `DataGridTemplateColumn` edit template uses `UpdateSourceTrigger=LostFocus` — Enter key triggers LostFocus in Avalonia; Escape is handled by DataGrid `CancelEdit` internals
- `HasUnsavedChanges` (T011) must be set AFTER `Entries` is populated, not before, to ensure the flag reflects the actual state change
- Empty-state visibility in T017: Avalonia 11 supports `{Binding !HasItems}` directly for bool negation in `IsVisible` bindings — no separate converter needed
