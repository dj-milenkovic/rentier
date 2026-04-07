---
description: "Task list for Feature 003: Holiday Configuration"
---

# Tasks: Holiday Configuration (Feature 003)

**Input**: Design documents from `.specify/specs/003-holiday-configuration/`  
**Branch**: `feature/003-holiday-configuration`  
**Prerequisites**: spec.md ✅  plan.md ✅  data-model.md ✅  contracts/ ✅

**Tests**: Included — domain validation, application handler logic, infrastructure round-trips,
and Desktop ViewModel commands must all have coverage tasks. Handlers and domain entities require
≥ 90 % branch coverage as per the project constitution quality gates.

**Organization**: Tasks are grouped by technical layer in strict dependency order
(Domain → Application → Infrastructure → Desktop → DI). This ordering is mandatory for this
feature because every user story (US1–US4) shares the same domain entities, EF tables, and
ViewModel; splitting by user story would produce artificial cross-cutting dependencies.  
Layer badges (`[DOMAIN]`, `[APPLICATION]`, `[INFRASTRUCTURE]`, `[DESKTOP]`, `[TEST]`, `[DI]`)
replace the `[USx]` label in every task; user-story traceability is captured in the Dependencies
section below.

## Format: `[ID] [P?] [LAYER] Description`

- **[P]**: Can run in parallel with adjacent tasks (different files, no blocking dependency)
- **[LAYER]**: Technical layer — `[DOMAIN]` · `[APPLICATION]` · `[INFRASTRUCTURE]` · `[DESKTOP]` · `[TEST]` · `[DI]`
- All tasks include exact file paths and implementation-critical details

---

## Phase 1: Domain Entities + Domain Tests

**Purpose**: Create the two new domain entities and their unit tests. These have no EF or
infrastructure dependencies and can proceed immediately.

**User stories served**: T001–T002 underpin US1/US2/US3/US4. T003 covers US3. T004 covers US1/US2.

- [x] T001 [DOMAIN] Create `PublicHoliday` sealed entity with `Guid Id`, `DateOnly Date`, `string Name`, `int Year` (all `private set`); add `private PublicHoliday() { }` parameterless constructor for EF Core materialisation; add `private PublicHoliday(Guid id, DateOnly date, string name)` that throws `DomainException("Holiday name must not be empty.")` when `string.IsNullOrWhiteSpace(name)`, sets `Id = id`, `Date = date`, `Name = name`, `Year = date.Year`; add `public static PublicHoliday Create(DateOnly date, string name) => new(Guid.NewGuid(), date, name);` factory method — `src/Rentier.Domain/Entities/PublicHoliday.cs`
- [x] T002 [P] [DOMAIN] Create `HolidayYearRange` sealed entity with `public const int SingletonId = 1`, `public const int MinStartYear = 2020`, `public const int MaxYearSpan = 10`; properties `int Id`, `int StartYear`, `int EndYear` (all `private set`); add `private HolidayYearRange() { }` parameterless constructor for EF Core materialisation; add `public HolidayYearRange(int startYear, int endYear)` constructor that throws `DomainException($"StartYear must be >= {MinStartYear}.")` when `startYear < MinStartYear`, throws `DomainException($"EndYear must be <= StartYear + {MaxYearSpan}.")` when `endYear > startYear + MaxYearSpan`, throws `DomainException("EndYear must be >= StartYear.")` when `endYear < startYear`, then sets `Id = SingletonId`, `StartYear = startYear`, `EndYear = endYear` — `src/Rentier.Domain/Entities/HolidayYearRange.cs`
- [x] T003 [TEST] Create `HolidayYearRangeTests` with five test methods: `ValidRange_NoThrow()` — `new HolidayYearRange(2024, 2027)` must not throw; `StartYearBelowMinimum_ThrowsDomainException()` — `new HolidayYearRange(2019, 2024)` must throw `DomainException`; `EndYearExceedsMax_ThrowsDomainException()` — `new HolidayYearRange(2024, 2035)` must throw `DomainException`; `EndYearEqualsStartPlusTen_IsValid()` — `new HolidayYearRange(2024, 2034)` must not throw; `EndYearLessThanStartYear_ThrowsDomainException()` — `new HolidayYearRange(2025, 2024)` must throw `DomainException`; use `FluentAssertions`; no EF or NSubstitute required — `tests/Rentier.Domain.Tests/HolidayYearRangeTests.cs`
- [x] T004 [P] [TEST] Create `PublicHolidayTests` with three test methods: `Create_ValidInputs_ReturnsEntity()` — `PublicHoliday.Create(new DateOnly(2025, 1, 1), "New Year's Day")` must return entity with `Id != Guid.Empty`, `Date.Year == 2025`, `Name == "New Year's Day"`, `Year == 2025`; `Create_EmptyName_ThrowsDomainException()` — `PublicHoliday.Create(new DateOnly(2025, 1, 1), "   ")` must throw `DomainException`; `YearProperty_MatchesDateYear()` — `Create` with `DateOnly(2026, 6, 28)` must have `Year == 2026`; use `FluentAssertions` — `tests/Rentier.Domain.Tests/PublicHolidayTests.cs`

**Checkpoint**: `dotnet test tests/Rentier.Domain.Tests` — all 8 domain tests pass with zero warnings.

---

## Phase 2: Application Interfaces

**Purpose**: Define the two new Application-layer contracts that Infrastructure will implement.
These are pure C# interfaces; no implementation required yet.

**User stories served**: T005 supports US1/US2/US3. T006 supports US4 exclusively.

- [x] T005 [APPLICATION] Create `IHolidayRepository` interface exactly matching the contract in `contracts/IHolidayRepository.cs`: three methods — `Task<HolidayConfDto> GetHolidayConfAsync(CancellationToken cancellationToken = default)` (returns all persisted holidays as DTOs ordered by Date + year range; empty list + null-equivalent when no data); `Task<HolidayYearRange?> GetYearRangeAsync(CancellationToken cancellationToken = default)` (returns singleton Id=1 or null on first run); `Task SaveHolidaysAsync(IReadOnlyList<PublicHoliday> holidays, HolidayYearRange yearRange, CancellationToken cancellationToken = default)` (replace-all strategy) — `src/Rentier.Application/Interfaces/IHolidayRepository.cs`
- [x] T006 [P] [APPLICATION] Create `IHolidayImporter` interface exactly matching the contract in `contracts/IHolidayImporter.cs`: single method `Task<Result<IReadOnlyList<HolidayEntryDto>>> ImportAsync(int year, CancellationToken cancellationToken = default)`; add XML-doc comment: "IMPORTANT: This interface MUST NOT be called automatically. Only invoked on explicit user action. (Constitution amendment CA-EXT-001)" — `src/Rentier.Application/Interfaces/IHolidayImporter.cs`

**Checkpoint**: Solution builds with zero errors. Interfaces compile without warnings.

---

## Phase 3: Application DTOs, Commands, and Queries

**Purpose**: Define the data-transfer and CQRS record types that bind handlers to the Desktop
layer. All five records are independent of each other and can be created in any order.

**User stories served**: T007 serves US1/US2/US4. T008 serves US1/US3. T009 serves US1.
T010 serves US2/US3. T011 serves US4.

- [x] T007 [APPLICATION] Create `HolidayEntryDto` sealed record: `public sealed record HolidayEntryDto(DateOnly Date, string Name);` — `src/Rentier.Application/DTOs/HolidayEntryDto.cs`
- [x] T008 [P] [APPLICATION] Create `HolidayConfDto` sealed record: `public sealed record HolidayConfDto(IReadOnlyList<HolidayEntryDto> Holidays, int StartYear, int EndYear);` — `src/Rentier.Application/DTOs/HolidayConfDto.cs`
- [x] T009 [P] [APPLICATION] Create `GetHolidayConfQuery` sealed record with no parameters: `public sealed record GetHolidayConfQuery();`; the query return type (used by the handler) is `Result<HolidayConfDto, Error>` — `src/Rentier.Application/Queries/GetHolidayConfQuery.cs`
- [x] T010 [P] [APPLICATION] Create `SaveHolidayConfCommand` sealed record: `public sealed record SaveHolidayConfCommand(IReadOnlyList<HolidayEntryDto> Holidays, int StartYear, int EndYear);`; return type for the handler is `Result<VoidResult, Error>` — `src/Rentier.Application/Commands/SaveHolidayConfCommand.cs`
- [x] T011 [P] [APPLICATION] Create `ImportHolidaysFromWebCommand` sealed record: `public sealed record ImportHolidaysFromWebCommand(int Year);`; return type for the handler is `Result<IReadOnlyList<HolidayEntryDto>, Error>` — `src/Rentier.Application/Commands/ImportHolidaysFromWebCommand.cs`

**Checkpoint**: Solution builds with zero errors. All five records compile.

---

## Phase 4: Application Handlers

**Purpose**: Implement the three CQRS handlers. Each handler depends on the interfaces (Phase 2)
and the command/query/DTO types (Phase 3). Handlers T012 and T013 can be written in parallel;
T014 can also be written in parallel with T012 and T013.

**User stories served**: T012 serves US1. T013 serves US2/US3. T014 serves US4.

- [x] T012 [APPLICATION] Create `GetHolidayConfQueryHandler` implementing `IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>`: constructor-inject `IHolidayRepository`; `HandleAsync` must: (1) call `await _repository.GetYearRangeAsync(ct)` — if result is **null** (first run, FR-009), seed 9 Serbian public holidays for `DateTime.Today.Year` (see seeding table in plan.md) by constructing `PublicHoliday.Create(...)` for each, create `HolidayYearRange(DateTime.Today.Year, DateTime.Today.Year + 3)`, call `await _repository.SaveHolidaysAsync(seededHolidays, yearRange, ct)` to persist before displaying; (2) call `await _repository.GetHolidayConfAsync(ct)`; (3) return `Result<HolidayConfDto, Error>.Success(dto)` — `src/Rentier.Application/Handlers/GetHolidayConfQueryHandler.cs`
- [x] T013 [P] [APPLICATION] Create `SaveHolidayConfCommandHandler` implementing `ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>`: constructor-inject `IHolidayRepository`; `HandleAsync` must: (1) validate the commanded year range by constructing `new HolidayYearRange(cmd.StartYear, cmd.EndYear)` inside a try/catch — on `DomainException` return `Result<VoidResult, Error>.Failure(new Error("INVALID_YEAR_RANGE", ex.Message))`; (2) validate for duplicate dates: if `cmd.Holidays` contains two or more entries with the same `DateOnly` value, return `Result<VoidResult, Error>.Failure(new Error("DUPLICATE_DATES", "Holiday list contains duplicate dates."))`; (3) build `IReadOnlyList<PublicHoliday>` from `cmd.Holidays` using `PublicHoliday.Create(dto.Date, dto.Name)`; (4) call `await _repository.SaveHolidaysAsync(holidays, yearRange, ct)`; (5) return `Result<VoidResult, Error>.Success(VoidResult.Value)` — NOTE: seeding is handled by `GetHolidayConfQueryHandler` on first load (FR-009); this handler does NOT seed — `src/Rentier.Application/Handlers/SaveHolidayConfCommandHandler.cs`
- [x] T014 [P] [APPLICATION] Create `ImportHolidaysFromWebCommandHandler` implementing `ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>`: constructor-inject `IHolidayImporter`; `HandleAsync` calls `return await _importer.ImportAsync(cmd.Year, ct)` — the handler returns the `Result<IReadOnlyList<HolidayEntryDto>, Error>` directly without saving; add no additional logic (the ViewModel decides whether to save) — `src/Rentier.Application/Handlers/ImportHolidaysFromWebCommandHandler.cs`

**Checkpoint**: Solution builds with zero errors. All three handlers compile.

---

## Phase 5: Application Handler Tests

**Purpose**: Unit test all three handlers using NSubstitute mocks. Tests in this phase can all
be written in parallel since they target different test classes.

**User stories served**: T015 covers US1. T016 covers US2/US3. T017 covers US4.

- [x] T015 [TEST] Create `GetHolidayConfQueryHandlerTests` with three test methods: `FirstRun_NoYearRange_SeedsAndReturnsDto()` — mock `IHolidayRepository.GetYearRangeAsync` returns `null`; assert `SaveHolidaysAsync` is called once with a non-empty holiday list (seeded Serbian holidays) and a `HolidayYearRange` with `StartYear = DateTime.Today.Year`; assert handler returns `Result<HolidayConfDto, Error>` where `IsSuccess == true`; `EmptyDatabase_AfterSeed_ReturnsPopulatedDto()` — (continuation of first-run scenario) after seeding, mock `GetHolidayConfAsync` returns a pre-populated dto; assert handler returns `Result<HolidayConfDto, Error>.Success` with that dto; `PopulatedDatabase_ReturnsMappedDto()` — mock `GetYearRangeAsync` returns an existing range (not null, skip seeding path); mock `GetHolidayConfAsync` returns pre-populated `HolidayConfDto` with two `HolidayEntryDto` entries and `StartYear = 2025`, `EndYear = 2028`; handler must return `Result<HolidayConfDto, Error>.Success` with exactly those values; use `NSubstitute` for the mock and `FluentAssertions` for assertions — `tests/Rentier.Application.Tests/GetHolidayConfQueryHandlerTests.cs`
- [x] T016 [P] [TEST] Create `SaveHolidayConfCommandHandlerTests` with four test methods: `ValidCommand_SavesHolidays()` — pass command with one holiday and valid year range `(2025, 2028)`; mock `SaveHolidaysAsync` does nothing; assert `SaveHolidaysAsync` is called once with that holiday and result is `Result<VoidResult, Error>` with `IsSuccess == true`; `SaveEmptyList_Allowed()` — pass command with empty `Holidays` and valid year range; assert `SaveHolidaysAsync` called with empty list and handler returns `IsSuccess == true`; `InvalidYearRange_ReturnsDomainError()` — pass `SaveHolidayConfCommand([], 2019, 2025)` (StartYear < 2020); assert handler returns `IsSuccess == false` and `Error.Code == "INVALID_YEAR_RANGE"`; assert `SaveHolidaysAsync` was NOT called; `DuplicateDates_ReturnsDuplicateError()` — pass command with two entries having the same `DateOnly` value; assert handler returns `IsSuccess == false` and `Error.Code == "DUPLICATE_DATES"`; assert `SaveHolidaysAsync` was NOT called; use `NSubstitute` and `FluentAssertions` — `tests/Rentier.Application.Tests/SaveHolidayConfCommandHandlerTests.cs`
- [x] T017 [P] [TEST] Create `ImportHolidaysFromWebCommandHandlerTests` with two test methods: `ImporterSuccess_ReturnsHolidayList()` — mock `IHolidayImporter.ImportAsync` returns `Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(new List<HolidayEntryDto> { new(new DateOnly(2025,1,1), "New Year") })`; handler returns result where `IsSuccess == true` and `Value` contains that list; `ImporterFailure_ReturnsFailureResult()` — mock returns `Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(new Error("FETCH_FAILED", "HTTP 503"))`; handler returns result where `IsSuccess == false` with same error (handler passes through); use `NSubstitute` and `FluentAssertions` — `tests/Rentier.Application.Tests/ImportHolidaysFromWebCommandHandlerTests.cs`

**Checkpoint**: `dotnet test tests/Rentier.Application.Tests` — all handler tests pass.

---

## Phase 6: Infrastructure EF Configuration + Migration

**Purpose**: Add AngleSharp, configure EF entity mappings, extend `AppDbContext`, and generate
the EF Core migration. T019 and T020 are independent and can be written in parallel.
T021 depends on T019 + T020; T022 depends on T021.

**User stories served**: T018 supports US4. T019 supports US1/US2. T020 supports US3.
T021–T022 support US1/US2/US3/US4 (all require the migration).

- [x] T018 [INFRASTRUCTURE] Add AngleSharp NuGet reference to Infrastructure project: add `<PackageReference Include="AngleSharp" Version="1.*" />` inside the `<ItemGroup>` element — `src/Rentier.Infrastructure/Rentier.Infrastructure.csproj`
- [x] T019 [P] [INFRASTRUCTURE] Create `PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>` (internal sealed): `builder.ToTable("PublicHolidays")`; `builder.HasKey(h => h.Id)`; `builder.Property(h => h.Name).IsRequired().HasMaxLength(200)`; `builder.Property(h => h.Date).IsRequired()` (EF Core 8 handles `DateOnly` natively with SQLite — no `HasConversion` needed); `builder.Property(h => h.Year).IsRequired()`; `builder.HasIndex(h => h.Year)` — `src/Rentier.Infrastructure/Persistence/Configurations/PublicHolidayConfiguration.cs`
- [x] T020 [P] [INFRASTRUCTURE] Create `HolidayYearRangeConfiguration : IEntityTypeConfiguration<HolidayYearRange>` (internal sealed): `builder.ToTable("HolidayYearRange")`; `builder.HasKey(r => r.Id)`; `builder.Property(r => r.StartYear).IsRequired()`; `builder.Property(r => r.EndYear).IsRequired()`; add XML-doc comment: "No HasData seeding here — seeding is handled by Application layer (SaveHolidayConfCommandHandler) on first run" — `src/Rentier.Infrastructure/Persistence/Configurations/HolidayYearRangeConfiguration.cs`
- [x] T021 [INFRASTRUCTURE] Modify `AppDbContext`: add `public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();` and `public DbSet<HolidayYearRange> HolidayYearRange => Set<HolidayYearRange>();` properties; if using `ApplyConfigurationsFromAssembly` in `OnModelCreating` these configurations will be picked up automatically — otherwise add `builder.ApplyConfiguration(new PublicHolidayConfiguration())` and `builder.ApplyConfiguration(new HolidayYearRangeConfiguration())` (depends on T019 + T020) — `src/Rentier.Infrastructure/Persistence/AppDbContext.cs`
- [x] T022 [INFRASTRUCTURE] Generate EF Core migration by running: `dotnet ef migrations add 0003_HolidayConfiguration --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop`; confirm the generated `Up()` method creates `PublicHolidays` table (columns: `Id TEXT PK`, `Date TEXT NOT NULL`, `Name TEXT NOT NULL`, `Year INTEGER NOT NULL`) with index `IX_PublicHolidays_Year`, and `HolidayYearRange` table (columns: `Id INTEGER PK`, `StartYear INTEGER NOT NULL`, `EndYear INTEGER NOT NULL`); confirm `Down()` drops both tables (depends on T021) — `src/Rentier.Infrastructure/Persistence/Migrations/0003_HolidayConfiguration.cs` (+ `AppDbContextModelSnapshot.cs` update)

**Checkpoint**: `dotnet build` succeeds. `dotnet ef database update --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop` creates both tables without errors.

---

## Phase 7: Infrastructure Repository + Scraper

**Purpose**: Implement `IHolidayRepository` and `IHolidayImporter`. Both can be written in
parallel since they touch different files.

**User stories served**: T023 serves US1/US2/US3. T024 serves US4.

- [x] T023 [INFRASTRUCTURE] Create `HolidayRepository : IHolidayRepository` (internal sealed): constructor-inject `AppDbContext _db`; implement `GetHolidayConfAsync` — query all `PublicHoliday` rows with `AsNoTracking()` ordered by `Date` ascending, project to `HolidayEntryDto`; query `HolidayYearRange` with `Id == 1` using `AsNoTracking()`; return `new HolidayConfDto(holidays, yearRange?.StartYear ?? 0, yearRange?.EndYear ?? 0)`; implement `GetYearRangeAsync` — `return await _db.HolidayYearRange.AsNoTracking().FirstOrDefaultAsync(r => r.Id == 1, ct)`; implement `SaveHolidaysAsync` — `await _db.PublicHolidays.ExecuteDeleteAsync(ct)` (removes ALL rows), then `_db.PublicHolidays.AddRange(holidays)`, then check existence: `var rangeExists = await _db.HolidayYearRange.AnyAsync(r => r.Id == HolidayYearRange.SingletonId, ct)` — if `rangeExists` is true call `_db.HolidayYearRange.Update(yearRange)` else call `_db.HolidayYearRange.Add(yearRange)`, then `await _db.SaveChangesAsync(ct)` — `src/Rentier.Infrastructure/Repositories/HolidayRepository.cs`
- [x] T024 [P] [INFRASTRUCTURE] Create `TimeAndDateHolidayScraper : IHolidayImporter` (internal sealed): constructor-inject `HttpClient _http`; implement `ImportAsync(int year, CancellationToken ct)`: fetch `https://www.timeanddate.com/holidays/serbia/{year}?hol=1` via `await _http.GetStringAsync(url, ct)` inside a try/catch returning `Result.Fail` on `HttpRequestException` or `TaskCanceledException`; parse with AngleSharp using `IConfiguration config = Configuration.Default.WithDefaultLoader(); IBrowsingContext context = BrowsingContext.New(config);` — create per-request (not injected); parse HTML via `await context.OpenAsync(req => req.Content(html), ct)`; select `table.table` rows with `document.QuerySelectorAll("table.table tr")`; filter out rows where `row.ClassList.Contains("noshow") || row.ClassList.Contains("js-holiday-private")`; for each visible row: get date cell (first `<td>`) and name cell (second `<td class='ce'>`); parse date string to `DateOnly` using `DateOnly.ParseExact(dateText.Trim(), new[]{"MMM d","d MMM","MMM dd","dd MMM"}, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None)` wrapped in try/catch (skip malformed rows); collect results as `List<HolidayEntryDto>`; if list is empty return `Result.Fail(new Error("NO_HOLIDAYS_FOUND", $"No holidays found for year {year}"))`; otherwise return `Result.Ok((IReadOnlyList<HolidayEntryDto>)results)` — `src/Rentier.Infrastructure/Scraping/TimeAndDateHolidayScraper.cs`

**Checkpoint**: Solution builds with zero errors. No `.Result` or `.Wait()` calls present.

---

## Phase 8: Infrastructure Tests

**Purpose**: Integration tests for `HolidayRepository` using SQLite in-memory (EF Core `UseInMemoryDatabase`).

**User stories served**: T025 covers US1/US2/US3.

- [x] T025 [TEST] Create `HolidayRepositoryTests` with four test methods using EF Core `UseInMemoryDatabase` (or `UseSqlite("DataSource=:memory:")` with `db.Database.EnsureCreated()`): `GetHolidayConf_EmptyDatabase_ReturnsEmptyDto()` — fresh db; `GetHolidayConfAsync()` returns `HolidayConfDto` with empty list and `StartYear == 0`, `EndYear == 0`; `GetHolidayConf_WithData_ReturnsSortedDtoAndRange()` — seed one `HolidayYearRange(2025, 2028)` and three `PublicHoliday` rows in unsorted date order; assert returned `Holidays` are sorted by `Date` ascending, `StartYear == 2025`, `EndYear == 2028`; `SaveHolidays_ReplacesAllExistingRows()` — seed two existing holidays; call `SaveHolidaysAsync` with one new holiday and a new year range; query again and assert only one holiday remains (replace-all strategy); `GetYearRange_WhenExists_ReturnsSingleton()` — seed `HolidayYearRange(2024, 2027)`; assert `GetYearRangeAsync()` returns entity with `Id == 1`, `StartYear == 2024`; use `FluentAssertions` — `tests/Rentier.Infrastructure.Tests/HolidayRepositoryTests.cs`

**Checkpoint**: `dotnet test tests/Rentier.Infrastructure.Tests` — all repository tests pass.

---

## Phase 9: Desktop ViewModels

**Purpose**: Implement the two new ViewModels. `HolidaySettingsViewModel` depends on
`HolidayEntryViewModel`, so T026 must complete before T027.

**User stories served**: T026 serves US1/US2. T027 serves US1/US2/US3/US4.

- [x] T026 [DESKTOP] Create `HolidayEntryViewModel : ReactiveObject`: `private DateOnly _date; public DateOnly Date { get => _date; set => this.RaiseAndSetIfChanged(ref _date, value); }` and `private string _name = string.Empty; public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value); }`; add static factory `public static HolidayEntryViewModel FromDto(HolidayEntryDto dto) => new() { Date = dto.Date, Name = dto.Name };`; add `public HolidayEntryDto ToDto() => new HolidayEntryDto(Date, Name);` — NO `[Reactive]` / Fody, NO `[ObservableProperty]` / CommunityToolkit — `src/Rentier.Desktop/ViewModels/HolidayEntryViewModel.cs`
- [x] T027 [DESKTOP] Create `HolidaySettingsViewModel : ReactiveObject, IActivatableViewModel`: fields and `RaiseAndSetIfChanged` properties — `int _startYear`, `int _endYear`, `bool _isLoading`, `string? _errorMessage`, `string? _successMessage`, `int _importYear = DateTime.Today.Year` (year to pass to ImportCommand); `public int ImportYear { get => _importYear; set => this.RaiseAndSetIfChanged(ref _importYear, value); }`; `public ObservableCollection<HolidayEntryViewModel> Entries { get; } = new()`; constructor-inject `IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>> _queryHandler`, `ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>> _saveHandler`, `ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>> _importHandler`; commands: `AddRowCommand = ReactiveCommand.Create(() => Entries.Add(new HolidayEntryViewModel()))`, `public HolidayEntryViewModel? SelectedEntry { get => _selectedEntry; set => this.RaiseAndSetIfChanged(ref _selectedEntry, value); }` (private field `HolidayEntryViewModel? _selectedEntry`); `DeleteRowCommand = ReactiveCommand.Create<HolidayEntryViewModel>(entry => Entries.Remove(entry), this.WhenAnyValue(x => x.SelectedEntry).Select(e => e != null))`; also add `bool _hasUnsavedChanges` property via `RaiseAndSetIfChanged`; set `HasUnsavedChanges = true` in `AddRowCommand`, `DeleteRowCommand` execute, and subscribe to `Entries.CollectionChanged`; set `HasUnsavedChanges = false` on `SaveCommand` success, `SaveCommand = ReactiveCommand.CreateFromTask(async ct => { ... dispatch SaveHolidayConfCommand ... })`, `ImportCommand = ReactiveCommand.CreateFromTask<int>(async (year, ct) => { ... dispatch ImportHolidaysFromWebCommand ... })`; `WhenActivated` block loads data: dispatch `GetHolidayConfQuery`, populate `Entries`, `StartYear`, `EndYear` on `RxApp.MainThreadScheduler`; on `SaveCommand` success set `SuccessMessage`; on failure set `ErrorMessage`; on `ImportCommand` success replace `Entries` contents (does NOT auto-save); on failure set `ErrorMessage`; set `IsLoading = true` at start and `IsLoading = false` in finally for both `SaveCommand` and `ImportCommand`; `public ViewModelActivator Activator { get; } = new()` — NO `[Reactive]` / Fody, NO `[ObservableProperty]` / CommunityToolkit — `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs`

**Checkpoint**: Solution builds with zero errors. ViewModel compiles with no Fody/CommunityToolkit attributes.

---

## Phase 10: Desktop Views

**Purpose**: Create the AXAML view and its code-behind. Both files can be authored in parallel
since the code-behind is a stub (`WhenActivated` binding) referencing the AXAML class.

**User stories served**: T028–T029 expose US1/US2/US3/US4 to the user.

- [x] T028 [DESKTOP] Create `HolidaySettingsView.axaml` as `<UserControl>` (will become `ReactiveUserControl` in code-behind): toolbar `<StackPanel Orientation="Horizontal">` with four `<Button>` controls — Add (`Command="{Binding AddRowCommand}"`), Delete Selected (`Command="{Binding DeleteRowCommand}" CommandParameter="{Binding SelectedItem, ElementName=HolidaysGrid}"`), Import (`Command="{Binding ImportCommand}" CommandParameter="{Binding ImportYear}"`), Save (`Command="{Binding SaveCommand}"`); year range row: two `<NumericUpDown>` controls bound to `StartYear` and `EndYear`; `<DataGrid x:Name="HolidaysGrid" ItemsSource="{Binding Entries}" AutoGenerateColumns="False">` with two `<DataGridTextColumn>` — Header bound to `Holidays_Date_Column` string key, Binding `Date` (editable) and Header `Holidays_Name_Column`, Binding `Name` (editable); feedback row: `<TextBlock Text="{Binding ErrorMessage}" IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}" Foreground="Red" />`; `<TextBlock Text="{Binding SuccessMessage}" IsVisible="{Binding SuccessMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}" Foreground="Green" />`; `<ProgressBar IsIndeterminate="True" IsVisible="{Binding IsLoading}" />`; all user-visible strings must reference `Strings.resx` keys (no hard-coded literals) — `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`
- [x] T029 [P] [DESKTOP] Create `HolidaySettingsView.axaml.cs` code-behind as `public partial class HolidaySettingsView : ReactiveUserControl<HolidaySettingsViewModel>`: add `public HolidaySettingsView()` constructor calling `InitializeComponent()`; add `this.WhenActivated(disposables => { /* bindings if needed */ })` block (bindings in AXAML are sufficient for this view; block can remain empty initially) — `src/Rentier.Desktop/Views/HolidaySettingsView.axaml.cs`

**Checkpoint**: Solution builds with zero AXAML errors. `HolidaySettingsView` renders without runtime exceptions in the Avalonia previewer.

---

## Phase 11: DI Wiring + Integration

**Purpose**: Wire all new types into the DI container, extend `SettingsViewModel` and
`SettingsView`, and add all resource string keys. T030 and T031 can be written in parallel;
T032 and T034 can also be written in parallel with each other after their prerequisites.

**User stories served**: All four user stories become navigable and functional after this phase.

- [x] T030 [DI] Modify `InfrastructureServiceExtensions.cs`: add two registrations inside `AddInfrastructureServices` — `services.AddTransient<IHolidayRepository, HolidayRepository>();`; `services.AddHttpClient<IHolidayImporter, TimeAndDateHolidayScraper>();` (registers both `IHolidayImporter` and a named `HttpClient` factory for the scraper via IHttpClientFactory in a single call — preferred over `AddHttpClient<TimeAndDateHolidayScraper>()` + `AddTransient<IHolidayImporter, TimeAndDateHolidayScraper>()` to avoid double registration); add required `using` statements for `Rentier.Application.Interfaces`, `Rentier.Infrastructure.Repositories`, `Rentier.Infrastructure.Scraping` (depends on T023 + T024) — `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`
- [x] T031 [P] [DI] Modify `CompositionRoot.cs`: add four `AddTransient` registrations — `services.AddTransient<GetHolidayConfQueryHandler>()`; `services.AddTransient<SaveHolidayConfCommandHandler>()`; `services.AddTransient<ImportHolidaysFromWebCommandHandler>()`; `services.AddTransient<HolidaySettingsViewModel>()`; add required `using` statements for `Rentier.Application.Handlers` and `Rentier.Desktop.ViewModels` (depends on T012–T014 + T027) — `src/Rentier.Desktop/Composition/CompositionRoot.cs`
- [x] T032 [P] [DI] Modify `SettingsViewModel.cs`: add `public HolidaySettingsViewModel HolidayTab { get; }` property; add `HolidaySettingsViewModel holidayTab` constructor parameter; set `HolidayTab = holidayTab` in constructor body (depends on T027) — `src/Rentier.Desktop/ViewModels/SettingsViewModel.cs`
- [x] T033 [DESKTOP] Modify `SettingsView.axaml`: inside the existing `<TabControl>`, add a second `<TabItem>` after the Profile tab: `<TabItem Header="{x:Static lang:Strings.Settings_Holidays_TabHeader}"><views:HolidaySettingsView DataContext="{Binding HolidayTab}" /></TabItem>`; add the `views:` XML namespace if not already present (depends on T028 + T032) — `src/Rentier.Desktop/Views/SettingsView.axaml`
- [x] T034 [P] [DI] Add 14 string keys to `Strings.resx`: `Settings_Holidays_TabHeader` = "Holidays"; `Holidays_AddRow_Button` = "Add"; `Holidays_DeleteRow_Button` = "Delete Selected"; `Holidays_Save_Button` = "Save"; `Holidays_Import_Button` = "Import from Web"; `Holidays_Date_Column` = "Date"; `Holidays_Name_Column` = "Name"; `Holidays_StartYear_Label` = "Start Year"; `Holidays_EndYear_Label` = "End Year"; `Holidays_Saved_Confirmation` = "Changes saved successfully."; `Holidays_ImportError_Prefix` = "Import failed: "; `Holidays_ImportYear_Prompt` = "Enter year to import:"; `Holidays_InvalidDate_Error` = "Invalid date format."; `Holidays_UnsavedChanges_Label` = "Unsaved changes" — `src/Rentier.Desktop/Resources/Strings.resx`

**Checkpoint**: Full `dotnet build` succeeds. Launch the app, navigate to Settings → Holidays,
verify the Holidays tab is visible and the DataGrid loads with seeded holidays on first run.

---

## Phase 12: Desktop ViewModel Tests

**Purpose**: Unit test `HolidaySettingsViewModel` commands and state transitions using
NSubstitute mocks. All handler dependencies are injected via constructor.

**User stories served**: T035 covers US1/US2/US4.

- [x] T035 [TEST] Create `HolidaySettingsViewModelTests` with six test methods: `OnActivate_LoadsHolidays()` — mock `GetHolidayConfQueryHandler.HandleAsync` returns `Result<HolidayConfDto, Error>.Success(new HolidayConfDto([new HolidayEntryDto(new DateOnly(2025,1,1), "New Year")], 2025, 2028))`; activate ViewModel; assert `Entries.Count == 1`, `StartYear == 2025`, `EndYear == 2028`; `AddRow_AppendsBlankEntry()` — execute `AddRowCommand`; assert `Entries.Count` increases by one; `DeleteRow_RemovesEntry()` — add an entry; execute `DeleteRowCommand` passing that entry; assert `Entries` no longer contains it; `SaveCommand_DispatchesSaveHolidayConfCommand()` — mock `SaveHolidayConfCommandHandler.HandleAsync` returns `Result<VoidResult, Error>.Success(VoidResult.Value)`; add one entry, execute `SaveCommand`; assert `SaveHolidayConfCommandHandler.HandleAsync` was called once with a command matching `Entries[0].ToDto()`; `ImportCommand_OnSuccess_MergesIntoEntries()` — mock `ImportHolidaysFromWebCommandHandler.HandleAsync` returns `Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(new List<HolidayEntryDto> { new(new DateOnly(2025,3,1), "St. David") })`; execute `ImportCommand` with year `2025`; assert `Entries.Count == 1` and `Entries[0].Name == "St. David"`; assert `SaveCommand` was NOT automatically called (import does not auto-save); `ImportCommand_OnFailure_SetsErrorMessage()` — mock returns `Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(new Error("FETCH_FAILED", "timeout"))`; execute `ImportCommand`; assert `ErrorMessage` is not null/empty and `Entries` is unchanged; use `NSubstitute` and `FluentAssertions`; activate ViewModel with `new TestScheduler()` or `ImmediateScheduler.Instance` to avoid async timing issues — `tests/Rentier.Desktop.Tests/HolidaySettingsViewModelTests.cs`

**Checkpoint**: `dotnet test tests/Rentier.Desktop.Tests` — all 6 ViewModel tests pass.

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1  (Domain entities + tests)     — no dependencies; start immediately
Phase 2  (Application interfaces)      — no dependencies; can run in parallel with Phase 1
Phase 3  (DTOs / Commands / Queries)   — no dependencies; can run in parallel with Phases 1–2
Phase 4  (Application handlers)        — depends on Phase 2 (interfaces) + Phase 3 (DTOs)
Phase 5  (Application handler tests)   — depends on Phase 4 (handlers to test)
Phase 6  (EF config + migration)       — depends on Phase 1 (entities); T022 requires T021
Phase 7  (Repository + Scraper)        — depends on Phase 2 (interfaces) + Phase 6 (DbSets ready)
Phase 8  (Infrastructure tests)        — depends on Phase 7 (repository under test)
Phase 9  (Desktop ViewModels)          — depends on Phase 3 (DTOs) + Phase 4 (handlers); T027 after T026
Phase 10 (Desktop Views)               — depends on Phase 9 (ViewModels)
Phase 11 (DI Wiring)                   — depends on Phases 7 + 9 + 10 (all implementations ready)
Phase 12 (Desktop ViewModel tests)     — depends on Phase 9 (ViewModel under test)
```

### User Story → Task Traceability

| User Story | Blocking Tasks | Primary Tasks |
|---|---|---|
| **US1** View & Edit Holidays (P1) | T001, T005, T007–T009, T021–T022 | T012, T015, T019, T023, T025, T026–T029, T031–T033 |
| **US2** Add & Delete Rows (P2) | T001, T005, T010, T021–T022 | T013, T016, T023, T026–T029, T031–T033 |
| **US3** Year Range Config (P3) | T002, T005, T008, T010, T021–T022 | T003, T013, T016, T020, T023, T025, T027–T029, T031–T033 |
| **US4** Import from Web (P4) | T006, T011, T018 | T014, T017, T024, T027–T029, T030–T033 |

### Parallel Opportunities Within Phases

```bash
# Phase 1 — two files, fully parallel:
Task T001  Create PublicHoliday entity
Task T002  Create HolidayYearRange entity           (parallel with T001)
Task T003  HolidayYearRange tests                   (after T002)
Task T004  PublicHoliday tests                      (parallel with T003, after T001)

# Phase 2 — two interfaces, fully parallel:
Task T005  IHolidayRepository
Task T006  IHolidayImporter                         (parallel with T005)

# Phase 3 — five independent records, all parallel:
Task T007  HolidayEntryDto
Task T008  HolidayConfDto                           (parallel with T007)
Task T009  GetHolidayConfQuery                      (parallel with T007–T008)
Task T010  SaveHolidayConfCommand                   (parallel with T007–T009)
Task T011  ImportHolidaysFromWebCommand              (parallel with T007–T010)

# Phase 4 — three independent handlers (after Phase 2 + Phase 3 complete):
Task T012  GetHolidayConfQueryHandler
Task T013  SaveHolidayConfCommandHandler             (parallel with T012)
Task T014  ImportHolidaysFromWebCommandHandler       (parallel with T012–T013)

# Phase 5 — three independent test classes (after Phase 4):
Task T015  GetHolidayConfQueryHandlerTests
Task T016  SaveHolidayConfCommandHandlerTests        (parallel with T015)
Task T017  ImportHolidaysFromWebCommandHandlerTests  (parallel with T015–T016)

# Phase 6 — two EF configs in parallel, then DbContext, then migration:
Task T018  AngleSharp NuGet
Task T019  PublicHolidayConfiguration               (parallel with T018)
Task T020  HolidayYearRangeConfiguration            (parallel with T018–T019)
Task T021  AppDbContext DbSets                      (after T019 + T020)
Task T022  EF Migration                             (after T021)

# Phase 7 — two independent implementations (after Phase 6):
Task T023  HolidayRepository
Task T024  TimeAndDateHolidayScraper                (parallel with T023)

# Phase 11 — partially parallel DI wiring:
Task T030  InfrastructureServiceExtensions
Task T031  CompositionRoot                          (parallel with T030)
Task T032  SettingsViewModel                        (parallel with T030–T031)
Task T033  SettingsView.axaml                       (after T028 + T032)
Task T034  Strings.resx                             (parallel with T033)
```

---

## Parallel Execution Examples

### Fast-path MVP — US1 (View & Edit Holidays)

```
Step 1 (parallel): T001, T005, T007, T008, T009
Step 2 (parallel): T004, T006, T019, T021
Step 3:            T012
Step 4:            T022 (migration)
Step 5:            T023
Step 6:            T026
Step 7:            T027 (subset — load + display only)
Step 8 (parallel): T028, T029
Step 9 (parallel): T030, T031, T032, T034
Step 10:           T033
→ US1 independently testable at this point
```

### Full feature (sequential priority)

```
US1 MVP above → add T002, T003, T010, T013, T016, T020 → US3 complete
              → add T011, T014, T017, T018, T024 → US4 complete
              → US2 is covered by the same SaveCommand path (T010, T013) added for US3
→ T015, T025, T035 complete the coverage requirement
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (T001 — `PublicHoliday` only; T004 optional until later)
2. Complete Phase 2 (T005 — `IHolidayRepository` only)
3. Complete Phase 3 (T007, T008, T009)
4. Complete Phase 4 (T012 only)
5. Complete Phase 6 (T019, T021, T022 — skipping T020 HolidayYearRange config)
6. Complete Phase 7 (T023 — `HolidayRepository` only)
7. Complete Phase 9 (T026, T027 subset)
8. Complete Phase 10 (T028, T029)
9. Complete Phase 11 (T030, T031, T032, T033, T034)
10. **STOP and VALIDATE**: Settings → Holidays tab shows holidays, inline edit works, Save persists

### Incremental Delivery

1. US1 MVP above → validate independently
2. Add T002, T010, T013, T020 → US2 (Add/Delete) + US3 (Year Range) complete → validate
3. Add T006, T011, T014, T018, T024 → US4 (Import from Web) complete → validate
4. Add all test tasks (T003, T004, T015–T017, T025, T035) → coverage gates met → ready for merge

### Domain / Application Coverage Gates (from constitution)

- Domain: 100 % rule/state coverage — enforced by T003 + T004 (8 tests total)
- Application handlers: ≥ 90 % — enforced by T015 + T016 + T017 (≥ 9 test cases)
- Infrastructure: integration tests — enforced by T025 (4 round-trip tests)
- Desktop ViewModel: key command paths — enforced by T035 (6 test cases)

---

## Notes

- `[P]` tasks operate on different files and have no dependency on an immediately preceding incomplete task
- Layer badges replace `[USx]` labels; user-story traceability is in the Dependencies table above
- **No `.Result` / `.Wait()`** anywhere in handlers, ViewModel commands, or the scraper
- **No `[Reactive]` (Fody) or `[ObservableProperty]` (CommunityToolkit)** on ViewModels — use `this.RaiseAndSetIfChanged(ref _field, value)` exclusively
- **No hard-coded UI strings** — every user-visible string must use a `Strings.resx` key
- **`DateOnly` everywhere** — `DateTime` is prohibited for holiday dates throughout the feature
- **Import does NOT auto-save** — `ImportHolidaysFromWebCommandHandler` returns a list; the ViewModel populates `Entries`; the user must click Save to persist
- **Seeding fires exactly once** — `SaveHolidayConfCommandHandler` seeds only when `GetYearRangeAsync()` returns `null`; subsequent saves never re-seed
- **`HolidayYearRange` Id is always 1** — use `HolidayYearRange.SingletonId` constant throughout
- Commit after each phase or logical group; stop at any checkpoint to validate independently
- Run `dotnet build` (zero warnings) and `dotnet test` (all green) before merging the branch
