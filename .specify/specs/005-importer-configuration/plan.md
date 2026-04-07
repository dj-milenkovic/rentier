# Implementation Plan: Importer Configuration

**Branch**: `feature/005-importer-configuration` | **Date**: 2026-04-06 | **Spec**: `specs/005-importer-configuration/spec.md`  
**Input**: Feature specification from `.specify/specs/005-importer-configuration/spec.md`

---

## Summary

Feature 005 delivers **Settings → Importers**: a two-panel CRUD UI for configuring IBKR statement importer entries. Users can add, edit, and delete importer configurations. Each importer stores a display name, report type (`IbkrCsv`), optional FK references to a `TaxpayerProfile` and a `Mailbox`, three email filter fields (`FromFilter`, `SubjectFilter`, `AttachmentRegex`), and free-text `PaymentNotes`.

The feature completely redesigns the existing stub `Importer` domain entity (which had only `Id`, `DisplayName`, `FilterExpression`), introduces EF migration `0005_ImporterConfiguration` creating the `Importers` table, wires full CQRS Application layer (1 query + 3 commands, 4 handlers), and adds a reactive Avalonia two-panel settings tab as the fourth tab in Settings. `AttachmentRegex` is validated in the handler via `new Regex(...)` / `ArgumentException`. No actual email processing occurs — this feature is configuration only.

---

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+ (FluentTheme), ReactiveUI, EF Core 8 (SQLite provider), Microsoft.Extensions.DependencyInjection, xUnit + FluentAssertions + NSubstitute  
**Storage**: SQLite via EF Core 8; new `Importers` table created by migration `0005_ImporterConfiguration`; FKs to `TaxpayerProfiles` and `Mailboxes` with `DeleteBehavior.SetNull`  
**Testing**: xUnit + FluentAssertions + NSubstitute; SQLite in-memory provider for Infrastructure integration tests  
**Target Platform**: Windows desktop (Avalonia)  
**Project Type**: Desktop application  
**Performance Goals**: Save/delete < 200 ms on idle local SQLite; list render < 100 ms for ≤ 50 importers  
**Constraints**: Single-user, single-process, offline-only; no email processing in this feature; no `.Result`/`.Wait()`; no hard-coded UI strings; `AttachmentRegex` validation via `System.Text.RegularExpressions` in Application handler  
**Scale/Scope**: Multiple importers per database; 1 new table (9 columns); 4 handlers; 2 new ViewModels; ~38 tests across 7 new test classes; depends on Feature 004 being applied first (Mailboxes table must exist before migration 0005)

---

## Constitution Check

_GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design._

- [x] **Clean Architecture boundary is preserved** (`Desktop → Application → Domain`; Infrastructure implements Application contracts only).  
  `ImporterSettingsViewModel` calls Application handlers only; no repository or EF imports in Desktop; `IImporterRepository` defined in Application (already exists); `ImporterRepository` implements it in Infrastructure.

- [x] **All monetary/rate/percentage values are modeled as `decimal`**.  
  `Importer` contains no monetary fields; `decimal` rule is not triggered by this feature.

- [x] **All business dates are modeled as `DateOnly`**.  
  `Importer` contains no date fields. This feature has no date/time values anywhere.

- [x] **Security/privacy constraints hold**.  
  No secrets stored in this feature. `PaymentNotes` is user data stored only in local SQLite — consistent with local-first policy.

- [x] **External network usage is limited to approved endpoints**.  
  This feature makes zero outbound network calls. ✅ PASS.

- [x] **All I/O paths are async; UI avoids blocking calls**.  
  Repository methods are `async Task`/`Task<T>`; handlers are `async Task<Result<T, Error>>`; ViewModel uses `ReactiveCommand.CreateFromTask`; UI updates via `RxApp.MainThreadScheduler`. `.Result`/`.Wait()` are prohibited.

- [x] **Tests and coverage impact are defined**.  
  Domain: 100% rule/state coverage (`ImporterTests.cs`). Application: ≥ 90% coverage (4 handler test classes). Infrastructure: EF Core in-memory integration tests (`ImporterRepositoryTests.cs`). Desktop: `ImporterSettingsViewModelTests.cs`.

- [x] **Feature work is mapped to an approved spec task**.  
  Branch `feature/005-importer-configuration`; spec under `.specify/specs/005-importer-configuration/`.

**Result**: ✅ All 8 gates PASS. No violations requiring justification.

---

## Project Structure

### Documentation (this feature)

```text
.specify/specs/005-importer-configuration/
├── plan.md                              ← This file (speckit.plan output)
├── research.md                          ← Phase 0 output  ✅ GENERATED
├── data-model.md                        ← Phase 1 output  ✅ GENERATED
├── quickstart.md                        ← Phase 1 output  ✅ GENERATED
├── contracts/
│   ├── IImporterRepository.cs           ← Full interface contract  ✅ GENERATED
│   └── ImporterEntityDesign.md          ← Before/after entity diff  ✅ GENERATED
└── tasks.md                             ← Phase 2 output (speckit.tasks — NOT created by speckit.plan)
```

### Source Code (repository root)

```text
src/Rentier.Domain/Entities/Importer.cs
    MODIFIED — complete redesign:
        - Remove public constructor(Guid, string, string)
        - Add private Importer() {} for EF materialization
        - Change all { get; } to { get; private set; }
        - Remove FilterExpression property
        - Add ReportType, TaxpayerProfileId, MailboxId, FromFilter,
          SubjectFilter, AttachmentRegex, PaymentNotes properties
        - Add static Importer.Create(string displayName, ReportType) factory
        - Add void UpdateDetails(...) mutation method with full field set

src/Rentier.Domain/Enums/ReportType.cs
    NEW — new Enums/ folder
        public enum ReportType { IbkrCsv = 0 }

src/Rentier.Application/DTOs/ImporterDto.cs
    NEW — sealed record ImporterDto(Guid Id, string DisplayName, ReportType ReportType,
          Guid? TaxpayerProfileId, Guid? MailboxId, string FromFilter, string SubjectFilter,
          string AttachmentRegex, string PaymentNotes)

src/Rentier.Application/Queries/GetImportersQuery.cs
    NEW — sealed record GetImportersQuery()
          → Result<IReadOnlyList<ImporterDto>, Error>

src/Rentier.Application/Commands/AddImporterCommand.cs
    NEW — sealed record AddImporterCommand(string DisplayName, ReportType ReportType,
          Guid? TaxpayerProfileId, Guid? MailboxId, string FromFilter, string SubjectFilter,
          string AttachmentRegex, string PaymentNotes)
          → Result<Guid, Error>

src/Rentier.Application/Commands/UpdateImporterCommand.cs
    NEW — sealed record UpdateImporterCommand(Guid Id, string DisplayName, ReportType ReportType,
          Guid? TaxpayerProfileId, Guid? MailboxId, string FromFilter, string SubjectFilter,
          string AttachmentRegex, string PaymentNotes)
          → Result<VoidResult, Error>

src/Rentier.Application/Commands/DeleteImporterCommand.cs
    NEW — sealed record DeleteImporterCommand(Guid Id)
          → Result<VoidResult, Error>

src/Rentier.Application/Handlers/GetImportersQueryHandler.cs
    NEW — IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>
          GetAllAsync → project each Importer to ImporterDto → return as IReadOnlyList

src/Rentier.Application/Handlers/AddImporterCommandHandler.cs
    NEW — ICommandHandler<AddImporterCommand, Result<Guid, Error>>
          Validate AttachmentRegex (try new Regex / catch ArgumentException → INVALID_REGEX)
          → Importer.Create(DisplayName, ReportType)
          → importer.UpdateDetails(...) to set all optional fields
          → IImporterRepository.AddAsync
          → return Result.Success(importer.Id)

src/Rentier.Application/Handlers/UpdateImporterCommandHandler.cs
    NEW — ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>
          GetByIdAsync → null → IMPORTER_NOT_FOUND
          → Validate AttachmentRegex
          → importer.UpdateDetails(...)
          → IImporterRepository.UpdateAsync
          → return Result.Success(VoidResult.Value)

src/Rentier.Application/Handlers/DeleteImporterCommandHandler.cs
    NEW — ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>
          IImporterRepository.DeleteAsync(id)
          → return Result.Success(VoidResult.Value)
          (DeleteAsync is no-op if not found — handler does not verify existence)

src/Rentier.Infrastructure/Persistence/Configurations/ImporterConfiguration.cs
    NEW — IEntityTypeConfiguration<Importer>
          Table: "Importers"; PK: Id; Required: DisplayName (max 200), ReportType (int);
          Optional: TaxpayerProfileId (FK→TaxpayerProfiles, SetNull),
                    MailboxId (FK→Mailboxes, SetNull);
          Optional strings: FromFilter (max 500), SubjectFilter (max 500),
                            AttachmentRegex (max 1000), PaymentNotes (max 4000)

src/Rentier.Infrastructure/Persistence/AppDbContext.cs
    MODIFIED — add: public DbSet<Importer> Importers => Set<Importer>();
          No OnModelCreating change needed (ApplyConfigurationsFromAssembly discovers
          ImporterConfiguration automatically)

src/Rentier.Infrastructure/Repositories/ImporterRepository.cs
    NEW — implements IImporterRepository with AppDbContext
          GetByIdAsync: FindAsync([id], ct)  ← use FindAsync (PK cache); DO NOT use FirstOrDefaultAsync
          GetAllAsync: AsNoTracking().ToListAsync() cast to IReadOnlyList
          AddAsync: Add + SaveChangesAsync
          UpdateAsync: detach stale entry → Update + SaveChangesAsync
          DeleteAsync: FindAsync([id], ct) → if not null: Remove + SaveChangesAsync  ← DO NOT use ExecuteDeleteAsync

src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs
    MODIFIED — add:
          services.AddTransient<IImporterRepository, ImporterRepository>();

src/Rentier.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_0005_ImporterConfiguration.cs
    NEW — generated by:
          dotnet ef migrations add 0005_ImporterConfiguration \
            --project src/Rentier.Infrastructure \
            --startup-project src/Rentier.Desktop
          Creates table Importers with 9 columns; FK constraints to TaxpayerProfiles and Mailboxes
          PREREQUISITE: Feature 004 migration must be applied first (Mailboxes table must exist)

src/Rentier.Desktop/Extensions/ReportTypeExtensions.cs
    NEW — public static class ReportTypeExtensions
          ToDisplayString(this ReportType) → "IBKR CSV" for IbkrCsv

src/Rentier.Desktop/ViewModels/ImporterItemViewModel.cs
    NEW — ReactiveObject
          Properties: Guid Id, string DisplayName, string SubTitle (ReportType.ToDisplayString()),
                      bool IsNew (marks unsaved new entry)
          Two constructors:
            ImporterItemViewModel(ImporterDto dto)
            ImporterItemViewModel()  ← IsNew = true placeholder

src/Rentier.Desktop/ViewModels/ImporterSettingsViewModel.cs
    NEW — ReactiveObject; two-panel VM
          ObservableCollection<ImporterItemViewModel> ImporterItems
          ImporterItemViewModel? SelectedImporter  (RaiseAndSetIfChanged)
          string DisplayName, string FromFilter, string SubjectFilter,
          string AttachmentRegex, string PaymentNotes  (RaiseAndSetIfChanged)
          ReportType SelectedReportType  (RaiseAndSetIfChanged)
          TaxpayerProfileDto? SelectedTaxpayerProfile  (RaiseAndSetIfChanged)
          MailboxDto? SelectedMailbox  (RaiseAndSetIfChanged)
          ObservableCollection<ReportType> AvailableReportTypes
          ObservableCollection<TaxpayerProfileDto> AvailableProfiles
          ObservableCollection<MailboxDto> AvailableMailboxes
          bool IsEditMode, bool IsLoading
          string? ErrorMessage, string? SuccessMessage
          ReactiveCommand AddNewCommand (always enabled)
          ReactiveCommand SaveCommand (canExecute: DisplayName.Length > 0)
          ReactiveCommand DeleteCommand (canExecute: SelectedImporter != null && IsEditMode)
          Injected: IQueryHandler<GetImportersQuery,...>, IQueryHandler<GetTaxpayerProfileQuery,...>,
                    IQueryHandler<GetMailboxesQuery,...>, ICommandHandler<AddImporterCommand,...>,
                    ICommandHandler<UpdateImporterCommand,...>, ICommandHandler<DeleteImporterCommand,...>

src/Rentier.Desktop/Views/ImporterSettingsView.axaml
    NEW — ReactiveUserControl<ImporterSettingsViewModel>
          x:CompileBindings="False"
          Two-panel Grid (250px left, * right):
            Left: ListBox bound to ImporterItems, ItemTemplate shows DisplayName + SubTitle
            Right: StackPanel form with:
              TextBox DisplayName (max 200)
              ComboBox ReportType (ItemsSource=AvailableReportTypes, display via converter)
              ComboBox TaxpayerProfile (ItemsSource=AvailableProfiles, DisplayMemberPath=Jmbg or Name)
              ComboBox Mailbox (ItemsSource=AvailableMailboxes, DisplayMemberPath=Host or Username)
              TextBox FromFilter
              TextBox SubjectFilter
              TextBox AttachmentRegex
              TextBox PaymentNotes (multi-line, AcceptsReturn=True, max lines 6)
              Toolbar: Add New, Save, Delete buttons
              TextBlock ErrorMessage (red, visible when non-empty)
              TextBlock SuccessMessage (green, visible when non-empty)
          All visible strings via Strings.resx

src/Rentier.Desktop/Views/ImporterSettingsView.axaml.cs
    NEW — ReactiveUserControl<ImporterSettingsViewModel> code-behind
          WhenActivated calls ViewModel.LoadAsync()

src/Rentier.Desktop/Composition/CompositionRoot.cs
    MODIFIED — add handler registrations:
          services.AddTransient<ICommandHandler<AddImporterCommand, Result<Guid, Error>>,
                                AddImporterCommandHandler>();
          services.AddTransient<ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>,
                                UpdateImporterCommandHandler>();
          services.AddTransient<ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>,
                                DeleteImporterCommandHandler>();
          services.AddTransient<IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>,
                                GetImportersQueryHandler>();
          services.AddTransient<ImporterSettingsViewModel>();
          Update SettingsViewModel factory to inject ImporterSettingsViewModel as 4th parameter

src/Rentier.Desktop/ViewModels/SettingsViewModel.cs
    MODIFIED — add ImporterSettingsViewModel ImportersTab { get; }
               Update constructor to accept ImporterSettingsViewModel importersTab as 4th param

src/Rentier.Desktop/Views/SettingsView.axaml
    MODIFIED — add 4th TabItem (after Mailboxes tab):
               <TabItem Header="{x:Static res:Strings.Settings_Importers_TabHeader}">
                 <views:ImporterSettingsView DataContext="{Binding ImportersTab}" />
               </TabItem>

src/Rentier.Desktop/Resources/Strings.resx
    MODIFIED — add 14 new keys:
               Settings_Importers_TabHeader = "Importers"
               Importers_DisplayName_Label = "Display Name"
               Importers_ReportType_Label = "Report Type"
               Importers_TaxpayerProfile_Label = "Taxpayer Profile"
               Importers_Mailbox_Label = "Mailbox"
               Importers_FromFilter_Label = "From Filter"
               Importers_SubjectFilter_Label = "Subject Filter"
               Importers_AttachmentRegex_Label = "Attachment Pattern (Regex)"
               Importers_PaymentNotes_Label = "Payment Notes"
               Importers_AddNew_Button = "Add New"
               Importers_Save_Button = "Save"
               Importers_Delete_Button = "Delete"
               Importers_Saved_Confirmation = "Importer saved."
               Importers_NoneOption_Label = "(none)"

tests/Rentier.Domain.Tests/ImporterTests.cs
    NEW — ~10 test cases:
          Create_ValidDisplayName_ReturnsImporterWithNewGuid
          Create_BlankDisplayName_ThrowsDomainException
          Create_DisplayNameExceeds200Chars_ThrowsDomainException
          Create_DefaultsToIbkrCsvReportType
          Create_DefaultsToEmptyFilterFields
          UpdateDetails_ValidArgs_UpdatesAllProperties
          UpdateDetails_BlankDisplayName_ThrowsDomainException
          UpdateDetails_DisplayNameExceeds200Chars_ThrowsDomainException
          UpdateDetails_PaymentNotesExceeds4000Chars_ThrowsDomainException
          UpdateDetails_NullStrings_SetsEmptyStringDefaults

tests/Rentier.Application.Tests/AddImporterCommandHandlerTests.cs
    NEW — ~4 test cases:
          Handle_ValidCommand_ReturnsSuccessWithGuid
          Handle_InvalidAttachmentRegex_ReturnsFailure
          Handle_EmptyAttachmentRegex_IsAllowed
          Handle_BlankDisplayName_DomainExceptionWrappedAsFailure

tests/Rentier.Application.Tests/UpdateImporterCommandHandlerTests.cs
    NEW — ~4 test cases:
          Handle_ExistingImporter_UpdatesAndReturnsSuccess
          Handle_NonExistentId_ReturnsNotFoundFailure
          Handle_InvalidAttachmentRegex_ReturnsFailureWithoutPersisting
          Handle_ValidRegex_UpdatesSuccessfully

tests/Rentier.Application.Tests/DeleteImporterCommandHandlerTests.cs
    NEW — ~2 test cases:
          Handle_ExistingImporter_DeletesAndReturnsSuccess
          Handle_NonExistentId_IsNoOpAndReturnsSuccess

tests/Rentier.Application.Tests/GetImportersQueryHandlerTests.cs
    NEW — ~2 test cases:
          Handle_EmptyRepo_ReturnsEmptyList
          Handle_MultipleImporters_ReturnsAllProjectedToDtos

tests/Rentier.Infrastructure.Tests/ImporterRepositoryTests.cs
    NEW — ~6 test cases (EF Core InMemory):
          AddAsync_ThenGetAll_ReturnsImporter
          GetByIdAsync_ExistingId_ReturnsImporter
          GetByIdAsync_MissingId_ReturnsNull
          UpdateAsync_ChangesPersistedCorrectly
          DeleteAsync_ExistingId_Removes
          DeleteAsync_MissingId_IsNoOp

tests/Rentier.Desktop.Tests/ImporterSettingsViewModelTests.cs
    NEW — ~7 test cases:
          SaveCommand_DisabledWhenDisplayNameEmpty
          AddNewCommand_AddsUnsavedItemToList
          SaveCommand_OnNewItem_CallsAddHandler_AndUpdatesId
          SaveCommand_OnExistingItem_CallsUpdateHandler
          DeleteCommand_CanExecute_OnlyWhenImporterSelectedAndEditMode
          DeleteCommand_RemovesItemFromList
          LoadAsync_PopulatesImporterItemsAndDropdowns
```

---

## Design Notes

### EF FK Configuration (No Navigation Properties)

The `ImporterConfiguration` configures two optional FK relationships without navigation properties:

```csharp
// FK to TaxpayerProfile — scalar property, no nav prop
builder.HasOne<TaxpayerProfile>()
    .WithMany()
    .HasForeignKey(i => i.TaxpayerProfileId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);

// FK to Mailbox — scalar property, no nav prop
builder.HasOne<Mailbox>()
    .WithMany()
    .HasForeignKey(i => i.MailboxId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);
```

`TaxpayerProfileId` and `MailboxId` are real CLR properties on `Importer` (type `Guid?`). The `HasForeignKey(i => i.Prop)` expression overload is used (not the string overload). EF generates `FOREIGN KEY (TaxpayerProfileId) REFERENCES TaxpayerProfiles(Id) ON DELETE SET NULL` and similarly for Mailboxes.

### Regex Validation in Handler

```csharp
if (!string.IsNullOrEmpty(command.AttachmentRegex))
{
    try
    {
        _ = new Regex(command.AttachmentRegex, RegexOptions.None, TimeSpan.FromSeconds(1));
    }
    catch (ArgumentException ex)
    {
        return Result<Guid, Error>.Failure(new Error("INVALID_REGEX", ex.Message));
    }
}
```

Applied in both `AddImporterCommandHandler` and `UpdateImporterCommandHandler` before any domain mutation. A `TimeSpan.FromSeconds(1)` timeout is passed to `Regex` constructor as a best practice but validation is purely syntactic.

### ViewModel Activation / Dropdown Loading

```csharp
// ImporterSettingsView.axaml.cs (WhenActivated)
this.WhenActivated(disposables =>
{
    ViewModel!.LoadAsync().Subscribe().DisposeWith(disposables);
});

// ImporterSettingsViewModel.LoadAsync()
public async Task LoadAsync()
{
    IsLoading = true;
    try
    {
        // 1. Load importers
        var importersResult = await _getImportersHandler.HandleAsync(new GetImportersQuery());
        if (importersResult.IsSuccess)
        {
            ImporterItems.Clear();
            foreach (var dto in importersResult.Value)
                ImporterItems.Add(new ImporterItemViewModel(dto));
        }

        // 2. Load taxpayer profile (0 or 1)
        var profileResult = await _getProfileHandler.HandleAsync(new GetTaxpayerProfileQuery());
        AvailableProfiles.Clear();
        if (profileResult.IsSuccess && profileResult.Value is not null)
            AvailableProfiles.Add(profileResult.Value);

        // 3. Load mailboxes
        var mailboxResult = await _getMailboxesHandler.HandleAsync(new GetMailboxesQuery());
        if (mailboxResult.IsSuccess)
        {
            AvailableMailboxes.Clear();
            foreach (var dto in mailboxResult.Value)
                AvailableMailboxes.Add(dto);
        }
    }
    finally
    {
        IsLoading = false;
    }
}
```

### Post-Save State Management

```
After Add success (returns Guid newId):
  → reload importers (LoadImportersAsync)
  → find ImporterItemViewModel where Id == newId
  → SelectedImporter = that item
  → IsEditMode = true

After Update success:
  → remember currentId = SelectedImporter.Id
  → reload importers
  → SelectedImporter = item where Id == currentId

After Delete success:
  → reload importers
  → SelectedImporter = null
  → clear form fields
  → IsEditMode = false
```

### `AddTransient` for All Registrations

Consistent with Features 002–004. Desktop uses a root `ServiceProvider` with no HTTP scope. `AddTransient` is safe and correct for all handlers, repositories, and ViewModels.

### `ReportType` Enum Display

`ReportTypeExtensions.ToDisplayString()` in `Rentier.Desktop/Extensions/` converts enum values to human-readable strings. The `ComboBox` in XAML uses an `ItemTemplate` with a `TextBlock` whose `Text` is bound via a value converter (or inline function via markup) that calls `ToDisplayString()`. Alternatively, an `ObservableCollection<string>` of display strings with index-based sync can be used — the simpler approach for Avalonia without compiled bindings.

---

## Complexity Tracking

> No Constitution Check violations requiring justification.
