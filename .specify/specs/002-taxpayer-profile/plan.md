# Implementation Plan: Taxpayer Profile Management

**Branch**: `feature/002-taxpayer-profile` | **Date**: 2026-04-06 | **Spec**: `specs/002-taxpayer-profile/spec.md`  
**Input**: Feature specification from `.specify/specs/002-taxpayer-profile/spec.md`

---

## Summary

This feature enriches the `TaxpayerProfile` domain entity with two new optional fields (`PhoneNumber`, `Email`),
introduces full CQRS Application layer use cases (`SaveTaxpayerProfileCommand`, `GetTaxpayerProfileQuery`),
wires the Entity Framework Core persistence layer (EF entity configuration, migration `0002_TaxpayerProfile`,
`TaxpayerProfileRepository`), and replaces the placeholder `SettingsView` with a real Profile form backed by a
reactive `SettingsViewModel` (hosting a `ProfileSettingsViewModel` child). All I/O is asynchronous; the JMBG
uniqueness constraint is enforced at both Domain and DB levels (defense in depth).

---

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+ (FluentTheme), ReactiveUI, EF Core 8 (SQLite provider),
Microsoft.Extensions.DependencyInjection, xUnit + FluentAssertions + NSubstitute  
**Storage**: SQLite via EF Core 8; new `TaxpayerProfiles` table created by migration `0002_TaxpayerProfile`  
**Testing**: xUnit + FluentAssertions + NSubstitute; SQLite in-memory provider for Infrastructure integration tests  
**Target Platform**: Windows / macOS cross-platform desktop (Avalonia)  
**Project Type**: Desktop application  
**Performance Goals**: Save operation < 200 ms on idle local SQLite; first render of Settings → Profile < 100 ms  
**Constraints**: Single-user, single-process, offline-only; no outbound network calls; no `.Result`/`.Wait()`;
no hard-coded UI strings  
**Scale/Scope**: Singleton profile record; 1 table; 7 fields; ~20 tests total across 5 new test classes

---

## Constitution Check

_GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design._

- [x] **Clean Architecture boundary is preserved** (`Desktop -> Application -> Domain`; Infrastructure implements
  Application contracts only). `SettingsViewModel` / `ProfileSettingsViewModel` call Application handlers only; no
  repository or EF imports in Desktop; `ITaxpayerProfileRepository` defined in Application.
- [x] **All monetary/rate/percentage values are modeled as `decimal`**. `TaxpayerProfile` contains no monetary
  fields; `decimal` rule is not triggered.
- [x] **All business dates are modeled as `DateOnly`**. `TaxpayerProfile` contains no date fields; `DateOnly`
  rule is not triggered.
- [x] **Security/privacy constraints hold**: all data stored locally in SQLite; no IMAP credentials or secrets
  involved; raw JMBG values MUST NOT be written to application logs.
- [x] **External network usage is limited to approved endpoints or explicitly justified**. This feature makes
  zero outbound network calls (FR-014, CA-004). ✅ PASS.
- [x] **All I/O paths are async; UI avoids blocking calls and uses reactive async command flow**. Repository
  methods are `async Task` / `async Task<T>`; handlers are `async Task<Result<T>>`; ViewModel uses
  `ReactiveCommand.CreateFromTask`; UI updates via `RxApp.MainThreadScheduler`; `.Result`/`.Wait()` are
  prohibited.
- [x] **Tests and coverage impact are defined**. Domain: 100% rule/state coverage (9 test cases in
  `TaxpayerProfileTests.cs`). Application: ≥ 90% coverage (2 new handler test classes). Infrastructure: SQLite
  in-memory integration tests. Desktop: `SettingsViewModelTests.cs` covering field validation and command state.
- [x] **Feature work is mapped to an approved spec task**. Branch `feature/002-taxpayer-profile`; spec task
  `002` under `.specify/specs/002-taxpayer-profile/`.

**Result**: ✅ All 8 gates PASS. No violations requiring justification.

---

## Project Structure

### Documentation (this feature)

```text
.specify/specs/002-taxpayer-profile/
├── plan.md              ← This file (speckit.plan output)
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   ├── commands.md      ← SaveTaxpayerProfileCommand contract
│   ├── queries.md       ← GetTaxpayerProfileQuery contract
│   └── repository.md   ← ITaxpayerProfileRepository usage contract
└── tasks.md             ← Phase 2 output (speckit.tasks — NOT created by speckit.plan)
```

### Source Code (repository root)

```text
src/Rentier.Domain/Entities/TaxpayerProfile.cs
    MODIFIED — add PhoneNumber (string?), Email (string?); update constructor signature
               and guard clauses; add nullable parameters with no format validation.

src/Rentier.Application/Commands/SaveTaxpayerProfileCommand.cs
    NEW — sealed record SaveTaxpayerProfileCommand with Jmbg, FullName, Address,
          OpstinaCode, PhoneNumber?, Email? properties.

src/Rentier.Application/Handlers/SaveTaxpayerProfileCommandHandler.cs
    NEW — ICommandHandler<SaveTaxpayerProfileCommand, Result<Unit, Error>>;
          upsert via GetAsync → null → insert with new Guid / existing → update same Id.

src/Rentier.Application/Queries/GetTaxpayerProfileQuery.cs
    NEW — sealed record GetTaxpayerProfileQuery (no parameters).

src/Rentier.Application/Handlers/GetTaxpayerProfileQueryHandler.cs
    NEW — IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>;
          returns null DTO when no profile saved.

src/Rentier.Application/DTOs/TaxpayerProfileDto.cs
    NEW — sealed record with all 7 TaxpayerProfile attributes (Guid Id, string Jmbg,
          string FullName, string Address, string OpstinaCode, string? PhoneNumber,
          string? Email).

src/Rentier.Infrastructure/Persistence/AppDbContext.cs
    MODIFIED — add DbSet<TaxpayerProfile> TaxpayerProfiles; register
               TaxpayerProfileConfiguration in OnModelCreating.

src/Rentier.Infrastructure/Persistence/Configurations/TaxpayerProfileConfiguration.cs
    NEW — IEntityTypeConfiguration<TaxpayerProfile>; Fluent API: table name, PK,
          required columns (Jmbg, FullName, Address, OpstinaCode), nullable columns
          (PhoneNumber, Email), unique index on Jmbg.

src/Rentier.Infrastructure/Persistence/Migrations/0002_TaxpayerProfile.cs
    NEW — EF Core migration; creates TaxpayerProfiles table with UNIQUE constraint
          on Jmbg column.

src/Rentier.Infrastructure/Repositories/TaxpayerProfileRepository.cs
    NEW — implements ITaxpayerProfileRepository; GetAsync (FirstOrDefaultAsync),
          SaveAsync (upsert via EF tracking state), DeleteAsync (ExecuteDeleteAsync).

src/Rentier.Desktop/ViewModels/SettingsViewModel.cs
    REPLACED — hosts ProfileSettingsViewModel child; removes Placeholder property.

src/Rentier.Desktop/ViewModels/ProfileSettingsViewModel.cs
    NEW — ReactiveObject with reactive properties (Jmbg, FullName, Address,
          OpstinaCode, PhoneNumber, Email); inline validation observables; SaveCommand
          (ReactiveCommand.CreateFromTask); IsLoading, ErrorMessage, SuccessMessage.

src/Rentier.Desktop/Views/SettingsView.axaml
    REPLACED — TabControl with Profile tab hosting ProfileSettingsView; strings from
               Strings.resx only.

src/Rentier.Desktop/Views/SettingsView.axaml.cs
    REPLACED — ReactiveUserControl<SettingsViewModel> code-behind with WhenActivated.

src/Rentier.Desktop/Views/ProfileSettingsView.axaml
    NEW — ReactiveUserControl<ProfileSettingsViewModel>; Form with TextBox per field,
          inline error TextBlock per required field, Save button bound to SaveCommand.

src/Rentier.Desktop/Views/ProfileSettingsView.axaml.cs
    NEW — ReactiveUserControl<ProfileSettingsViewModel> code-behind.

src/Rentier.Desktop/Composition/CompositionRoot.cs
    MODIFIED — register ProfileSettingsViewModel (Transient);
               register ITaxpayerProfileRepository → TaxpayerProfileRepository (Scoped);
               register SaveTaxpayerProfileCommandHandler, GetTaxpayerProfileQueryHandler.

src/Rentier.Desktop/Resources/Strings.resx
    MODIFIED — add: Settings_Profile_TabHeader, Profile_Jmbg_Label, Profile_FullName_Label,
               Profile_Address_Label, Profile_OpstinaCode_Label, Profile_PhoneNumber_Label,
               Profile_Email_Label, Profile_Save_Button, Profile_Saved_Confirmation,
               Profile_JmbgValidation_Error, Profile_RequiredField_Error.

tests/Rentier.Domain.Tests/TaxpayerProfileTests.cs
    NEW — 9 test cases: Jmbg valid (13 digits), Jmbg short (12), Jmbg long (14),
          Jmbg non-numeric, Jmbg whitespace; FullName/Address/OpstinaCode null and
          whitespace; PhoneNumber/Email nullable (null and empty string both valid).

tests/Rentier.Application.Tests/SaveTaxpayerProfileCommandHandlerTests.cs
    NEW — insert path (null from repo), update path (existing profile), validation
          error path (invalid Jmbg); NSubstitute mocks for ITaxpayerProfileRepository.

tests/Rentier.Application.Tests/GetTaxpayerProfileQueryHandlerTests.cs
    NEW — existing profile returns DTO, null profile returns null DTO.

tests/Rentier.Infrastructure.Tests/TaxpayerProfileRepositoryTests.cs
    NEW — integration tests: insert then verify, update preserves Id, JMBG unique index
          violation throws, GetAsync returns null on empty db.

tests/Rentier.Desktop.Tests/SettingsViewModelTests.cs
    NEW — ProfileSettingsViewModel: JMBG inline validation disabled/enabled,
          SaveCommand canExecute transitions, successful save sets SuccessMessage,
          IsLoading true during save then false after.
```

---

## Complexity Tracking

> No Constitution Check violations requiring justification.

---

## Design Notes

### Upsert Strategy (FR-004)

`SaveTaxpayerProfileCommandHandler.HandleAsync`:
1. Calls `ITaxpayerProfileRepository.GetAsync()`.
2. If `null` → construct `new TaxpayerProfile(Guid.NewGuid(), ...)` → call `SaveAsync`.
3. If existing → construct `new TaxpayerProfile(existingProfile.Id, ...)` → call `SaveAsync`.
4. `TaxpayerProfileRepository.SaveAsync` uses EF `Entry(entity).State` to determine `Added` vs `Modified`.

### ViewModel Structure

```
SettingsViewModel (ReactiveObject)
└── ProfileSettingsViewModel ProfileTab { get; }   ← child VM
```

`SettingsView.axaml` hosts a `TabControl` with one `TabItem` ("Profile") whose `Content` is
`ProfileSettingsView` with `DataContext` bound to `SettingsViewModel.ProfileTab`.

### JMBG Uniqueness (Defense in Depth)

- **Domain layer**: `TaxpayerProfile` constructor throws `DomainException` if not 13 numeric digits.
- **ViewModel layer**: Inline validation observable disables Save button before dispatch.
- **Database layer**: Fluent API unique index on `Jmbg`; EF migration generates `UNIQUE` SQLite constraint.

### Singleton Profile Enforcement

Only one `TaxpayerProfile` row ever exists. `SaveAsync` upserts by `Id`; `GetAsync` calls
`FirstOrDefaultAsync()`. No database-level constraint enforces the singleton (a unique index on the
table would require a dummy constant column); instead the Application layer is authoritative through
the `ITaxpayerProfileRepository` contract.

### EF Core Entity Configuration

`TaxpayerProfileConfiguration : IEntityTypeConfiguration<TaxpayerProfile>` registered in
`AppDbContext.OnModelCreating(builder => builder.ApplyConfigurationsFromAssembly(...))`. Uses
`HasConversion` for private setters via a no-arg protected EF constructor (or owned type approach).

> **Note**: `TaxpayerProfile` currently has no parameterless constructor and all properties use
> `get`-only auto properties. A private `TaxpayerProfile()` constructor for EF materialization and
> property setters (or `HasField` / shadow properties) must be added as part of the Infrastructure
> changes. See `research.md` for the chosen pattern.
