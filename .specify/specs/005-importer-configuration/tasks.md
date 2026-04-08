---
description: "Task list for Feature 005: Importer Configuration"
---

# Tasks: Importer Configuration (Feature 005)

**Input**: Design documents from `.specify/specs/005-importer-configuration/`  
**Branch**: `feature/005-importer-configuration`  
**Prerequisites**: spec.md ✅ plan.md ✅ data-model.md ✅ contracts/ ✅

**Tests**: Tests are included for all Domain, Application, Infrastructure, and Desktop layers
in line with constitution quality gates (Domain: 100% rule/state coverage; Application: ≥ 90%;
Infrastructure: EF SQLite in-memory integration; Desktop: ViewModel behaviour).

**Organization**: Tasks are grouped by layer first (Domain → Application → Infrastructure →
Desktop) and by user story within the Desktop phases. The foundational CQRS layer is shared
across all four user stories (list, add, edit, delete), so it is completed in a single
foundational phase before any Desktop story work begins.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no blocking dependency on an incomplete task)
- **[Story]**: User story label — `[US1]`, `[US2]`, `[US3]`, `[US4]` — required in all
  Desktop phase tasks
- All tasks include exact file paths

---

## Pre-Flight: Critical Existing State

> ⚠️ Before starting, confirm the following files **already exist** and must **NOT** be recreated:
>
> - `src/Rentier.Application/Repositories/IImporterRepository.cs` — namespace `Rentier.Application.Repositories`;
>   interface with `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`.
>   **DO NOT CREATE — use as-is.**
> - `src/Rentier.Domain/Entities/Importer.cs` — EXISTS with OLD fields `(Id, DisplayName, FilterExpression)`.
>   **MODIFY in T002 — complete redesign required.**
> - `src/Rentier.Desktop/ViewModels/SettingsViewModel.cs` — has `ProfileTab`, `HolidayTab`, `MailboxesTab`.
>   **MODIFY in T029 — add `ImportersTab` as 4th parameter.**
> - `src/Rentier.Desktop/Views/SettingsView.axaml` — has 3 tabs.
>   **MODIFY in T030 — add 4th Importers tab.**
>
> ⚠️ Do **NOT** use `ExecuteDeleteAsync` anywhere in repository implementation — the EF Core
> SQLite in-memory provider used in infrastructure tests does not support it.
> Use `FindAsync` + `Remove` + `SaveChangesAsync` instead.

---

## Phase 1: Domain — Entity Redesign

**Purpose**: Introduce the `ReportType` enum and completely redesign the `Importer` entity.
All subsequent phases depend on the redesigned entity compiling cleanly.

> **⚠️ CRITICAL**: No Application, Infrastructure, or Desktop work may begin until T002 compiles.

- [X] T001 [DOMAIN] CREATE `src/Rentier.Domain/Enums/ReportType.cs`:
  `namespace Rentier.Domain.Enums;`
  XML doc comment: `/// <summary>Report type supported by an importer configuration.</summary>`
  `public enum ReportType { IbkrCsv = 0 }`
  (Creates the `Enums/` folder if it does not already exist.)

- [X] T002 [DOMAIN] MODIFY `src/Rentier.Domain/Entities/Importer.cs` — complete redesign:
  Add `using Rentier.Domain.Enums;` and `using Rentier.Domain.Exceptions;`.
  Remove the existing `FilterExpression` property and the existing public constructor.
  Add `private Importer() { }` (EF Core parameterless materialisation constructor).
  Replace all properties with: `public Guid Id { get; private set; }`,
  `public string DisplayName { get; private set; } = string.Empty`,
  `public ReportType ReportType { get; private set; }`,
  `public Guid? TaxpayerProfileId { get; private set; }`,
  `public Guid? MailboxId { get; private set; }`,
  `public string FromFilter { get; private set; } = string.Empty`,
  `public string SubjectFilter { get; private set; } = string.Empty`,
  `public string AttachmentRegex { get; private set; } = string.Empty`,
  `public string PaymentNotes { get; private set; } = string.Empty`.
  Add factory: `public static Importer Create(string displayName, ReportType reportType = ReportType.IbkrCsv)` —
  throws `DomainException` for empty/whitespace DisplayName and for DisplayName > 200 chars;
  returns `new Importer { Id = Guid.NewGuid(), DisplayName = displayName.Trim(), ReportType = reportType }`.
  Add mutation: `public void UpdateDetails(string displayName, ReportType reportType, Guid? taxpayerProfileId, Guid? mailboxId, string fromFilter, string subjectFilter, string attachmentRegex, string paymentNotes)` —
  re-validates DisplayName (empty → DomainException; > 200 chars → DomainException);
  validates PaymentNotes length ≤ 4000; assigns all properties (null filter fields → `string.Empty`).

**Checkpoint**: `dotnet build src/Rentier.Domain` produces zero errors.

---

## Phase 2: Domain Tests

**Purpose**: Verify domain logic in isolation before any Application or Infrastructure code
depends on the entity behaviour.

- [X] T003 [TEST] CREATE `tests/Rentier.Domain.Tests/ImporterTests.cs`:
  xUnit test class using FluentAssertions; no mocks required.
  `Create_ValidDisplayName_ReturnsImporter` — assert Id is non-empty Guid, DisplayName matches input.
  `Create_EmptyDisplayName_ThrowsDomainException` — empty string throws `DomainException`.
  `Create_WhitespaceDisplayName_ThrowsDomainException` — whitespace-only string throws `DomainException`.
  `Create_DisplayNameTooLong_ThrowsDomainException` — 201-char string throws `DomainException`.
  `Create_DisplayName200Chars_Succeeds` — boundary pass: exactly 200 chars succeeds.
  `Create_SetsDefaultReportType` — assert `ReportType == ReportType.IbkrCsv` when not specified.
  `Create_GeneratesUniqueId` — two `Create` calls produce different Guids.
  `UpdateDetails_ValidInputs_UpdatesAllFields` — call UpdateDetails with non-null values; assert
  all nine properties updated correctly.
  `UpdateDetails_EmptyDisplayName_ThrowsDomainException` — assert DomainException.
  `UpdateDetails_NullForeignKeys_AcceptsNulls` — `TaxpayerProfileId = null`, `MailboxId = null` succeeds.
  `UpdateDetails_NullFilters_StoresEmptyString` — null filter strings stored as `string.Empty`.
  `UpdateDetails_PaymentNotesExceeds4000_ThrowsDomainException` — 4001-char string throws DomainException.

**Checkpoint**: `dotnet test tests/Rentier.Domain.Tests` — all tests pass.

---

## Phase 3: Application — DTOs, Commands, Queries (Parallel Batch)

**Purpose**: Define the CQRS records that form the Application contract. These have no
mutual dependencies and can all be written in parallel.

> All tasks in this phase can run in parallel (T004–T008).

- [X] T004 [P] [APPLICATION] CREATE `src/Rentier.Application/DTOs/ImporterDto.cs`:
  `namespace Rentier.Application.DTOs;`
  `using Rentier.Domain.Enums;`
  `public sealed record ImporterDto(Guid Id, string DisplayName, ReportType ReportType, Guid? TaxpayerProfileId, Guid? MailboxId, string FromFilter, string SubjectFilter, string AttachmentRegex, string PaymentNotes);`

- [X] T005 [P] [APPLICATION] CREATE `src/Rentier.Application/Queries/GetImportersQuery.cs`:
  `namespace Rentier.Application.Queries;`
  `public sealed record GetImportersQuery();`
  Returns `Result<IReadOnlyList<ImporterDto>, Error>`.

- [X] T006 [P] [APPLICATION] CREATE `src/Rentier.Application/Commands/AddImporterCommand.cs`:
  `namespace Rentier.Application.Commands;`
  `using Rentier.Domain.Enums;`
  `public sealed record AddImporterCommand(string DisplayName, ReportType ReportType, Guid? TaxpayerProfileId, Guid? MailboxId, string FromFilter, string SubjectFilter, string AttachmentRegex, string PaymentNotes);`
  Returns `Result<Guid, Error>`.

- [X] T007 [P] [APPLICATION] CREATE `src/Rentier.Application/Commands/UpdateImporterCommand.cs`:
  `namespace Rentier.Application.Commands;`
  `using Rentier.Domain.Enums;`
  `public sealed record UpdateImporterCommand(Guid Id, string DisplayName, ReportType ReportType, Guid? TaxpayerProfileId, Guid? MailboxId, string FromFilter, string SubjectFilter, string AttachmentRegex, string PaymentNotes);`
  Returns `Result<VoidResult, Error>`.

- [X] T008 [P] [APPLICATION] CREATE `src/Rentier.Application/Commands/DeleteImporterCommand.cs`:
  `namespace Rentier.Application.Commands;`
  `public sealed record DeleteImporterCommand(Guid Id);`
  Returns `Result<VoidResult, Error>`.

---

## Phase 4: Application — Handlers

**Purpose**: Implement the four CQRS handlers. Depends on Phase 3 records compiling.
`GetImportersQueryHandler` has no sibling dependencies; the three command handlers
can be written in parallel with each other.

- [X] T009 [APPLICATION] CREATE `src/Rentier.Application/Handlers/GetImportersQueryHandler.cs`:
  `namespace Rentier.Application.Handlers;`
  Implements `IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>`.
  Constructor-inject `IImporterRepository _repository` (namespace `Rentier.Application.Repositories`).
  `HandleAsync`: `var importers = await _repository.GetAllAsync(ct)`;
  project each to `new ImporterDto(i.Id, i.DisplayName, i.ReportType, i.TaxpayerProfileId, i.MailboxId, i.FromFilter, i.SubjectFilter, i.AttachmentRegex, i.PaymentNotes)`;
  return `Result<IReadOnlyList<ImporterDto>, Error>.Success(list.AsReadOnly())`.

- [X] T010 [P] [APPLICATION] CREATE `src/Rentier.Application/Handlers/AddImporterCommandHandler.cs`:
  `namespace Rentier.Application.Handlers;`
  Implements `ICommandHandler<AddImporterCommand, Result<Guid, Error>>`.
  Constructor-inject `IImporterRepository _repository`.
  `HandleAsync`:
  (1) If `AttachmentRegex` is non-empty, validate: `try { _ = new System.Text.RegularExpressions.Regex(command.AttachmentRegex); } catch (ArgumentException ex) { return Result<Guid, Error>.Failure(new Error("INVALID_REGEX", ex.Message)); }`.
  (2) `try { var importer = Importer.Create(command.DisplayName, command.ReportType); importer.UpdateDetails(command.DisplayName, command.ReportType, command.TaxpayerProfileId, command.MailboxId, command.FromFilter, command.SubjectFilter, command.AttachmentRegex, command.PaymentNotes); await _repository.AddAsync(importer, ct); return Result<Guid, Error>.Success(importer.Id); } catch (DomainException ex) { return Result<Guid, Error>.Failure(new Error("DOMAIN_ERROR", ex.Message)); }`.

- [X] T011 [P] [APPLICATION] CREATE `src/Rentier.Application/Handlers/UpdateImporterCommandHandler.cs`:
  `namespace Rentier.Application.Handlers;`
  Implements `ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>`.
  Constructor-inject `IImporterRepository _repository`.
  `HandleAsync`:
  (1) `var importer = await _repository.GetByIdAsync(command.Id, ct); if (importer is null) return Result<VoidResult, Error>.Failure(new Error("IMPORTER_NOT_FOUND", $"Importer {command.Id} not found."))`.
  (2) If `AttachmentRegex` is non-empty, validate regex (same pattern as T010).
  (3) `try { importer.UpdateDetails(...); } catch (DomainException ex) { return Result<VoidResult, Error>.Failure(new Error("DOMAIN_ERROR", ex.Message)); }`.
  (4) `await _repository.UpdateAsync(importer, ct); return Result<VoidResult, Error>.Success(VoidResult.Value)`.

- [X] T012 [P] [APPLICATION] CREATE `src/Rentier.Application/Handlers/DeleteImporterCommandHandler.cs`:
  `namespace Rentier.Application.Handlers;`
  Implements `ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>`.
  Constructor-inject `IImporterRepository _repository`.
  `HandleAsync`: `await _repository.DeleteAsync(command.Id, ct); return Result<VoidResult, Error>.Success(VoidResult.Value)`.
  (Delete is idempotent — no NOT_FOUND check required.)

---

## Phase 5: Application Tests (Parallel Batch)

**Purpose**: Unit test all four handlers using NSubstitute mocks. All four test classes
are independent and can be written in parallel.

> All tasks in this phase can run in parallel (T013–T016).

- [X] T013 [P] [TEST] CREATE `tests/Rentier.Application.Tests/GetImportersQueryHandlerTests.cs`:
  Mock `IImporterRepository` with NSubstitute.
  `HandleAsync_NoImporters_ReturnsEmptyList` — repo returns empty list; assert result is success,
  value is empty `IReadOnlyList<ImporterDto>`.
  `HandleAsync_WithImporters_ReturnsMappedDtos` — repo returns two `Importer` entities (created via
  `Importer.Create(...)`); assert result is success, DTO count = 2, all fields mapped correctly
  including nullable `TaxpayerProfileId` and `MailboxId`.

- [X] T014 [P] [TEST] CREATE `tests/Rentier.Application.Tests/AddImporterCommandHandlerTests.cs`:
  Mock `IImporterRepository` with NSubstitute.
  `HandleAsync_ValidCommand_AddsImporterAndReturnsGuid` — valid command with empty regex; assert result
  is success, returned Guid is non-empty, `AddAsync` called once.
  `HandleAsync_InvalidRegex_ReturnsFailure` — `AttachmentRegex = "[invalid"` (unclosed bracket); assert
  result is failure with code `"INVALID_REGEX"`, `AddAsync` never called.
  `HandleAsync_EmptyRegex_Succeeds` — empty `AttachmentRegex`; assert no regex error, `AddAsync` called.
  `HandleAsync_InvalidDisplayName_ReturnsDomainError` — empty `DisplayName`; assert result is failure
  with code `"DOMAIN_ERROR"`, `AddAsync` never called.
  `HandleAsync_NullForeignKeys_Succeeds` — null `TaxpayerProfileId` and `MailboxId`; assert success.

- [X] T015 [P] [TEST] CREATE `tests/Rentier.Application.Tests/UpdateImporterCommandHandlerTests.cs`:
  Mock `IImporterRepository` with NSubstitute.
  `HandleAsync_ValidUpdate_UpdatesAndReturnsSuccess` — repo returns existing importer; valid new values;
  assert `UpdateAsync` called once, result is success.
  `HandleAsync_NotFound_ReturnsFailure` — repo `GetByIdAsync` returns null; assert result is failure
  with code `"IMPORTER_NOT_FOUND"`, `UpdateAsync` never called.
  `HandleAsync_InvalidRegex_ReturnsFailure` — existing importer found; regex `"[invalid"`; assert
  failure with code `"INVALID_REGEX"`, `UpdateAsync` never called.
  `HandleAsync_InvalidDisplayName_ReturnsDomainError` — existing importer found; empty display name;
  assert failure with code `"DOMAIN_ERROR"`, `UpdateAsync` never called.

- [X] T016 [P] [TEST] CREATE `tests/Rentier.Application.Tests/DeleteImporterCommandHandlerTests.cs`:
  Mock `IImporterRepository` with NSubstitute.
  `HandleAsync_ExistingImporter_DeletesAndReturnsSuccess` — assert `DeleteAsync` called once with
  correct Guid, result is success.
  `HandleAsync_NonExistentId_StillReturnsSuccess` — `DeleteAsync` not throwing (no-op); assert result
  is success (idempotent delete).

**Checkpoint**: `dotnet build` on Application projects produces zero errors.
`dotnet test tests/Rentier.Application.Tests` — all handler tests pass.

---

## Phase 6: Infrastructure — EF Configuration, AppDbContext, and Migration

**Purpose**: Add `ImporterConfiguration`, register the `Importers` DbSet, and generate migration
`0005_ImporterConfiguration`. T017 and T018 can be written in parallel; T019 (migration) depends
on both.

- [X] T017 [P] [INFRASTRUCTURE] CREATE `src/Rentier.Infrastructure/Persistence/Configurations/ImporterConfiguration.cs`:
  `namespace Rentier.Infrastructure.Persistence.Configurations;`
  `public sealed class ImporterConfiguration : IEntityTypeConfiguration<Importer>`.
  `Configure`:
  `builder.ToTable("Importers"); builder.HasKey(i => i.Id);`
  `builder.Property(i => i.DisplayName).IsRequired().HasMaxLength(200);`
  `builder.Property(i => i.ReportType).IsRequired().HasConversion<int>();`
  `builder.Property(i => i.FromFilter).HasMaxLength(500).HasDefaultValue(string.Empty);`
  `builder.Property(i => i.SubjectFilter).HasMaxLength(500).HasDefaultValue(string.Empty);`
  `builder.Property(i => i.AttachmentRegex).HasMaxLength(1000).HasDefaultValue(string.Empty);`
  `builder.Property(i => i.PaymentNotes).HasMaxLength(4000).HasDefaultValue(string.Empty);`
  FK to TaxpayerProfile: `builder.HasOne<TaxpayerProfile>().WithMany().HasForeignKey(i => i.TaxpayerProfileId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);`
  FK to Mailbox: `builder.HasOne<Mailbox>().WithMany().HasForeignKey(i => i.MailboxId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);`
  Add usings: `Microsoft.EntityFrameworkCore; Microsoft.EntityFrameworkCore.Metadata.Builders; Rentier.Domain.Entities;`
  (`OnModelCreating` in `AppDbContext` already calls `ApplyConfigurationsFromAssembly(...)` — no
  additional registration needed.)

- [X] T018 [P] [INFRASTRUCTURE] MODIFY `src/Rentier.Infrastructure/Persistence/AppDbContext.cs`:
  Add `public DbSet<Importer> Importers => Set<Importer>();` property.
  Add `using Rentier.Domain.Entities;` if not already present.

- [X] T019 [INFRASTRUCTURE] RUN EF migration (depends on T017 + T018 compiling cleanly):
  Command: `dotnet ef migrations add 0005_ImporterConfiguration --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop`
  Verify the generated migration file is created under
  `src/Rentier.Infrastructure/Persistence/Migrations/` with a `Up()` method that creates the
  `Importers` table with columns: `Id` (TEXT PK), `DisplayName` (TEXT NOT NULL max 200),
  `ReportType` (INTEGER NOT NULL), `TaxpayerProfileId` (TEXT nullable FK → TaxpayerProfiles),
  `MailboxId` (TEXT nullable FK → Mailboxes), `FromFilter` (TEXT default ''),
  `SubjectFilter` (TEXT default ''), `AttachmentRegex` (TEXT default ''), `PaymentNotes` (TEXT default '').
  Correct any drift and re-run if generated DDL does not match.

---

## Phase 7: Infrastructure — Repository

**Purpose**: Implement `ImporterRepository` against the migrated schema. Depends on T019
(migration must exist and compile).

- [X] T020 [INFRASTRUCTURE] CREATE `src/Rentier.Infrastructure/Repositories/ImporterRepository.cs`:
  `namespace Rentier.Infrastructure.Repositories;`
  Implements `IImporterRepository` (namespace `Rentier.Application.Repositories`).
  Constructor-inject `AppDbContext _db`.
  `GetByIdAsync(Guid id, CancellationToken ct)`: `return await _db.Importers.FindAsync([id], ct);`
  (returns null for missing PK — do NOT use `FirstOrDefaultAsync`).
  `GetAllAsync(CancellationToken ct)`: `return await _db.Importers.AsNoTracking().ToListAsync(ct);`
  `AddAsync(Importer importer, CancellationToken ct)`: `_db.Importers.Add(importer); await _db.SaveChangesAsync(ct);`
  `UpdateAsync(Importer importer, CancellationToken ct)`: detach any stale tracked entry via
  `_db.ChangeTracker.Entries<Importer>().FirstOrDefault(e => e.Entity.Id == importer.Id)?.State = EntityState.Detached;`
  then `_db.Importers.Update(importer); await _db.SaveChangesAsync(ct);`
  `DeleteAsync(Guid id, CancellationToken ct)`: `var e = await _db.Importers.FindAsync([id], ct); if (e is not null) { _db.Importers.Remove(e); await _db.SaveChangesAsync(ct); }`
  **Do NOT use `ExecuteDeleteAsync`** — not supported by the in-memory SQLite test provider.
  Add usings: `Microsoft.EntityFrameworkCore; Rentier.Application.Repositories; Rentier.Domain.Entities; Rentier.Infrastructure.Persistence;`

---

## Phase 8: Infrastructure Tests

**Purpose**: Integration-test `ImporterRepository` against a real SQLite in-memory schema
(using `UseSqlite("Data Source=:memory:")` + `EnsureCreatedAsync`, NOT EF InMemory provider).

- [X] T021 [TEST] CREATE `tests/Rentier.Infrastructure.Tests/ImporterRepositoryTests.cs`:
  Use SQLite in-memory: `new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options`;
  call `await db.Database.EnsureCreatedAsync()` in setup to apply all EF configurations.
  Create a new `DbContext` instance per test (or use `Guid.NewGuid()` database name) to avoid state leakage.
  Helper: `private static Importer MakeImporter(string name = "Test") => Importer.Create(name, ReportType.IbkrCsv);`
  `GetAllAsync_Empty_ReturnsEmptyList` — fresh DB; assert count == 0.
  `AddAsync_NewImporter_PersistsCorrectly` — Add then GetAll; assert one entry; verify Id, DisplayName,
  ReportType, null TaxpayerProfileId, null MailboxId, empty filter strings all round-trip correctly.
  `GetByIdAsync_ExistingId_ReturnsImporter` — Add then GetById; assert entity not null.
  `GetByIdAsync_NotFound_ReturnsNull` — GetById with random Guid; assert null.
  `UpdateAsync_ExistingImporter_UpdatesAllFields` — Add; call `UpdateDetails` with new values; UpdateAsync;
  GetById; assert all updated fields persisted.
  `DeleteAsync_ExistingImporter_RemovesEntity` — Add; Delete; GetAll; assert count == 0.
  `DeleteAsync_NonExistentId_DoesNotThrow` — Delete with random Guid; assert no exception thrown.

**Checkpoint**: `dotnet build` on Infrastructure projects produces zero errors.
`dotnet test tests/Rentier.Infrastructure.Tests` — all repository tests pass.

---

## Phase 9: Desktop — Extensions

**Purpose**: Provide the `ReportType` → human-readable string mapping used by both the
ViewModel and the View. No dependencies on other Desktop tasks.

- [X] T022 [DESKTOP] CREATE `src/Rentier.Desktop/Extensions/ReportTypeExtensions.cs`:
  `namespace Rentier.Desktop.Extensions;`
  `using Rentier.Domain.Enums;`
  `public static class ReportTypeExtensions`
  `{ public static string ToDisplayString(this ReportType reportType) => reportType switch { ReportType.IbkrCsv => "IBKR CSV", _ => reportType.ToString() }; }`

---

## Phase 10: Desktop — ViewModels (User Stories 1–4)

**Purpose**: Implement `ImporterItemViewModel` (list item) and `ImporterSettingsViewModel`
(two-panel form). T023 is a prerequisite for T024.

**Stories covered**:
- US1 (View Importers): load list, `WhenActivated` data fetch, `SelectedImporter` → form population
- US2 (Add Importer): `AddNewCommand`, `SaveCommand` in new-importer mode
- US3 (Edit Importer): `SaveCommand` in edit mode, post-save list reload + selection restore
- US4 (Delete Importer): `DeleteCommand`, `CanExecute` when `SelectedImporter != null`

- [X] T023 [US1] [DESKTOP] CREATE `src/Rentier.Desktop/ViewModels/ImporterItemViewModel.cs`:
  `namespace Rentier.Desktop.ViewModels;`
  `public sealed class ImporterItemViewModel : ReactiveObject`.
  Properties: `public Guid Id { get; }`, `public string DisplayName { get; }`,
  `public string ReportTypeDisplay { get; }`.
  **Internal DTO storage** (for form population): `internal ImporterDto Dto { get; }` — stores the
  full `ImporterDto` so that `ImporterSettingsViewModel` can read all form fields when this item
  is selected (see T024 form population).
  Private constructor: `private ImporterItemViewModel(ImporterDto dto)` — stores `dto`; derives
  `Id = dto.Id`, `DisplayName = dto.DisplayName`, `ReportTypeDisplay = dto.ReportType.ToDisplayString()`.
  Static factory: `public static ImporterItemViewModel From(ImporterDto dto) => new(dto)`.
  Add usings: `Rentier.Application.DTOs; Rentier.Desktop.Extensions;`

- [X] T024 [US1] [DESKTOP] CREATE `src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs`:
  `namespace Rentier.Desktop.ViewModels;`
  `public sealed class ImporterSettingsViewModel : ReactiveObject, IActivatableViewModel`.
  `public ViewModelActivator Activator { get; } = new()`.

  **Form backing fields + properties** (all `RaiseAndSetIfChanged`):
  `string _displayName = ""` → `public string DisplayName`,
  `ReportType _reportType = ReportType.IbkrCsv` → `public ReportType ReportType`,
  `TaxpayerProfileDto? _selectedProfile` → `public TaxpayerProfileDto? SelectedProfile`,
  `MailboxDto? _selectedMailbox` → `public MailboxDto? SelectedMailbox`,
  `string _fromFilter = ""` → `public string FromFilter`,
  `string _subjectFilter = ""` → `public string SubjectFilter`,
  `string _attachmentRegex = ""` → `public string AttachmentRegex`,
  `string _paymentNotes = ""` → `public string PaymentNotes`.

  **State backing fields + properties** (all `RaiseAndSetIfChanged`):
  `ImporterItemViewModel? _selectedImporter` → `public ImporterItemViewModel? SelectedImporter`,
  `bool _isLoading` → `public bool IsLoading`,
  `string? _errorMessage` → `public string? ErrorMessage`,
  `string? _successMessage` → `public string? SuccessMessage`,
  `bool _isEditMode` → `public bool IsEditMode`.

  **Collections**:
  `public ObservableCollection<ImporterItemViewModel> ImporterItems { get; } = new()`
  `public ObservableCollection<TaxpayerProfileDto> AvailableProfiles { get; } = new()`
  `public ObservableCollection<MailboxDto> AvailableMailboxes { get; } = new()`
  `public IEnumerable<ReportType> AvailableReportTypes { get; } = Enum.GetValues<ReportType>()`

  **Constructor** injects (with optional `IScheduler? scheduler = null` for testability, defaulting to `RxApp.MainThreadScheduler`):
  `IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>> _getImporters`,
  `IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> _getProfile`,
  `IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>> _getMailboxes`,
  `ICommandHandler<AddImporterCommand, Result<Guid, Error>> _addImporter`,
  `ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>> _updateImporter`,
  `ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>> _deleteImporter`.

  **Commands**:
  `AddNewCommand = ReactiveCommand.Create(OnAddNew)` — always enabled; clears form; `IsEditMode = false`; `SelectedImporter = null`.
  `SaveCommand = ReactiveCommand.CreateFromTask(OnSaveAsync)` — dispatches to `AddImporterCommand` when `!IsEditMode`, `UpdateImporterCommand` when `IsEditMode`.
  `DeleteCommand = ReactiveCommand.CreateFromTask(OnDeleteAsync, this.WhenAnyValue(x => x.SelectedImporter, x => x.IsEditMode, (s, e) => s != null && e))` — only enabled when an importer is selected AND in edit mode (consistent with data-model.md §7).

  **`SelectedImporter` change side-effect**: when `SelectedImporter` is set to a non-null value,
  populate all form fields from `SelectedImporter.Dto` (the backing `ImporterDto` stored in
  `ImporterItemViewModel` — see T023):
  `DisplayName = SelectedImporter.Dto.DisplayName`,
  `ReportType = SelectedImporter.Dto.ReportType`,
  `SelectedProfile = AvailableProfiles.FirstOrDefault(p => p.Id == SelectedImporter.Dto.TaxpayerProfileId)`,
  `SelectedMailbox = AvailableMailboxes.FirstOrDefault(m => m.Id == SelectedImporter.Dto.MailboxId)`,
  `FromFilter = SelectedImporter.Dto.FromFilter`,
  `SubjectFilter = SelectedImporter.Dto.SubjectFilter`,
  `AttachmentRegex = SelectedImporter.Dto.AttachmentRegex`,
  `PaymentNotes = SelectedImporter.Dto.PaymentNotes`,
  set `IsEditMode = true`.
  When `SelectedImporter` is set to null, do NOT clear the form (clearing is done explicitly by
  `AddNewCommand` and by the post-delete flow).

  **SuccessMessage after save**: assign `SuccessMessage = Strings.Importers_Saved_Confirmation`
  (from `Rentier.Desktop.Resources` — key added in T031). Do NOT hardcode the string.
  Clear `SuccessMessage` and `ErrorMessage` at the start of each save/delete attempt.

  **`WhenActivated`**: call `LoadAsync` which runs sequentially:
  (1) load importers via `_getImporters.HandleAsync(...)`,
  (2) load profile via `_getProfile.HandleAsync(...)`,
  (3) load mailboxes via `_getMailboxes.HandleAsync(...)`.
  All `ObserveOn(scheduler)` to update collections on the UI thread.

  **Post-Add flow**: reload list; find the newly added importer by returned Guid; set `SelectedImporter`; `IsEditMode = true`.
  **Post-Update flow**: reload list; restore selection by saved Guid; `SuccessMessage` briefly set.
  **Post-Delete flow**: reload list; clear form; `SelectedImporter = null`; `IsEditMode = false`.

  **NO** `[Reactive]` attribute. **NO** `[ObservableProperty]` attribute. Use **only** `RaiseAndSetIfChanged`.
  Do **NOT** call `.Result` or `.Wait()` anywhere.

---

## Phase 11: Desktop — Views (User Stories 1–4)

**Purpose**: Implement the AXAML view and its code-behind. Depends on T024 (ViewModel must
compile before the view's bindings can reference it).

- [X] T025 [US1] [DESKTOP] CREATE `src/Rentier.Desktop/Views/ImporterSettingsView.axaml`:
  Root `UserControl` with `x:CompileBindings="False"`, `x:DataType="vm:ImporterSettingsViewModel"`,
  `xmlns:vm="using:Rentier.Desktop.ViewModels"`,
  `xmlns:res="using:Rentier.Desktop.Resources"`,
  `xmlns:views="using:Rentier.Desktop.Views"`.

  **Layout**: `Grid ColumnDefinitions="250,*"`.

  **Left panel** (Column 0): `DockPanel`.
  Bottom-docked: `Button Content="{x:Static res:Strings.Importers_AddNew_Button}" Command="{Binding AddNewCommand}"`.
  Fill: `ListBox ItemsSource="{Binding ImporterItems}" SelectedItem="{Binding SelectedImporter, Mode=TwoWay}"`.
  `ListBox.ItemTemplate` DataTemplate: `StackPanel` with `TextBlock Text="{Binding DisplayName}"` (primary)
  and `TextBlock Text="{Binding ReportTypeDisplay}"` (subtitle, smaller font / secondary style).

  **Right panel** (Column 1): `ScrollViewer` > `StackPanel Margin="16"`.
  Row per field using `TextBlock` label from `{x:Static res:Strings.XXX}` + input control:
  - `TextBlock "{x:Static res:Strings.Importers_DisplayName_Label}"` + `TextBox Text="{Binding DisplayName}"`
  - `TextBlock "{x:Static res:Strings.Importers_ReportType_Label}"` + ComboBox for ReportType:
    ```xml
    <ComboBox ItemsSource="{Binding AvailableReportTypes}"
              SelectedItem="{Binding ReportType, Mode=TwoWay}">
      <ComboBox.ItemTemplate>
        <DataTemplate>
          <TextBlock Text="{Binding Converter={x:Static local:ReportTypeDisplayConverter.Instance}}" />
        </DataTemplate>
      </ComboBox.ItemTemplate>
    </ComboBox>
    ```
    Add `xmlns:local="using:Rentier.Desktop.Converters"` to the UserControl namespace declarations.
    Create `src/Rentier.Desktop/Converters/ReportTypeDisplayConverter.cs` (or add to T022 as an alternative
    step): a `FuncValueConverter<ReportType, string>` that calls `rt.ToDisplayString()`:
    ```csharp
    namespace Rentier.Desktop.Converters;
    using Avalonia.Data.Converters;
    using Rentier.Desktop.Extensions;
    using Rentier.Domain.Enums;
    public static class ReportTypeDisplayConverter
    {
        public static readonly IValueConverter Instance =
            new FuncValueConverter<ReportType, string>(rt => rt.ToDisplayString());
    }
    ```
    This ensures Avalonia displays "IBKR CSV" (not the raw enum name "IbkrCsv") in the ComboBox.
  - `TextBlock "{x:Static res:Strings.Importers_TaxpayerProfile_Label}"` + `ComboBox ItemsSource="{Binding AvailableProfiles}" SelectedItem="{Binding SelectedProfile, Mode=TwoWay}" DisplayMemberBinding="{Binding FullName}"`; add null-placeholder item text `"{x:Static res:Strings.Importers_NoProfile_Placeholder}"`.
  - `TextBlock "{x:Static res:Strings.Importers_Mailbox_Label}"` + `ComboBox ItemsSource="{Binding AvailableMailboxes}" SelectedItem="{Binding SelectedMailbox, Mode=TwoWay}"`; DataTemplate displays `Username @ Host`; null-placeholder `"{x:Static res:Strings.Importers_NoMailbox_Placeholder}"`.
  - `TextBlock "{x:Static res:Strings.Importers_FromFilter_Label}"` + `TextBox Text="{Binding FromFilter}"`
  - `TextBlock "{x:Static res:Strings.Importers_SubjectFilter_Label}"` + `TextBox Text="{Binding SubjectFilter}"`
  - `TextBlock "{x:Static res:Strings.Importers_AttachmentRegex_Label}"` + `TextBox Text="{Binding AttachmentRegex}"`
  - `TextBlock "{x:Static res:Strings.Importers_PaymentNotes_Label}"` + `TextBox Text="{Binding PaymentNotes}" AcceptsReturn="True" MinLines="3"`
  Button row: `Button Content="{x:Static res:Strings.Importers_Save_Button}" Command="{Binding SaveCommand}"`,
  `Button Content="{x:Static res:Strings.Importers_Delete_Button}" Command="{Binding DeleteCommand}"`.
  Feedback: `TextBlock Text="{Binding ErrorMessage}" Foreground="Red" IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"`,
  `TextBlock Text="{Binding SuccessMessage}" Foreground="Green" IsVisible="{Binding SuccessMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"`,
  `ProgressBar IsIndeterminate="True" IsVisible="{Binding IsLoading}"`.
  **NO hardcoded strings** — every user-visible string MUST come from `{x:Static res:Strings.XXX}`.

- [X] T026 [US1] [DESKTOP] CREATE `src/Rentier.Desktop/Views/ImporterSettingsView.axaml.cs`:
  `namespace Rentier.Desktop.Views;`
  `public partial class ImporterSettingsView : ReactiveUserControl<ImporterSettingsViewModel>`
  `{ public ImporterSettingsView() { InitializeComponent(); } }`
  Add usings: `ReactiveUI; Rentier.Desktop.ViewModels;`

---

## Phase 12: DI Wiring + Resource Strings (Parallel Batch)

**Purpose**: Register all new types in the DI container, add the 4th tab to the Settings view,
and add all localisation string keys. Most tasks are independent and can be written in parallel.

> T027–T029, T031 can run in parallel. T030 should follow T025 and T029 to avoid merge conflicts
> on `SettingsView.axaml`.

- [X] T027 [P] [DI] MODIFY `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`:
  Add `services.AddTransient<IImporterRepository, ImporterRepository>();`
  Add usings: `using Rentier.Application.Repositories; using Rentier.Infrastructure.Repositories;`
  (Add only if not already present; do not remove any existing registrations.)

- [X] T028 [P] [DI] MODIFY `src/Rentier.Desktop/Composition/CompositionRoot.cs`:
  Register the following (all `AddTransient`):
  `IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>` → `GetImportersQueryHandler`
  `ICommandHandler<AddImporterCommand, Result<Guid, Error>>` → `AddImporterCommandHandler`
  `ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>` → `UpdateImporterCommandHandler`
  `ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>` → `DeleteImporterCommandHandler`
  `ImporterSettingsViewModel` (AddTransient, self-registration)
  Add required usings for all new types.

- [X] T029 [P] [DI] MODIFY `src/Rentier.Desktop/ViewModels/SettingsViewModel.cs`:
  Add `public ImporterSettingsViewModel ImportersTab { get; }` property.
  Add `ImporterSettingsViewModel importersTab` as the **4th** constructor parameter
  (after existing `profileTab`, `holidayTab`, `mailboxesTab`).
  Add `ImportersTab = importersTab;` assignment in constructor body.
  Keep all existing parameters and assignments unchanged.

- [X] T030 [DESKTOP] MODIFY `src/Rentier.Desktop/Views/SettingsView.axaml`:
  Add the 4th `<TabItem>` after the existing Mailboxes tab:
  `<TabItem Header="{x:Static res:Strings.Settings_Importers_TabHeader}"><views:ImporterSettingsView DataContext="{Binding ImportersTab}" /></TabItem>`
  The `xmlns:views` namespace is already present from feature 004 — do not add a duplicate.

- [X] T031 [P] [DESKTOP] MODIFY `src/Rentier.Desktop/Resources/Strings.resx`:
  Add the following string entries:
  `Settings_Importers_TabHeader` = `"Importers"`
  `Importers_DisplayName_Label` = `"Display Name"`
  `Importers_ReportType_Label` = `"Report Type"`
  `Importers_TaxpayerProfile_Label` = `"Taxpayer Profile"`
  `Importers_Mailbox_Label` = `"Mailbox"`
  `Importers_FromFilter_Label` = `"From Filter"`
  `Importers_SubjectFilter_Label` = `"Subject Filter"`
  `Importers_AttachmentRegex_Label` = `"Attachment Regex"`
  `Importers_PaymentNotes_Label` = `"Payment Notes"`
  `Importers_AddNew_Button` = `"Add New"`
  `Importers_Save_Button` = `"Save"`
  `Importers_Delete_Button` = `"Delete"`
  `Importers_Saved_Confirmation` = `"Importer saved."`
  `Importers_NoProfile_Placeholder` = `"— No Profile —"`
  `Importers_NoMailbox_Placeholder` = `"— No Mailbox —"`

---

## Phase 13: Smoke Test Update + Desktop ViewModel Tests

**Purpose**: Ensure the updated 4-parameter `SettingsViewModel` constructor does not break any
existing smoke tests, and add ViewModel behaviour tests for all four user stories.

- [X] T032 [P] [TEST] CHECK and UPDATE smoke test in `tests/Rentier.Desktop.Tests/`:
  Search for any test that instantiates `SettingsViewModel` (e.g. `MainWindowViewModelSmokeTests.cs`
  or similar). If found: update the constructor call to pass a 4th mock `ImporterSettingsViewModel`
  created with NSubstitute or a minimal stub (mock all 6 handler dependencies).
  If no such test exists: skip this task.

- [X] T033 [TEST] CREATE `tests/Rentier.Desktop.Tests/ImporterSettingsViewModelTests.cs`:
  Mock all 6 handler interfaces with NSubstitute. Use `IScheduler` injection (pass
  `ImmediateScheduler.Instance` or `RxApp.MainThreadScheduler` override for synchronous test execution).
  `WhenActivated_NoImporters_ImporterItemsIsEmpty` — `GetImportersQuery` returns empty list;
  activate VM; assert `ImporterItems.Count == 0`.
  `WhenActivated_LoadsAvailableMailboxes` — `GetMailboxesQuery` returns two `MailboxDto` instances;
  activate VM; assert `AvailableMailboxes.Count == 2`.
  `WhenActivated_LoadsAvailableProfiles` — `GetTaxpayerProfileQuery` returns a profile; activate VM;
  assert `AvailableProfiles.Count == 1`.
  `SelectImporter_PopulatesFormFields` — create an `ImporterDto` with all fields set to known values;
  create `ImporterItemViewModel.From(dto)` and add it to `ImporterItems`; set `SelectedImporter` to
  that item; assert all form fields (`DisplayName`, `ReportType`, `FromFilter`, `SubjectFilter`,
  `AttachmentRegex`, `PaymentNotes`) match the DTO values. (This relies on `ImporterItemViewModel`
  storing the full `ImporterDto` via the `.Dto` property — see T023.)
  `AddNewCommand_ClearsFormAndSetsEditModeFalse` — pre-fill form; execute `AddNewCommand`;
  assert `DisplayName == ""`, `IsEditMode == false`, `SelectedImporter == null`.
  `SaveCommand_NewImporter_CallsAddHandler` — `IsEditMode = false`; fill `DisplayName`; execute
  `SaveCommand`; assert `AddImporterCommand` handler called once, `UpdateImporterCommand` never called.
  `SaveCommand_EditMode_CallsUpdateHandler` — `IsEditMode = true`, `SelectedImporter` set; execute
  `SaveCommand`; assert `UpdateImporterCommand` handler called once, `AddImporterCommand` never called.
  `DeleteCommand_CanExecute_OnlyWhenImporterSelected` — assert `DeleteCommand.CanExecute` is false
  when `SelectedImporter == null`; set `SelectedImporter`; assert `CanExecute` becomes true.

**Checkpoint**: `dotnet build` on all projects produces zero errors.
`dotnet test` on entire solution — all tests pass. Application launches and navigates to
Settings → Importers without errors.

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Domain)
  └─► Phase 2 (Domain Tests)          [parallel eligible after Phase 1]
  └─► Phase 3 (App Records)           [parallel after Phase 1]
        └─► Phase 4 (App Handlers)    [after Phase 3]
              └─► Phase 5 (App Tests) [parallel after Phase 4]
  └─► Phase 6 (Infra EF+Migration)    [after Phase 1; T017+T018 parallel; T019 after both]
        └─► Phase 7 (Infra Repo)      [after Phase 6]
              └─► Phase 8 (Infra Tests) [after Phase 7]
  └─► Phase 9 (Desktop Extensions)    [after Phase 1]
        └─► Phase 10 (Desktop VMs)    [after Phases 3+4+9; T023 before T024]
              └─► Phase 11 (Desktop Views) [after Phase 10]
                    └─► Phase 12 (DI Wiring) [T027-T031; most parallel; T030 after T025+T029]
                          └─► Phase 13 (Smoke+VM Tests) [after Phase 12]
```

### User Story Coverage Map

| Story | Priority | Description | Key Tasks |
|---|---|---|---|
| US1 | P1 | View configured importers | T001–T022 (full stack), T023–T026 (Desktop) |
| US2 | P1 | Add a new importer | T024 (`AddNewCommand`, `SaveCommand` add path), T025 |
| US3 | P2 | Edit an existing importer | T024 (`SaveCommand` update path), T025 |
| US4 | P3 | Delete an importer | T024 (`DeleteCommand`), T025, T027–T031 |

### Within Each Phase

- Domain tasks complete → Application tasks can start
- EF config + DbSet (T017–T018, parallel) → Migration (T019)
- Migration → Repository (T020)
- ViewModel (T023–T024) → View (T025–T026) → DI wiring (T027–T031)
- DI wiring complete → Smoke test update (T032) → Final VM tests (T033)

### Parallel Opportunities

```
# Phase 3 — all 5 records in parallel:
T004  T005  T006  T007  T008

# Phase 4 — query handler first, then 3 command handlers in parallel:
T009 → (T010  T011  T012)

# Phase 5 — all 4 test classes in parallel:
T013  T014  T015  T016

# Phase 6 — EF config + DbSet in parallel, then migration:
(T017  T018) → T019

# Phase 12 — most DI tasks in parallel:
(T027  T028  T029  T031) → T030

# Phase 13 — smoke test check in parallel with VM test creation:
T032 (check/update) || T033 (create)
```

---

## Parallel Example: User Story 1 (View Importers)

```
# All Application record tasks together (Phase 3):
Task T004: Create ImporterDto
Task T005: Create GetImportersQuery
Task T006: Create AddImporterCommand
Task T007: Create UpdateImporterCommand
Task T008: Create DeleteImporterCommand

# Infrastructure EF tasks together (Phase 6):
Task T017: Create ImporterConfiguration.cs
Task T018: Add Importers DbSet to AppDbContext

# Application handler tests together (Phase 5):
Task T013: GetImportersQueryHandlerTests
Task T014: AddImporterCommandHandlerTests
Task T015: UpdateImporterCommandHandlerTests
Task T016: DeleteImporterCommandHandlerTests
```

---

## Implementation Strategy

### MVP First (User Story 1 — View Importers)

1. Complete Phase 1: Domain (T001–T002)
2. Complete Phase 2: Domain Tests (T003)
3. Complete Phase 3–5: Application CQRS layer (T004–T016)
4. Complete Phase 6–8: Infrastructure persistence layer (T017–T021)
5. Complete Phase 9–10: Desktop Extensions + `ImporterItemViewModel` (T022–T023)
6. Complete `ImporterSettingsViewModel` load/display path only (T024 — `WhenActivated` + read-only form population)
7. Complete Phase 11: Desktop Views (T025–T026)
8. Complete Phase 12: DI Wiring + Strings (T027–T031)
9. **STOP and VALIDATE**: Launch app → Settings → Importers → verify list loads and selection populates form
10. Complete Phase 13: Smoke test + VM tests (T032–T033)

### Incremental Delivery

1. US1 complete → list is readable, form populated on selection (MVP)
2. US2 complete → Add New + Save (write path proven)
3. US3 complete → in-place editing (update path proven)
4. US4 complete → Delete + housekeeping (delete path proven)
5. Phase 13 complete → all tests green, ready for merge

### Single-Developer Sequence (strict order)

`T001 → T002 → T003 → T004–T008 (any order) → T009–T012 (any order) → T013–T016 (any order) → T017–T018 (any order) → T019 → T020 → T021 → T022 → T023 → T024 → T025 → T026 → T027–T031 (any order) → T030 → T032 → T033`

---

## Notes

- `[P]` tasks = different files, no blocking dependency on an incomplete sibling
- `[US1]`–`[US4]` labels map tasks to specific user stories for traceability
- `IImporterRepository` namespace: **`Rentier.Application.Repositories`** (not `Rentier.Infrastructure`)
- `Result<T,E>` API: use `.Success(value)` and `.Failure(error)` — `.Ok()` does **not** exist
- `VoidResult.Value` for all void command handler returns
- **No** `.Result` / `.Wait()` anywhere — all async paths must use `await`
- **No** `[Reactive]` or `[ObservableProperty]` — use `RaiseAndSetIfChanged` exclusively
- XAML string prefix: `res:` (NOT `lang:`) — consistent with all existing Rentier views
- `x:CompileBindings="False"` on root of all new AXAML views
- Infrastructure tests use **SQLite in-memory** (`UseSqlite("Data Source=:memory:")` + `EnsureCreatedAsync`) — NOT `UseInMemoryDatabase`
- `ExecuteDeleteAsync` is **forbidden** in repository — use `FindAsync` + `Remove` + `SaveChangesAsync`
- `ReportType` enum lives in namespace `Rentier.Domain.Enums`
- `MailboxDto` and `TaxpayerProfileDto` are in namespace `Rentier.Application.DTOs`
- Verify `dotnet build` after each phase before proceeding — do not accumulate compile errors
- Commit after each phase or logical group completes
