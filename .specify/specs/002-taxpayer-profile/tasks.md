---
description: "Task list for Feature 002: Taxpayer Profile Management"
---

# Tasks: Taxpayer Profile Management (Feature 002)

**Input**: Design documents from `.specify/specs/002-taxpayer-profile/`
**Branch**: `feature/002-taxpayer-profile`
**Prerequisites**: spec.md ✅ plan.md ✅ data-model.md ✅ contracts/ ✅

**Tests**: TDD approach — test tasks are written first within each user-story phase and MUST be
run to confirm they FAIL (RED) before the corresponding implementation is authored.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no blocking dependency on an incomplete task)
- **[Story]**: User story label — `[US1]`, `[US2]`, `[US3]` — required in all Phase 3–5 tasks
- All tasks include exact file paths

---

## Phase 1: Setup — Shared Application Infrastructure

**Purpose**: Introduce the `Result<T, TError>` / `Error` / `VoidResult` common types that every
Application handler depends on. These two files have no dependencies inside this repository and
can be created before anything else in the solution.

- [X] T001 Create `Result<T, TError>` sealed class with `Ok(T value)` and `Fail(TError error)` static factory methods; include `VoidResult` sealed record with private constructor and `VoidResult.Value` singleton — in `src/Rentier.Application/Common/Result.cs`
- [X] T002 [P] Create `Error` sealed record with `Code` (string) and `Message` (string) primary-constructor parameters — in `src/Rentier.Application/Common/Error.cs`

**Checkpoint**: `Result<T, TError>`, `Error`, and `VoidResult` compile with zero warnings.

---

## Phase 2: Foundational — Domain Enrichment + Infrastructure Wiring

**Purpose**: Everything that MUST be complete before any user-story phase can begin.
Domain entity shape, Application record types, EF configuration, migration, repository, and
DbContext wiring are all blocking prerequisites.

> **⚠️ CRITICAL**: No user-story work (Phases 3–5) may start until this phase is complete.

> **Prerequisites**: Verify that `ICommandHandler<TCommand, TResult>` and
> `IQueryHandler<TQuery, TResult>` interfaces exist in
> `src/Rentier.Application/Interfaces/` (namespace: `Rentier.Application.Interfaces`). If absent from a prior feature, create them now
> before proceeding to the handler tasks in Phase 3.

**Batch A** (parallel — T003–T006; no inter-dependencies):

- [X] T003 [P] Enrich `TaxpayerProfile` entity: add `private TaxpayerProfile() { }` parameterless constructor for EF Core materialisation; change all property setters to `private set`; add `string? PhoneNumber { get; private set; }` and `string? Email { get; private set; }` optional properties; extend the public constructor with optional `string? phoneNumber = null` and `string? email = null` trailing parameters; no validation changes for optional fields — in `src/Rentier.Domain/Entities/TaxpayerProfile.cs`
- [X] T004 [P] Create `TaxpayerProfileDto` sealed record with positional parameters `(Guid Id, string Jmbg, string FullName, string Address, string OpstinaCode, string? PhoneNumber, string? Email)` — in `src/Rentier.Application/DTOs/TaxpayerProfileDto.cs`
- [X] T005 [P] Create `SaveTaxpayerProfileCommand` sealed record with positional parameters `(string Jmbg, string FullName, string Address, string OpstinaCode, string? PhoneNumber, string? Email)` — in `src/Rentier.Application/Commands/SaveTaxpayerProfileCommand.cs`
- [X] T006 [P] Create `GetTaxpayerProfileQuery` sealed record with no parameters: `public sealed record GetTaxpayerProfileQuery();` — in `src/Rentier.Application/Queries/GetTaxpayerProfileQuery.cs`

**After Batch A** (T007 depends on T003 — run after T003 entity fields are added):

- [X] T007 Create `TaxpayerProfileConfiguration : IEntityTypeConfiguration<TaxpayerProfile>` with Fluent API: table name `"TaxpayerProfiles"`, PK on `Id`, `Jmbg` required + `HasMaxLength(13)` + `HasIndex(x => x.Jmbg).IsUnique()`, `FullName` required + `HasMaxLength(200)`, `Address` required + `HasMaxLength(500)`, `OpstinaCode` required + `HasMaxLength(10)`, `PhoneNumber` optional + `HasMaxLength(50)`, `Email` optional + `HasMaxLength(254)` (depends on T003 — run after entity fields are added) — in `src/Rentier.Infrastructure/Persistence/Configurations/TaxpayerProfileConfiguration.cs`
- [X] T008 Modify `AppDbContext`: add `public DbSet<TaxpayerProfile> TaxpayerProfiles { get; set; }` property; register `TaxpayerProfileConfiguration` inside `OnModelCreating` via `builder.ApplyConfiguration(new TaxpayerProfileConfiguration())` or `ApplyConfigurationsFromAssembly` — in `src/Rentier.Infrastructure/Persistence/AppDbContext.cs`
- [X] T009 [P] Implement `TaxpayerProfileRepository : ITaxpayerProfileRepository`: constructor-inject `AppDbContext`; `GetAsync` → `FirstOrDefaultAsync` with `AsNoTracking()`; `SaveAsync` → `AnyAsync()` check with `AsNoTracking()`, then `context.Add(profile)` if no row or `context.Update(profile)` if row exists, then `SaveChangesAsync(ct)`; `DeleteAsync` → `ExecuteDeleteAsync(ct)` — in `src/Rentier.Infrastructure/Repositories/TaxpayerProfileRepository.cs`
- [X] T009b Create `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` with `public static class InfrastructureServiceExtensions` containing `public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, string dbPath)` that registers: `services.AddDbContext<AppDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"), ServiceLifetime.Transient)` (Transient not Scoped — Desktop uses a root ServiceProvider with no HTTP request scope; Scoped would throw InvalidOperationException at startup); `services.AddTransient<ITaxpayerProfileRepository, TaxpayerProfileRepository>()`; add `using Microsoft.EntityFrameworkCore;`, `using Rentier.Application.Repositories;`, `using Rentier.Infrastructure.Repositories;` — in `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`
- [X] T010 Generate EF Core migration `0002_TaxpayerProfile` by running `dotnet ef migrations add 0002_TaxpayerProfile --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop`; confirm the generated `Up()` method creates the `TaxpayerProfiles` table with the `UNIQUE` index on `Jmbg` — in `src/Rentier.Infrastructure/Persistence/Migrations/0002_TaxpayerProfile.cs`

**Checkpoint**: Solution builds with zero errors. `dotnet ef database update` succeeds against a
local SQLite file and creates the `TaxpayerProfiles` table. User-story phases can now begin.

---

## Phase 3: User Story 1 — First-Run Profile Setup (Priority: P1) 🎯 MVP

**Goal**: A user with an empty database opens Settings → Profile, fills in the required fields
(JMBG, FullName, Address, OpstinaCode) and optional fields, presses Save, restarts the
application, reopens Settings → Profile, and sees all saved values pre-populated.

**Independent Test**: Launch the app against a fresh (empty) SQLite database. Navigate to
Settings → Profile. Confirm all fields are blank. Enter a valid 13-digit JMBG, a full name, an
address, and an opstina code. Press Save. Confirm the success message appears. Restart the app.
Reopen Settings → Profile. Confirm every value is pre-populated exactly as entered.

### Tests for User Story 1 ⚠️

> **Write these tests FIRST. Run them and confirm RED (failing) before writing any implementation.**

- [X] T011 [P] [US1] Write `TaxpayerProfileTests.cs` with 9 domain test cases (method naming: `Constructor_StateUnderTest_ExpectedBehavior`): (1) valid 13-digit numeric JMBG constructs successfully; (2) null FullName throws `DomainException`; (3) whitespace-only FullName throws `DomainException`; (4) null Address throws `DomainException`; (5) whitespace-only Address throws `DomainException`; (6) null OpstinaCode throws `DomainException`; (7) whitespace-only OpstinaCode throws `DomainException`; (8) null PhoneNumber is accepted (no exception); (9) null Email and empty-string Email are both accepted — in `tests/Rentier.Domain.Tests/TaxpayerProfileTests.cs`
- [X] T012 [P] [US1] Write `GetTaxpayerProfileQueryHandlerTests.cs` with 2 test cases: (1) when repository `GetAsync` returns `null`, handler returns `Result.Ok` with a null DTO value; (2) when repository returns a `TaxpayerProfile` entity, handler returns `Result.Ok` with a fully-mapped `TaxpayerProfileDto` matching all 7 fields — mock `ITaxpayerProfileRepository` with NSubstitute — in `tests/Rentier.Application.Tests/GetTaxpayerProfileQueryHandlerTests.cs`
- [X] T013 [P] [US1] Write `SaveTaxpayerProfileCommandHandlerTests.cs` with 3 test cases: (1) repository `GetAsync` returns `null` (insert path) → handler calls `SaveAsync` with a newly generated `Guid` and returns `Result.Ok(VoidResult.Value)`; (2) invalid JMBG (non-13-digit) → `DomainException` caught → handler returns `Result.Fail(new Error("DOMAIN_VALIDATION", ...))` without calling `SaveAsync`; (3) whitespace-only `FullName` → `DomainException` caught → handler returns `Result.Fail` without calling `SaveAsync` — mock `ITaxpayerProfileRepository` with NSubstitute — in `tests/Rentier.Application.Tests/SaveTaxpayerProfileCommandHandlerTests.cs`
- [X] T014 [P] [US1] Write `TaxpayerProfileRepositoryTests.cs` with 2 integration test cases using the SQLite in-memory EF provider: (1) `GetAsync` on an empty database returns `null`; (2) `SaveAsync` with a new entity followed by `GetAsync` returns an entity with matching `Id`, `Jmbg`, `FullName`, `Address`, `OpstinaCode`, and null optional fields — in `tests/Rentier.Infrastructure.Tests/TaxpayerProfileRepositoryTests.cs`
- [X] T015 [P] [US1] Write `SettingsViewModelTests.cs` with 4 `ProfileSettingsViewModel` test cases: (1) on activation with a null query result, all string properties are empty string; (2) on activation with a populated query result, VM properties are populated with DTO values; (3) `SaveCommand.CanExecute` emits `false` when all fields are empty; (4) after a successful `SaveCommand` execution against a mocked `ICommandHandler`, `SuccessMessage` is non-null and `IsLoading` is `false` — in `tests/Rentier.Desktop.Tests/SettingsViewModelTests.cs`

### Implementation for User Story 1

- [X] T016 [US1] Implement `GetTaxpayerProfileQueryHandler : IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>`: add `using Rentier.Application.Interfaces;`; constructor-inject `ITaxpayerProfileRepository`; `HandleAsync` calls `repository.GetAsync(ct)`; if null returns `Result<TaxpayerProfileDto?, Error>.Ok(null)`; otherwise maps all 7 fields to `new TaxpayerProfileDto(...)` and returns `Result.Ok(dto)` — in `src/Rentier.Application/Handlers/GetTaxpayerProfileQueryHandler.cs`
- [X] T017 [US1] Implement `SaveTaxpayerProfileCommandHandler : ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>`: add `using Rentier.Application.Interfaces;`; constructor-inject `ITaxpayerProfileRepository`; `HandleAsync` wraps `new TaxpayerProfile(Guid.NewGuid(), ...)` in a try/catch for `DomainException` → return `Result.Fail(new Error("DOMAIN_VALIDATION", ex.Message))`; then calls `repository.GetAsync(ct)`; if null keep new `Guid`; if existing reconstruct with `existing.Id`; call `repository.SaveAsync(entity, ct)` and return `Result.Ok(VoidResult.Value)` — in `src/Rentier.Application/Handlers/SaveTaxpayerProfileCommandHandler.cs`
- [X] T018 [US1] Implement `ProfileSettingsViewModel : ReactiveObject`: use `RaiseAndSetIfChanged` for all reactive properties (no `[Reactive]` attribute / ReactiveUI.Fody); define each string property with a private backing field, e.g. `private string _jmbg = string.Empty; public string Jmbg { get => _jmbg; set => this.RaiseAndSetIfChanged(ref _jmbg, value); }` — apply the same pattern for `FullName`, `Address`, `OpstinaCode`, `PhoneNumber`, `Email` (all `string.Empty`); `private bool _isLoading; public bool IsLoading { get => _isLoading; set => this.RaiseAndSetIfChanged(ref _isLoading, value); }`; `private string? _successMessage; public string? SuccessMessage { get => _successMessage; set => this.RaiseAndSetIfChanged(ref _successMessage, value); }`; `private string? _errorMessage; public string? ErrorMessage { get => _errorMessage; set => this.RaiseAndSetIfChanged(ref _errorMessage, value); }`; `canSave` observable via `this.WhenAnyValue(...)` checking `Jmbg.Length == 13 && Jmbg.All(char.IsDigit) && !string.IsNullOrWhiteSpace(FullName) && !string.IsNullOrWhiteSpace(Address) && !string.IsNullOrWhiteSpace(OpstinaCode)`; `SaveCommand = ReactiveCommand.CreateFromTask(ExecuteSaveAsync, canSave)`; `WhenActivated` calls query handler and populates fields on main-thread scheduler — in `src/Rentier.Desktop/ViewModels/ProfileSettingsViewModel.cs`
- [X] T019 [US1] Create `ProfileSettingsView.axaml` as `ReactiveUserControl<ProfileSettingsViewModel>` with a `StackPanel` form containing: one labeled `TextBox` per field (Jmbg, FullName, Address, OpstinaCode, PhoneNumber, Email), label text bound to string resource keys (stubs acceptable here; finalised in T034); a Save `Button` bound to `SaveCommand`; a `TextBlock` bound to `SuccessMessage` (hidden when null); a `TextBlock` bound to `ErrorMessage` (hidden when null); `IsEnabled` on form bound to `!IsLoading`; create `ProfileSettingsView.axaml.cs` as `ReactiveUserControl<ProfileSettingsViewModel>` code-behind with `WhenActivated` — in `src/Rentier.Desktop/Views/ProfileSettingsView.axaml` and `src/Rentier.Desktop/Views/ProfileSettingsView.axaml.cs`
- [X] T020 [US1] Replace `SettingsViewModel`: remove the existing placeholder property; add `public ProfileSettingsViewModel ProfileTab { get; }` child VM property; inject `ProfileSettingsViewModel` via constructor; keep `ReactiveObject` base — in `src/Rentier.Desktop/ViewModels/SettingsViewModel.cs`
- [X] T021 [US1] Replace `SettingsView.axaml` with a `ReactiveUserControl<SettingsViewModel>` containing a `TabControl`; add one `TabItem` with `Header` bound to `Settings_Profile_TabHeader` resource key (stub acceptable here; finalised in T034) and `Content` set to a `ProfileSettingsView` instance with `DataContext` bound to `{Binding ProfileTab}`; update `SettingsView.axaml.cs` code-behind to `ReactiveUserControl<SettingsViewModel>` with `WhenActivated` — in `src/Rentier.Desktop/Views/SettingsView.axaml` and `src/Rentier.Desktop/Views/SettingsView.axaml.cs`
- [X] T022 [US1] Update DI wiring in `App.axaml.cs` and `CompositionRoot.cs`: (1) in `CompositionRoot.cs`, add `services.AddTransient<ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>, SaveTaxpayerProfileCommandHandler>()`; `services.AddTransient<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>, GetTaxpayerProfileQueryHandler>()`; `services.AddTransient<ProfileSettingsViewModel>()` — do NOT add `AddScoped<ITaxpayerProfileRepository>` here (registration moves to `InfrastructureServiceExtensions` from T009b); (2) in `App.axaml.cs`, make `OnFrameworkInitializationCompleted` override `async` (`public override async void OnFrameworkInitializationCompleted()`); resolve `dbPath` as `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rentier", "rentier.db")`; call `services.AddInfrastructureServices(dbPath)` BEFORE `services.AddDesktopServices()`; after `BuildServiceProvider()` call `await provider.GetRequiredService<AppDbContext>().Database.MigrateAsync()` — in `src/Rentier.Desktop/Composition/CompositionRoot.cs` and `src/Rentier.Desktop/App.axaml.cs`

**Checkpoint**: Launch the app against a fresh database. Navigate to Settings → Profile. Form is
blank. Fill in all required fields. Press Save. Success message appears. Restart. Reopen
Settings → Profile. All saved values are pre-populated. US1 is independently verified. ✅

---

## Phase 4: User Story 2 — Edit Existing Profile (Priority: P2)

**Goal**: A user who has previously saved a profile opens Settings → Profile, sees the form
pre-populated, modifies one or more fields, presses Save, and the existing record is updated
in-place. No duplicate row is created.

**Independent Test**: Save a profile, reopen Settings → Profile, change the Address field, press
Save. Reopen the view and confirm the updated address is shown. Verify that only one row exists
in the `TaxpayerProfiles` table (same `Id` as before).

### Tests for User Story 2 ⚠️

> **Write these tests FIRST. Run them and confirm RED (failing) before writing any implementation.**

- [X] T023 [P] [US2] Extend `SaveTaxpayerProfileCommandHandlerTests.cs` with 1 update-path test: when repository `GetAsync` returns an existing profile with `existingId`, the handler calls `SaveAsync` with a `TaxpayerProfile` whose `Id` equals `existingId` (not a new Guid) and returns `Result.Ok(VoidResult.Value)` — verify via NSubstitute argument capture — in `tests/Rentier.Application.Tests/SaveTaxpayerProfileCommandHandlerTests.cs`
- [X] T024 [P] [US2] Extend `TaxpayerProfileRepositoryTests.cs` with 3 update-path integration tests: (1) `SaveAsync` called twice with the same `Id` results in exactly one row in the table; (2) after update, `GetAsync` returns the updated `FullName` and `Address` values; (3) `Id` is identical before and after the update call — using SQLite in-memory provider — in `tests/Rentier.Infrastructure.Tests/TaxpayerProfileRepositoryTests.cs`
- [X] T025 [P] [US2] Extend `SettingsViewModelTests.cs` with 3 edit-flow tests: (1) clearing `FullName` to whitespace causes `SaveCommand.CanExecute` to emit `false`; (2) clearing `PhoneNumber` and `Email` and executing `SaveCommand` results in the command handler being called with `null` optional fields; (3) when the command handler returns `Result.Fail`, `ErrorMessage` is set and `SuccessMessage` remains null — in `tests/Rentier.Desktop.Tests/SettingsViewModelTests.cs`

### Implementation for User Story 2

- [X] T026 [US2] Review `SaveTaxpayerProfileCommandHandler.HandleAsync` update path: confirm the entity is reconstructed with `existing.Id` when `GetAsync` returns a non-null profile; if the logic is already correct from T017 no code change is needed; add or correct the `existing.Id` assignment if missing — in `src/Rentier.Application/Handlers/SaveTaxpayerProfileCommandHandler.cs`
- [X] T027 [US2] Review `TaxpayerProfileRepository.SaveAsync` upsert logic: confirm `AnyAsync()` is used (not `GetAsync`) for the existence check to avoid double-read tracking conflicts; confirm `context.Update(profile)` is called when a row exists; if logic is correct from T009 no change needed; fix `Add`/`Update` branch if incorrect — in `src/Rentier.Infrastructure/Repositories/TaxpayerProfileRepository.cs`
- [X] T028 [US2] Add `FullNameError`, `AddressError`, and `OpstinaCodeError` observable string? properties to `ProfileSettingsViewModel`: each driven by `this.WhenAnyValue(x => x.FieldName)` → `null` when non-whitespace, `"This field is required"` when null or whitespace; incorporate each error observable into `canSave` so any non-null error disables `SaveCommand` — in `src/Rentier.Desktop/ViewModels/ProfileSettingsViewModel.cs`
- [X] T029 [US2] Add inline required-field error `TextBlock` elements to `ProfileSettingsView.axaml`: one below each of FullName, Address, and OpstinaCode `TextBox` controls; bind `IsVisible` to the corresponding `*Error` property being non-null; bind `Text` to the corresponding `*Error` property; resource key `Profile_RequiredField_Error` stub acceptable here (finalised in T034) — in `src/Rentier.Desktop/Views/ProfileSettingsView.axaml`

**Checkpoint**: Edit the saved profile (change Address), press Save, reopen the view, confirm
updated values. Confirm only one row in the database. US2 is independently verified. ✅

---

## Phase 5: User Story 3 — JMBG Validation Feedback (Priority: P2)

**Goal**: When the JMBG field contains a value that is not exactly 13 numeric digits, the Save
button is disabled and an inline error message appears next to the JMBG field — before any data
is dispatched to the Application layer.

**Independent Test**: Type a 12-digit string into the JMBG field; verify the Save button is
disabled and an inline error reads "JMBG must be exactly 13 digits". Type a 13-digit numeric
string (all other required fields valid); verify the Save button becomes enabled and the error
disappears.

### Tests for User Story 3 ⚠️

> **Write these tests FIRST. Run them and confirm RED (failing) before writing any implementation.**

- [X] T030 [P] [US3] Extend `TaxpayerProfileTests.cs` with 5 JMBG boundary test cases: (1) 12-digit numeric string throws `DomainException`; (2) 14-digit numeric string throws `DomainException`; (3) exactly 13 characters containing letters (e.g., `"1234567890ABC"`) throws `DomainException`; (4) exactly 13 space characters throws `DomainException`; (5) `null` JMBG throws `DomainException` — in `tests/Rentier.Domain.Tests/TaxpayerProfileTests.cs`
- [X] T031 [P] [US3] Extend `SettingsViewModelTests.cs` with 3 JMBG ViewModel tests: (1) setting `Jmbg` to a 12-digit string causes `JmbgError` to be non-null and `SaveCommand.CanExecute` to be `false`; (2) setting `Jmbg` to 13 non-numeric characters causes `JmbgError` to be non-null; (3) setting `Jmbg` to exactly 13 numeric digits (all other required fields valid and non-whitespace) causes `JmbgError` to be `null` and `SaveCommand.CanExecute` to be `true` — in `tests/Rentier.Desktop.Tests/SettingsViewModelTests.cs`

### Implementation for User Story 3

- [X] T032 [US3] Add `JmbgError` observable string? property to `ProfileSettingsViewModel`: driven by `this.WhenAnyValue(x => x.Jmbg)` → `null` when `value?.Length == 13 && value.All(char.IsDigit)`, otherwise `"JMBG must be exactly 13 digits"`; update `canSave` observable to also require `JmbgError == null` (replacing or extending the existing length+digit check inline) — in `src/Rentier.Desktop/ViewModels/ProfileSettingsViewModel.cs`
- [X] T033 [US3] Add JMBG inline error `TextBlock` to `ProfileSettingsView.axaml`: positioned directly below the JMBG `TextBox`; bind `IsVisible` to `JmbgError` being non-null; bind `Text` to `JmbgError`; resource key `Profile_JmbgValidation_Error` stub acceptable here (finalised in T034) — in `src/Rentier.Desktop/Views/ProfileSettingsView.axaml`

**Checkpoint**: Type an invalid JMBG. Confirm Save is disabled and inline error is visible.
Type a valid 13-digit JMBG with all other required fields filled. Confirm Save is enabled and
error disappears. US3 is independently verified. ✅

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Replace all string stubs with proper resource keys, verify coverage gates, and
confirm CI-clean build.

> **⚠️ IMPORTANT**: T034 (Strings.resx) must be completed before the view AXAML files are
> committed to a PR. If stubs were used during Phases 3–5, replace them now. No hard-coded
> user-visible strings are permitted in view markup or code-behind (FR-016).

- [X] T034 [P] Add all 11 string resource keys to `Strings.resx`: `Settings_Profile_TabHeader` = `"Profile"`, `Profile_Jmbg_Label` = `"JMBG"`, `Profile_FullName_Label` = `"Full Name"`, `Profile_Address_Label` = `"Address"`, `Profile_OpstinaCode_Label` = `"Opstina Code"`, `Profile_PhoneNumber_Label` = `"Phone Number"`, `Profile_Email_Label` = `"Email"`, `Profile_Save_Button` = `"Save Profile"`, `Profile_Saved_Confirmation` = `"Profile saved successfully."`, `Profile_JmbgValidation_Error` = `"JMBG must be exactly 13 digits"`, `Profile_RequiredField_Error` = `"This field is required"`; update all view markup stubs to reference these keys via `{x:Static resx:Strings.KeyName}` — in `src/Rentier.Desktop/Resources/Strings.resx`
- [X] T035 [P] Build all projects and confirm zero compiler warnings: run `dotnet build --no-incremental` from the repository root; fix any warnings before merge — across all four projects
- [X] T036 Run `dotnet test tests/Rentier.Domain.Tests` with coverage collection and confirm 100% rule/state coverage for `TaxpayerProfile` constructor paths (all 14 test cases from T011 + T030 green)
- [X] T037 [P] Run `dotnet test tests/Rentier.Application.Tests` with coverage collection and confirm ≥ 90% branch coverage for `SaveTaxpayerProfileCommandHandler` and `GetTaxpayerProfileQueryHandler`
- [X] T038 [P] Run the full test suite (`dotnet test`) across all four test projects and confirm all tests are green; note any flaky infrastructure tests and stabilise before merge
- [X] T039 Audit `SaveTaxpayerProfileCommandHandler.cs` and `GetTaxpayerProfileQueryHandler.cs` to confirm no log statement writes the raw `Jmbg` value; search for `Jmbg` and `jmbg` references inside any `ILogger` / `Log.` / `Debug.` call sites — in `src/Rentier.Application/Handlers/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1 (Result type must exist). Blocks Phases 3–5.
- **Phase 3 (US1)**: Depends on Phase 2 complete. No dependency on US2 or US3.
- **Phase 4 (US2)**: Depends on Phase 2 complete. Builds on US1 implementation files but tests
  can be written in parallel with Phase 3 if the US1 implementation is complete.
- **Phase 5 (US3)**: Depends on Phase 2 complete. Extends files created in Phase 3.
- **Phase 6 (Polish)**: Depends on Phases 3–5 complete.

### User Story Dependencies

- **US1 (P1)**: Independent after Phase 2. Creates the core files that US2 and US3 extend.
- **US2 (P2)**: Extends `ProfileSettingsViewModel.cs`, `ProfileSettingsView.axaml`, and test files from US1. Should begin after US1 implementation is merged to avoid conflicts.
- **US3 (P2)**: Extends `ProfileSettingsViewModel.cs`, `ProfileSettingsView.axaml`, and test files from US1. Can be worked in parallel with US2 by a second developer if file-conflict risk is managed.

### Within Each User Story

1. Test tasks (T0nn [P] [USn]) MUST be written and confirmed RED before implementation begins.
2. Within tests: domain tests → handler tests → repository tests → VM tests (all parallel).
3. Within implementation: handlers → ViewModel → Views → DI registration.
4. Story complete and independently verified before moving to the next priority.

### Key Cross-Task Dependencies

| Task | Depends On |
|------|-----------|
| T007 (`TaxpayerProfileConfiguration`) | T003 (entity shape finalised) |
| T008 (`AppDbContext`) | T003, T007 |
| T009 (`TaxpayerProfileRepository`) | T003, T008 |
| T010 (EF migration) | T007, T008 |
| T016 (`GetTaxpayerProfileQueryHandler`) | T001, T004, T006 |
| T017 (`SaveTaxpayerProfileCommandHandler`) | T001, T003, T005 |
| T018 (`ProfileSettingsViewModel`) | T016, T017 (handler interfaces) |
| T019 (`ProfileSettingsView`) | T018 |
| T020 (`SettingsViewModel`) | T018 |
| T021 (`SettingsView`) | T019, T020 |
| T022 (`CompositionRoot`) | T009, T016, T017, T018 |
| T028 (required-field error observables) | T018 |
| T029 (required-field inline TextBlocks) | T028 |
| T032 (`JmbgError` observable) | T018 |
| T033 (JMBG inline TextBlock) | T032 |
| T034 (`Strings.resx`) | T019, T021, T029, T033 (must replace stubs) |

---

## Parallel Execution Examples

### Phase 2 — Batch A (all five can start simultaneously)

```
Task T003: Enrich TaxpayerProfile.cs
Task T004: Create TaxpayerProfileDto.cs
Task T005: Create SaveTaxpayerProfileCommand.cs
Task T006: Create GetTaxpayerProfileQuery.cs
Task T007: Create TaxpayerProfileConfiguration.cs
```

### Phase 2 — Batch B (after Batch A completes)

```
Task T008: Modify AppDbContext.cs (sequential)
```

### Phase 2 — Batch C (after Batch B)

```
Task T009: Implement TaxpayerProfileRepository.cs
Task T010: Generate EF migration
```

### Phase 3 — US1 Tests (all five can start simultaneously)

```
Task T011: Write TaxpayerProfileTests.cs
Task T012: Write GetTaxpayerProfileQueryHandlerTests.cs
Task T013: Write SaveTaxpayerProfileCommandHandlerTests.cs
Task T014: Write TaxpayerProfileRepositoryTests.cs
Task T015: Write SettingsViewModelTests.cs
```

### Phase 3 — US1 Implementation

```
Step 1 (parallel): T016 GetQueryHandler, T017 SaveCommandHandler
Step 2 (sequential): T018 ProfileSettingsViewModel (depends on Step 1)
Step 3 (parallel): T019 ProfileSettingsView, T020 SettingsViewModel (depends on T018)
Step 4 (sequential): T021 SettingsView (depends on T019, T020)
Step 5 (sequential): T022 CompositionRoot (depends on all above)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Foundational (T003–T010) — **CRITICAL blocker**
3. Complete Phase 3: User Story 1 (T011–T022)
4. **STOP and VALIDATE**: Launch app, fill form, save, restart, verify persistence
5. Demo or merge as MVP ← full end-to-end flow working

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 → Test independently → **MVP demo** (profile save/load working)
3. US2 → Test independently → Edit flow working
4. US3 → Test independently → Inline JMBG validation working
5. Polish → CI green, coverage gates met → **Merge-ready**

### Parallel Team Strategy (if 2+ developers)

1. Both developers complete Phase 1 + Phase 2 together (fast, ~1–2 hours)
2. Developer A: Phase 3 (US1 — core save/load flow)
3. Developer B: Phase 3 tests can run concurrently; picks up Phase 4 (US2) once US1
   implementation is committed (avoid AXAML file conflicts)
4. Developer A or B: Phase 5 (US3 — JMBG validation layer on top of US1 VM)
5. Both: Phase 6 (Polish + CI verification)

---

## Notes

- `[P]` tasks touch different files and have no dependency on an incomplete task in the same phase.
- `[USn]` label maps every task to a specific user story for traceability to spec.md.
- JMBG raw values MUST NOT appear in any log output — enforced by T039 audit.
- No `.Result` or `.Wait()` anywhere in the call stack — all I/O paths are `async/await`.
- `SettingsView.axaml` and `ProfileSettingsView.axaml` must reference only `Strings.resx` keys; no hard-coded user-visible text is permitted.
- `DeleteAsync` is implemented in `TaxpayerProfileRepository` (T009) but is NOT wired to any UI element in this feature; it is reserved for a future maintenance feature.
- The `AppDbContext` `OnModelCreating` change (T008) triggers the EF migration (T010); both must be complete before the app can start without a migration error.
- Test naming convention: `MethodName_StateUnderTest_ExpectedBehavior` per constitution §Testing Requirements.

