# Implementation Plan: Holiday Configuration

**Branch**: `feature/003-holiday-configuration` | **Date**: 2026-04-06 | **Spec**: `specs/003-holiday-configuration/spec.md`  
**Input**: Feature specification from `.specify/specs/003-holiday-configuration/spec.md`

---

## Summary

Feature 003 introduces a full holiday-management capability to the Rentier desktop application.
The taxpayer can view, add, edit, and delete public holiday records via **Settings → Holidays**,
persist them to SQLite, and optionally import them from `timeanddate.com` for any year within the
configured range. Two new domain entities (`PublicHoliday`, `HolidayYearRange`) back the feature;
the existing `HolidayConf` value object is left structurally unchanged. An AngleSharp-powered
scraper (`TimeAndDateHolidayScraper`) implements `IHolidayImporter` in Infrastructure and is
invoked only on explicit user action. Serbian holidays for the current year are seeded on first
run (when `HolidayYearRange` row does not yet exist).

---

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+ (FluentTheme), ReactiveUI, EF Core 8 (SQLite provider),
AngleSharp 1.x, Microsoft.Extensions.DependencyInjection, xUnit + FluentAssertions + NSubstitute  
**Storage**: SQLite via EF Core 8; two new tables `PublicHolidays` + `HolidayYearRange` created by
migration `0003_HolidayConfiguration`  
**Testing**: xUnit + FluentAssertions + NSubstitute; SQLite in-memory for Infrastructure integration
tests  
**Target Platform**: Windows / macOS cross-platform desktop (Avalonia)  
**Project Type**: Desktop application  
**Performance Goals**: Save operation < 200 ms on local SQLite with typical holiday counts (≤ 50
rows/year); Import (web fetch + parse) expected < 3 s on broadband  
**Constraints**: Single-user, offline-first; outbound HTTP only on explicit user click; no
`.Result`/`.Wait()`; no hard-coded UI strings; all dates `DateOnly`  
**Scale/Scope**: Singleton `HolidayYearRange` (Id=1); up to ~50 holiday rows per year; ~35 new
tests across 4 test classes

---

## Constitution Check

_GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design._

- [x] **Clean Architecture boundary is preserved** (`Desktop -> Application -> Domain`; Infrastructure
  implements Application contracts only). `HolidaySettingsViewModel` calls `ICommandHandler` /
  `IQueryHandler` only; `IHolidayRepository` and `IHolidayImporter` are defined in Application;
  `TimeAndDateHolidayScraper` and `HolidayRepository` live in Infrastructure. Desktop has no direct
  reference to EF Core or AngleSharp.
- [x] **All monetary/rate/percentage values are modeled as `decimal`**. Holiday configuration contains
  no monetary fields; rule is not triggered.
- [x] **All business dates are modeled as `DateOnly`**. `PublicHoliday.Date` is `DateOnly`; web scraper
  parses raw strings to `DateOnly` at the Infrastructure boundary; `DateTime` is prohibited throughout
  the feature.
- [x] **Security/privacy constraints hold**: all data stored locally in SQLite; no credentials involved;
  outbound HTTP is user-initiated on-demand only (CA-EXT-001 amendment recorded in clarify.md).
- [x] **External network usage explicitly justified**: `timeanddate.com` scraping is approved as a
  user-initiated, on-demand exception (CA-EXT-001). The scraper is not called automatically.
  Amendment documented in `.specify/specs/003-holiday-configuration/clarify.md`.
- [x] **All I/O paths are async; UI avoids blocking calls**: repository methods are `async Task<T>`;
  handlers are `async Task<Result<T>>`; `HolidaySettingsViewModel` uses `ReactiveCommand.CreateFromTask`;
  UI updates scheduled via `RxApp.MainThreadScheduler`.
- [x] **Tests and coverage impact defined**: Domain — 100% rule/state coverage (HolidayYearRange
  validation, PublicHoliday construction). Application — ≥ 90% (3 handler test classes, NSubstitute
  mocks). Infrastructure — EF Core InMemory integration tests for `HolidayRepository`.
  Desktop — `HolidaySettingsViewModelTests` covering add/delete/save/import commands.
- [x] **Feature work mapped to approved spec task**: branch `feature/003-holiday-configuration`; spec
  `003` under `.specify/specs/003-holiday-configuration/`.

**Result**: ✅ All 8 gates PASS. One approved constitution amendment (CA-EXT-001) for
`timeanddate.com` user-initiated HTTP access. No gate violations.

---

## Project Structure

### Documentation (this feature)

```text
.specify/specs/003-holiday-configuration/
├── plan.md              ← This file (speckit.plan output)
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   ├── IHolidayRepository.cs   ← interface signature (C# snippet)
│   └── IHolidayImporter.cs     ← interface signature (C# snippet)
└── tasks.md             ← Phase 2 output (speckit.tasks — NOT created by speckit.plan)
```

### Source Code Changes

```text
── DOMAIN LAYER ───────────────────────────────────────────────────────────────────

src/Rentier.Domain/Entities/PublicHoliday.cs
    NEW — Guid Id, DateOnly Date, string Name, int Year.
          Private parameterless EF constructor; private setters.
          Static factory: Create(DateOnly date, string name) → PublicHoliday.

src/Rentier.Domain/Entities/HolidayYearRange.cs
    NEW — int Id (always 1, singleton), int StartYear, int EndYear.
          Constructor validates StartYear >= 2020 and EndYear <= StartYear + 10;
          throws DomainException on violation.

── APPLICATION LAYER ──────────────────────────────────────────────────────────────

src/Rentier.Application/DTOs/HolidayEntryDto.cs
    NEW — sealed record (DateOnly Date, string Name).

src/Rentier.Application/DTOs/HolidayConfDto.cs
    NEW — sealed record (IReadOnlyList<HolidayEntryDto> Holidays, int StartYear, int EndYear).

src/Rentier.Application/Interfaces/IHolidayRepository.cs
    NEW — GetHolidayConfAsync, GetYearRangeAsync, SaveHolidaysAsync.
          (See contracts/IHolidayRepository.cs for full signature.)

src/Rentier.Application/Interfaces/IHolidayImporter.cs
    NEW — ImportAsync(int year, CancellationToken) → Task<Result<IReadOnlyList<HolidayEntryDto>>>.
          (See contracts/IHolidayImporter.cs for full signature.)

src/Rentier.Application/Commands/SaveHolidayConfCommand.cs
    NEW — sealed record with IReadOnlyList<HolidayEntryDto> Holidays, int StartYear, int EndYear.

src/Rentier.Application/Commands/ImportHolidaysFromWebCommand.cs
    NEW — sealed record with int Year.

src/Rentier.Application/Queries/GetHolidayConfQuery.cs
    NEW — sealed record (no parameters).

src/Rentier.Application/Handlers/SaveHolidayConfCommandHandler.cs
    NEW — ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>.
          1. Validate year range: new HolidayYearRange(cmd.StartYear, cmd.EndYear) in try/catch;
             on DomainException return Result<VoidResult, Error>.Failure(INVALID_YEAR_RANGE).
          2. Validate no duplicate dates in cmd.Holidays; on duplicates return
             Result<VoidResult, Error>.Failure(DUPLICATE_DATES) (FR-006).
          3. Build IReadOnlyList<PublicHoliday> from cmd.Holidays.
          4. Call SaveHolidaysAsync; return Result<VoidResult, Error>.Success(VoidResult.Value).
          NOTE: seeding is handled by GetHolidayConfQueryHandler on first load (FR-009).

src/Rentier.Application/Handlers/GetHolidayConfQueryHandler.cs
    NEW — IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>.
          1. Call GetYearRangeAsync; if null (first run, FR-009), seed 9 Serbian holidays
             for current year, create HolidayYearRange(currentYear, currentYear+3), save
             via SaveHolidaysAsync → persists before displaying (FR-009 compliant).
          2. Call GetHolidayConfAsync; return Result<HolidayConfDto, Error>.Success(dto).

src/Rentier.Application/Handlers/ImportHolidaysFromWebCommandHandler.cs
    NEW — ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>>>.
          Delegates to IHolidayImporter.ImportAsync; returns result without saving.

── INFRASTRUCTURE LAYER ───────────────────────────────────────────────────────────

src/Rentier.Infrastructure/Persistence/Configurations/PublicHolidayConfiguration.cs
    NEW — IEntityTypeConfiguration<PublicHoliday>; table "PublicHolidays"; PK Guid Id;
          required Name (maxLength 200); DateOnly→string conversion; index on Year.

src/Rentier.Infrastructure/Persistence/Configurations/HolidayYearRangeConfiguration.cs
    NEW — IEntityTypeConfiguration<HolidayYearRange>; table "HolidayYearRange"; PK int Id;
          HasData seed for Id=1 omitted (seeded via Application layer, not migration data seed).

src/Rentier.Infrastructure/Persistence/AppDbContext.cs
    MODIFIED — add DbSet<PublicHoliday> PublicHolidays and DbSet<HolidayYearRange> HolidayYearRange.

src/Rentier.Infrastructure/Repositories/HolidayRepository.cs
    NEW — implements IHolidayRepository; uses AppDbContext (Transient).
          GetHolidayConfAsync: query all holidays ordered by Date + query HolidayYearRange.
          SaveHolidaysAsync: ExecuteDeleteAsync on existing rows + AddRange new rows +
          Upsert HolidayYearRange by EF tracking state.

src/Rentier.Infrastructure/Scraping/TimeAndDateHolidayScraper.cs
    NEW — implements IHolidayImporter.
          Fetches https://www.timeanddate.com/holidays/serbia/{year}?hol=1 via HttpClient.
          Parses HTML with AngleSharp IBrowsingContext.
          Selects table rows excluding elements with class "noshow" or "js-holiday-private".
          Extracts date cell + name cell; parses date string to DateOnly.
          Returns Result<IReadOnlyList<HolidayEntryDto>> (failure on HTTP error or parse error).

src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs
    MODIFIED — add:
          services.AddTransient<IHolidayRepository, HolidayRepository>();
          services.AddHttpClient<IHolidayImporter, TimeAndDateHolidayScraper>();

src/Rentier.Infrastructure/Rentier.Infrastructure.csproj
    MODIFIED — add <PackageReference Include="AngleSharp" Version="1.*" />.

src/Rentier.Infrastructure/Persistence/Migrations/0003_HolidayConfiguration.cs  (+ Snapshot)
    NEW — EF migration; creates PublicHolidays table and HolidayYearRange table.
    COMMAND: dotnet ef migrations add 0003_HolidayConfiguration \
               --project src/Rentier.Infrastructure \
               --startup-project src/Rentier.Desktop

── DESKTOP LAYER ──────────────────────────────────────────────────────────────────

src/Rentier.Desktop/ViewModels/HolidayEntryViewModel.cs
    NEW — ReactiveObject wrapping a single editable row.
          Properties: DateText (string, two-way), Name (string, two-way).
          Converts DateText → DateOnly on validation; exposes HasDateError (bool).

src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs
    NEW — ReactiveObject.
          ObservableCollection<HolidayEntryViewModel> Entries.
          int StartYear, int EndYear (RaiseAndSetIfChanged).
          ReactiveCommand AddRowCommand, DeleteRowCommand, SaveCommand, ImportCommand.
          string ErrorMessage, string SuccessMessage, bool IsLoading (RaiseAndSetIfChanged).
          On activate: dispatches GetHolidayConfQuery.
          SaveCommand: dispatches SaveHolidayConfCommand.
          ImportCommand: prompts for year → dispatches ImportHolidaysFromWebCommand →
                         merges result into Entries (does NOT auto-save).

src/Rentier.Desktop/Views/HolidaySettingsView.axaml
    NEW — ReactiveUserControl<HolidaySettingsViewModel>.
          DataGrid bound to Entries with columns: Date (TextBox), Name (TextBox).
          Toolbar: Add, Delete, Save, Import buttons.
          Year range: two NumericUpDown controls (StartYear, EndYear).
          Error/Success message TextBlocks.

src/Rentier.Desktop/Views/HolidaySettingsView.axaml.cs
    NEW — ReactiveUserControl<HolidaySettingsViewModel> code-behind; WhenActivated.

src/Rentier.Desktop/ViewModels/SettingsViewModel.cs
    MODIFIED — add HolidayTab property (HolidaySettingsViewModel).
               Constructor receives HolidaySettingsViewModel; sets HolidayTab.

src/Rentier.Desktop/Views/SettingsView.axaml
    MODIFIED — add "Holidays" TabItem hosting HolidaySettingsView;
               DataContext bound to SettingsViewModel.HolidayTab.

src/Rentier.Desktop/Composition/CompositionRoot.cs
    MODIFIED — register:
               services.AddTransient<HolidaySettingsViewModel>();
               services.AddTransient<SaveHolidayConfCommandHandler>();
               services.AddTransient<GetHolidayConfQueryHandler>();
               services.AddTransient<ImportHolidaysFromWebCommandHandler>();

src/Rentier.Desktop/Resources/Strings.resx
    MODIFIED — add keys:
               Settings_Holidays_TabHeader, Holidays_AddRow_Button,
               Holidays_DeleteRow_Button, Holidays_Save_Button,
               Holidays_Import_Button, Holidays_Date_Column,
               Holidays_Name_Column, Holidays_StartYear_Label,
               Holidays_EndYear_Label, Holidays_Saved_Confirmation,
               Holidays_ImportError_Prefix, Holidays_ImportYear_Prompt,
               Holidays_InvalidDate_Error, Holidays_UnsavedChanges_Label.

── TEST PROJECTS ──────────────────────────────────────────────────────────────────

tests/Rentier.Domain.Tests/HolidayYearRangeTests.cs
    NEW — ValidRange_NoThrow; StartYearBelowMinimum_ThrowsDomainException;
          EndYearExceedsMax_ThrowsDomainException; EndYearEqualsStartPlusTen_Valid;
          StartYearEqualsMin_Valid.

tests/Rentier.Domain.Tests/PublicHolidayTests.cs
    NEW — Create_ValidInputs_ReturnsEntity; Create_EmptyName_ThrowsDomainException;
          YearProperty_MatchesDateYear.

tests/Rentier.Application.Tests/SaveHolidayConfCommandHandlerTests.cs
    NEW — FirstSave_NoExistingRange_SeedsAndPersists;
          SubsequentSave_ExistingRange_ReplacesHolidays;
          SaveEmptyList_Allowed_NoReseed.

tests/Rentier.Application.Tests/GetHolidayConfQueryHandlerTests.cs
    NEW — NoHolidays_ReturnsEmptyDto; ExistingHolidays_ReturnsMappedDto.

tests/Rentier.Application.Tests/ImportHolidaysFromWebCommandHandlerTests.cs
    NEW — ImporterSuccess_ReturnsHolidayList; ImporterFailure_ReturnsFailureResult.

tests/Rentier.Infrastructure.Tests/HolidayRepositoryTests.cs
    NEW — SaveThenGet_RoundTrip; Replace_DeletesOldRows; YearRange_Upsert.

tests/Rentier.Desktop.Tests/HolidaySettingsViewModelTests.cs
    NEW — OnActivate_LoadsHolidays; AddRow_AppendsEntry; DeleteRow_RemovesEntry;
          SaveCommand_DispatchesSaveHolidayConfCommand;
          ImportCommand_OnSuccess_MergesIntoEntries;
          ImportCommand_OnFailure_SetsErrorMessage.
```

---

## Seeding Logic

**Trigger**: `GetHolidayConfQueryHandler.HandleAsync` calls `GetYearRangeAsync()` and checks
whether `HolidayYearRange` row (Id = 1) exists. Seeding occurs **before displaying the
Holidays tab** (FR-009 requirement).

- **Does NOT exist** → seed current-year Serbian holidays + insert `HolidayYearRange(Id=1,
  StartYear=currentYear, EndYear=currentYear+3)` via `SaveHolidaysAsync` → then return
  the seeded data to the ViewModel.
- **Already exists** → skip seeding; read and return existing data normally.

`SaveHolidayConfCommandHandler` does **not** seed. It validates and persists exactly
what the user has in the UI. The edge case "deliberate save of empty list" (spec.md)
is handled correctly because by the time Save is clicked, `HolidayYearRange` already
exists (seeded on first load), so no re-seeding occurs.

**Seed data** (current year `Y`):

| Date | Name |
|------|------|
| Y-01-01 | New Year's Day |
| Y-01-02 | New Year's Day (observed) |
| Y-01-07 | Orthodox Christmas |
| Y-02-15 | Statehood Day (Sretenje) |
| Y-02-16 | Statehood Day (Sretenje, 2nd day) |
| Y-05-01 | Labour Day |
| Y-05-02 | Labour Day (2nd day) |
| Y-06-28 | St. Vitus Day (Vidovdan) |
| Y-11-11 | Armistice Day |

---

## Import Flow

1. User clicks **Import** in `HolidaySettingsView`.
2. ViewModel shows year-input prompt (NumericUpDown inline or small overlay).
3. User confirms year → `ImportHolidaysFromWebCommand { Year = y }` dispatched.
4. `ImportHolidaysFromWebCommandHandler` calls `IHolidayImporter.ImportAsync(y, ct)`.
5. `TimeAndDateHolidayScraper.ImportAsync`:
   - `GET https://www.timeanddate.com/holidays/serbia/{year}?hol=1`
   - Parse with AngleSharp; select `table.table` rows.
   - Exclude `<tr class="noshow">` and `<tr class="js-holiday-private">`.
   - Each visible row: extract date text cell → parse to `DateOnly`; extract name cell.
   - Return `Result<IReadOnlyList<HolidayEntryDto>>`.
6. On success: ViewModel replaces `Entries` collection with imported rows.
   User must click **Save** to persist — import does NOT auto-save.
7. On failure: ViewModel sets `ErrorMessage`; `Entries` unchanged.

---

## Migration Notes

**Migration name**: `0003_HolidayConfiguration`  
**Command**:
```bash
dotnet ef migrations add 0003_HolidayConfiguration \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Desktop
```

**New tables**:

| Table | Key Columns |
|-------|-------------|
| `PublicHolidays` | `Id` (GUID PK), `Date` (TEXT stored as ISO-8601), `Name` (TEXT NOT NULL, maxlen 200), `Year` (INT) |
| `HolidayYearRange` | `Id` (INT PK, always 1), `StartYear` (INT NOT NULL), `EndYear` (INT NOT NULL) |

**No HasData seeding in migration** — seeding performed by Application layer on first use.

---

## Design Notes

### Replace-All Save Strategy (FR-015 / A-005)

`HolidayRepository.SaveHolidaysAsync`:
1. `ExecuteDeleteAsync` on `PublicHolidays` table (removes all rows for all years).
2. `AddRange(newEntities)`.
3. `SaveChangesAsync()`.

This avoids diffing logic at the cost of a full replace on every save — acceptable for ≤ 50 rows.

### ViewModel Structure

```
SettingsViewModel
├── ProfileTab  : ProfileSettingsViewModel   ← feature 002
└── HolidayTab  : HolidaySettingsViewModel   ← feature 003 (new)
```

`SettingsView.axaml` hosts a `TabControl`; existing Profile `TabItem` unchanged;
new Holidays `TabItem` added with `HolidaySettingsView` content.

### ReactiveUI Patterns

- All mutable ViewModel properties use `this.RaiseAndSetIfChanged(ref _field, value)` — NO `[Reactive]` / Fody, NO `[ObservableProperty]` / CommunityToolkit on ViewModels.
- Commands: `ReactiveCommand.CreateFromTask(...)`.
- UI thread scheduling: `ObserveOn(RxApp.MainThreadScheduler)`.

### DateOnly Storage in EF Core 8

EF Core 8 supports `DateOnly` natively with the SQLite provider (stored as TEXT `"yyyy-MM-dd"`).
No explicit `HasConversion` required. Confirmed in research.md.

### AngleSharp Integration

`TimeAndDateHolidayScraper` is registered via `AddHttpClient<IHolidayImporter, TimeAndDateHolidayScraper>()`.
This single call registers both the `IHolidayImporter` interface and a named `HttpClient`
managed by `IHttpClientFactory` (preventing socket exhaustion).
`IBrowsingContext` is created per-request (not injected) to remain thread-safe.
See `research.md` for DOM structure notes and CSS selector details.
