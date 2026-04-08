# Tasks: Filings List and Management UI (012)

**Branch**: `feature/012-filings-list-management` (off master, after 010-011 merges)  
**Input**: `.specify/specs/012-filings-list-management/` — spec.md, plan.md, data-model.md, contracts/, clarify.md  
**Total tasks**: 44 | **User stories**: 5 | **Phases**: 8

---

## Format: `[ID] [P?] [Story?] Description — file path`

- **[P]**: Parallelisable — different file, no dependency on incomplete tasks in same phase
- **[US1–US5]**: Maps to user story from spec.md
- All file paths are relative to repository root

---

## Phase 1: Setup

**Purpose**: Branch creation and verification.

- [ ] T001 Create branch `feature/012-filings-list-management` off master; verify migration 0008 (`20260407123841_0008_FilingsTable.cs`) is the latest migration in `src/Rentier.Infrastructure/Persistence/Migrations/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain entity changes, application contracts, and infrastructure database support. All five user stories depend on this phase being complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T002 Add `public string? PaymentReference { get; private set; }` property and `SetPaymentReference(string? reference)` method to `Filing` aggregate root in `src/Rentier.Domain/Entities/Filing.cs`; method trims input, stores null when empty after trim, throws `DomainException("PaymentReference must not exceed 200 characters.")` when length > 200

- [ ] T003 [P] Add `SetPaymentReference` test cases to `tests/Rentier.Domain.Tests/FilingTests.cs`: `SetPaymentReference_WithNull_StoresNull`, `SetPaymentReference_WithEmptyString_StoresNull`, `SetPaymentReference_WithWhitespaceOnly_StoresNull`, `SetPaymentReference_WithValidString_TrimsAndStores`, `SetPaymentReference_WithExactly200Characters_Succeeds`, `SetPaymentReference_WithOver200Characters_ThrowsDomainException` (depends on T002)

- [ ] T004 [P] Create `FilingFilterMode` enum in `src/Rentier.Application/Enums/FilingFilterMode.cs`: `Unpaid = 0` (Init and Filed), `All = 1` (all statuses); namespace `Rentier.Application.Enums`

- [ ] T005 [P] Create `FilingRowDto` positional record in `src/Rentier.Application/DTOs/FilingRowDto.cs`: fields `Guid Id`, `FilingStatus Status`, `IncomeType IncomeType`, `string PayingEntity`, `DateOnly FilingDeadline`, `decimal TaxPayable`, `string? PaymentReference`; namespace `Rentier.Application.DTOs`

- [ ] T006 [P] Create `FilingsPageResult` positional record in `src/Rentier.Application/DTOs/FilingsPageResult.cs`: fields `IReadOnlyList<FilingRowDto> Rows`, `int TotalCount`, `int TotalPages`; namespace `Rentier.Application.DTOs`

- [ ] T007 Add `GetPagedAsync` method signature to `IFilingRepository` in `src/Rentier.Application/Repositories/IFilingRepository.cs`: `Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(FilingFilterMode filter, int page, int pageSize, CancellationToken ct = default)` (depends on T004)

- [ ] T008 [P] Add `PaymentReference` EF Core column mapping to `FilingConfiguration.Configure()` in `src/Rentier.Infrastructure/Persistence/Configurations/FilingConfiguration.cs`: `builder.Property(f => f.PaymentReference).IsRequired(false).HasMaxLength(200)` (depends on T002)

- [ ] T009 Implement `FilingRepository.GetPagedAsync` in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs`: `AsNoTracking()`, apply `Where(f => f.Status == FilingStatus.Init || f.Status == FilingStatus.Filed)` when `filter == FilingFilterMode.Unpaid`, count total before paging, `OrderBy(f => f.FilingDeadline).Skip((page-1)*pageSize).Take(pageSize)`, return `(items.AsReadOnly(), totalCount)` (depends on T007, T008)

- [ ] T010 Scaffold EF Core migration `0009_FilingPaymentReference` by running `dotnet ef migrations add 0009_FilingPaymentReference --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop` from repo root; verify generated Up/Down: `AddColumn<string>("PaymentReference", "Filings", nullable: true, maxLength: 200)` / `DropColumn("PaymentReference", "Filings")` (depends on T002, T008)

**Checkpoint**: Domain, DTOs, repository interface and implementation, and migration are ready. User story implementation can begin.

---

## Phase 3: User Story 1 — View All Filings (Priority: P1) 🎯 MVP

**Goal**: Show a paginated DataGrid of all filings sorted by deadline ascending with columns: Status, Income Type, Paying Entity, Filing Deadline (yyyy-MM-dd), Tax Payable (N,NNN.NN RSD), Payment Reference. Loading overlay, error banner, empty-state panel, and Previous/Next pagination with "Page X of Y" indicator are all included.

**Independent Test**: Navigate to Filings screen with seeded data; confirm rows appear in deadline-ascending order with correct column values, pagination controls are visible, loading overlay appears on fetch start, and empty-state panel appears when no data.

### Application — Query handler

- [ ] T011 [US1] Create `GetFilingsQuery` positional record in `src/Rentier.Application/Queries/GetFilingsQuery.cs`: `sealed record GetFilingsQuery(FilingFilterMode Filter, int Page, int PageSize = 20)`; namespace `Rentier.Application.Queries` (depends on T004)

- [ ] T012 [US1] Create `GetFilingsQueryHandler` in `src/Rentier.Application/Handlers/GetFilingsQueryHandler.cs` implementing `IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>`; validate `Page >= 1` (return `Failure("Page must be >= 1.")`) and `PageSize in [1, 100]` (return `Failure("PageSize must be between 1 and 100.")`); call `IFilingRepository.GetPagedAsync(query.Filter, query.Page, query.PageSize, ct)`; project each `Filing` → `FilingRowDto` mapping `TaxPayable = f.TaxPayableRsd`; compute `TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / query.PageSize))`; return `Result<FilingsPageResult, Error>.Success(...)` or wrap exceptions as `Failure` (depends on T005, T006, T007, T011)

### Application — Tests

- [ ] T013 [P] [US1] Write `GetFilingsQueryHandlerTests` in `tests/Rentier.Application.Tests/GetFilingsQueryHandlerTests.cs` using NSubstitute; test methods: `HandleAsync_WithUnpaidFilter_PassesUnpaidToRepository`, `HandleAsync_WithAllFilter_PassesAllToRepository`, `HandleAsync_MapsFilingTaxPayableRsdToTaxPayable`, `HandleAsync_ReturnsRowsSortedByHandlerResult`, `HandleAsync_ComputesCorrectTotalPages`, `HandleAsync_WhenNoResults_ReturnsTotalPagesOfOne`, `HandleAsync_WhenPageLessThan1_ReturnsFailure`, `HandleAsync_WhenPageSizeOutOfRange_ReturnsFailure` (depends on T012)

### Infrastructure — Tests

- [ ] T014 [P] [US1] Add `GetPagedAsync` integration test methods to `tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs` using existing `IAsyncLifetime` in-memory SQLite fixture; methods: `GetPagedAsync_UnpaidFilter_ReturnsOnlyInitAndFiledFilings`, `GetPagedAsync_AllFilter_ReturnsAllFilings`, `GetPagedAsync_ResultsAreSortedByFilingDeadlineAscending`, `GetPagedAsync_Pagination_SkipsAndTakesCorrectly`, `GetPagedAsync_ReturnsTotalCountBeforePaging` (depends on T009, T010)

### Desktop — Strings and converters

- [ ] T015 [P] [US1] Add 25 new keys to `src/Rentier.Desktop/Resources/Strings.resx` (grep existing keys first — `IncomeType_Dividend` and `IncomeType_Interest` may already exist from feature 011): `Filings_Col_Status=Status`, `Filings_Col_IncomeType=Income Type`, `Filings_Col_PayingEntity=Paying Entity`, `Filings_Col_Deadline=Filing Deadline`, `Filings_Col_TaxPayable=Tax Payable`, `Filings_Col_PaymentRef=Payment Reference`, `Filings_Filter_Unpaid=Unpaid`, `Filings_Filter_All=All`, `Filings_Page_Previous=← Previous`, `Filings_Page_Next=Next →`, `Filings_Page_Indicator=Page {0} of {1}`, `Filings_Empty=No filings found.`, `Filings_Delete_Confirmation_Title=Delete Filing`, `Filings_Delete_Confirmation_Message=Are you sure you want to delete this filing? This action cannot be undone.`, `Filings_Delete_Action_Button=Delete` (column button), `Filings_Delete_Confirm_Button=Delete` (dialog primary), `Filings_Delete_Cancel_Button=Cancel`, `Filings_Error_NotFound=Filing not found.`, `Filings_Error_InvalidTransition=Invalid status transition.`, `Filings_Error_PaymentRefTooLong=Payment reference must not exceed 200 characters.`, `Filings_Error_LoadFailed=Failed to load filings. Please try again.`, `Filings_Error_Dismiss=✕`, `FilingStatus_Init=Init`, `FilingStatus_Filed=Filed`, `FilingStatus_Paid=Paid`, `IncomeType_Dividend=Dividend` (skip if exists), `IncomeType_Interest=Interest` (skip if exists)

- [ ] T016 [P] [US1] Create `FilingStatusExtensions` in `src/Rentier.Desktop/Extensions/FilingStatusExtensions.cs` with `ToDisplayString(this FilingStatus s)` returning `Strings.FilingStatus_Init/Filed/Paid`; create `FilingStatusDisplayConverter` in `src/Rentier.Desktop/Converters/FilingStatusDisplayConverter.cs` as `public static readonly IValueConverter Instance = new FuncValueConverter<FilingStatus, string>(s => s.ToDisplayString())` (depends on T015)

- [ ] T017 [P] [US1] Create `IncomeTypeExtensions` in `src/Rentier.Desktop/Extensions/IncomeTypeExtensions.cs` with `ToDisplayString(this IncomeType t)` returning `Strings.IncomeType_Dividend/Interest`; create `IncomeTypeDisplayConverter` in `src/Rentier.Desktop/Converters/IncomeTypeDisplayConverter.cs` as `public static readonly IValueConverter Instance = new FuncValueConverter<IncomeType, string>(t => t.ToDisplayString())` (depends on T015)

- [ ] T018 [P] [US1] Create `InvertBoolConverter` in `src/Rentier.Desktop/Converters/InvertBoolConverter.cs` as `public static readonly IValueConverter Instance = new FuncValueConverter<bool, bool>(b => !b)` using Avalonia `FuncValueConverter<TIn, TOut>`

### Desktop — ViewModels

- [ ] T019 [P] [US1] Create `FilingRowViewModel` sealed class in `src/Rentier.Desktop/ViewModels/FilingRowViewModel.cs`: properties `Guid Id`, `FilingStatus Status`, `IncomeType IncomeType`, `string PayingEntity`, `DateOnly FilingDeadline`, `decimal TaxPayable`, `string? PaymentReference` (all get-only, private set via constructor); computed `string DeadlineDisplay => FilingDeadline.ToString("yyyy-MM-dd")`, `string TaxPayableDisplay => $"{TaxPayable.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)} RSD"` (**must use InvariantCulture** — Serbian locale uses period as thousands separator which would produce wrong format), `bool IsPaymentReferenceEditable => Status == FilingStatus.Filed`, `IReadOnlyList<FilingStatus> AvailableNextStatuses` (Init→[Filed], Filed→[Paid], Paid→[]); static `From(FilingRowDto dto)` factory (depends on T005)

- [ ] T020 [US1] Rewrite `FilingsViewModel` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` inheriting `ReactiveObject, IActivatableViewModel`; constructor injects `IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>> getFilings`, `ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>> updateStatus`, `ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>> updateReference`, `ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>> deleteFiling`, `Func<string, Task<bool>> confirmDelete`, `IScheduler? scheduler = null`; private fields `_filter = FilingFilterMode.Unpaid`, `_currentPage = 1`, `_totalPages = 1`, `_totalCount = 0`, `_isLoading`, `_errorMessage`, `_showAll`; public properties via `this.RaiseAndSetIfChanged`: `IsLoading`, `ErrorMessage`, `ShowAll` (setter resets page to 1 and calls LoadPageCommand; implemented in Phase 4 US2), `CurrentPage`, `TotalPages`, `TotalCount`; computed `string PageIndicator`, `bool IsEmpty`, `bool HasPreviousPage`, `bool HasNextPage`; `ObservableCollection<FilingRowViewModel> Rows`; commands: `LoadPageCommand`, `PreviousPageCommand` (canExecute: `HasPreviousPage && !IsLoading`), `NextPageCommand` (canExecute: `HasNextPage && !IsLoading`), `ClearErrorCommand`; all created with `ReactiveCommand.CreateFromTask`; **`WhenActivated` block**: `this.WhenActivated(disposables => { LoadPageCommand.Execute().Subscribe().DisposeWith(disposables); })` — **all subscriptions inside `WhenActivated` MUST call `.DisposeWith(disposables)`** to prevent accumulation on repeated activations; `LoadPageAsync(CancellationToken ct)`: sets IsLoading=true, ErrorMessage=null, calls handler with `var filter = _showAll ? FilingFilterMode.All : FilingFilterMode.Unpaid` (derive inline, no separate `_filter` field), populates Rows, updates TotalPages/TotalCount, clamps page if needed, sets IsLoading=false in finally; NOTE: AdvanceStatusCommand, SavePaymentRefCommand, DeleteFilingCommand stubs are added in Phases 5-7 (depends on T011, T012, T019)

### Desktop — View

- [ ] T021 [US1] Replace `src/Rentier.Desktop/Views/FilingsView.axaml` with full DataGrid view: `x:CompileBindings="False"` on root UserControl; xmlns for `res` (Rentier.Desktop.Resources), `local` (Rentier.Desktop.Converters), `vm` (Rentier.Desktop.ViewModels); DataGrid `ItemsSource="{Binding Rows}"` `AutoGenerateColumns="False"` `IsReadOnly="True"` (will be updated in later phases); columns: (1) `DataGridTextColumn Header=Filings_Col_Status Binding="{Binding Status, Converter={x:Static local:FilingStatusDisplayConverter.Instance}}"`, (2) `DataGridTextColumn Header=Filings_Col_IncomeType Binding="{Binding IncomeType, Converter={x:Static local:IncomeTypeDisplayConverter.Instance}}"`, (3) `DataGridTextColumn Header=Filings_Col_PayingEntity Binding="{Binding PayingEntity}"`, (4) `DataGridTextColumn Header=Filings_Col_Deadline Binding="{Binding DeadlineDisplay}"`, (5) `DataGridTextColumn Header=Filings_Col_TaxPayable Binding="{Binding TaxPayableDisplay}"`, (6) `DataGridTextColumn Header=Filings_Col_PaymentRef Binding="{Binding PaymentReference}"`; `ProgressBar IsIndeterminate="True" IsVisible="{Binding IsLoading}"`; `TextBlock` for ErrorMessage bound with `StringConverters.IsNotNullOrEmpty`; `TextBlock Text="{x:Static res:Strings.Filings_Empty}" IsVisible="{Binding IsEmpty}"`; pagination bar: `Button Command="{Binding PreviousPageCommand}"`, `TextBlock Text="{Binding PageIndicator}"`, `Button Command="{Binding NextPageCommand}"` (depends on T015, T016, T017, T019)

- [ ] T022 [P] [US1] Update `src/Rentier.Desktop/Views/FilingsView.axaml.cs` code-behind to `public partial class FilingsView : ReactiveUserControl<FilingsViewModel>` with Avalonia `[assembly: XmlnsDefinition]`-compatible pattern; remove `Placeholder` binding dependency (depends on T020, T021)

**Checkpoint**: Filings screen loads and displays a read-only paginated list sorted by deadline. US1 is fully functional and independently testable.

---

## Phase 4: User Story 2 — Filter Unpaid vs All (Priority: P2)

**Goal**: A toggle row above the DataGrid lets the user switch between "Unpaid" (Init + Filed) and "All" (every status). Changing the filter resets to page 1 and reloads.

**Independent Test**: Seed filings of all three statuses; confirm "Unpaid" toggle hides Paid rows, "All" toggle shows them, and the page indicator resets to "Page 1 of …" on each toggle.

- [ ] T023 [US2] Add `ShowAll` property with toggle logic to `FilingsViewModel` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: `ShowAll` setter sets `_filter = ShowAll ? FilingFilterMode.All : FilingFilterMode.Unpaid`, resets `_currentPage = 1`, raises `CurrentPage` changed, then executes `LoadPageCommand.Execute().Subscribe()`

- [ ] T024 [US2] Add filter toggle row to `src/Rentier.Desktop/Views/FilingsView.axaml`: horizontal `StackPanel` above the DataGrid with two `ToggleButton`s — `IsChecked="{Binding ShowAll, Converter={x:Static local:InvertBoolConverter.Instance}}" Content="{x:Static res:Strings.Filings_Filter_Unpaid}"` and `IsChecked="{Binding ShowAll}" Content="{x:Static res:Strings.Filings_Filter_All}"`; use `IsChecked` two-way binding so selecting one deselects the other visually

**Checkpoint**: Filter toggle is functional. Switching between Unpaid and All reloads the list with the correct subset of filings.

---

## Phase 5: User Story 3 — Advance Filing Status (Priority: P2)

**Goal**: Status column becomes an inline ComboBox showing only valid next statuses. Selecting a new value calls `UpdateFilingStatusCommand`; success reloads the page; failure shows an error banner and reverts.

**Independent Test**: For an Init row, confirm the ComboBox shows only [Filed]; select it; verify the row now shows Filed (reload). For a Paid row, confirm the ComboBox shows no items (or is disabled).

### Application — Status command

- [ ] T025 [P] [US3] Create `UpdateFilingStatusCommand` positional record in `src/Rentier.Application/Commands/UpdateFilingStatusCommand.cs`: `sealed record UpdateFilingStatusCommand(Guid FilingId, FilingStatus NewStatus)`; namespace `Rentier.Application.Commands`

- [ ] T026 [US3] Create `UpdateFilingStatusCommandHandler` in `src/Rentier.Application/Handlers/UpdateFilingStatusCommandHandler.cs` implementing `ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>`; `GetByIdAsync(command.FilingId)` → return `Failure("Filing not found.")` if null; `try { filing.AdvanceStatus(command.NewStatus); } catch (DomainException ex) { return Failure(ex.Message); }`; `await UpdateAsync(filing, ct)`; return `Success(VoidResult.Instance)` (depends on T025)

- [ ] T027 [P] [US3] Write `UpdateFilingStatusCommandHandlerTests` in `tests/Rentier.Application.Tests/UpdateFilingStatusCommandHandlerTests.cs` using NSubstitute; test methods: `HandleAsync_ValidInitToFiled_UpdatesStatusAndReturnsSuccess`, `HandleAsync_ValidFiledToPaid_UpdatesStatusAndReturnsSuccess`, `HandleAsync_InvalidTransition_ReturnsFailureWithoutPersisting` (generic), `HandleAsync_InitToPaidTransition_ReturnsFailureWithoutPersisting` (SC-005: explicit Init→Paid rejection — highest-risk skip), `HandleAsync_FiledToInitTransition_ReturnsFailureWithoutPersisting` (SC-005: backward transition rejected), `HandleAsync_FilingNotFound_ReturnsFailure` (depends on T026)

### Desktop — Status command

- [ ] T028 [US3] Add `AdvanceStatusCommand` to `FilingsViewModel` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: `ReactiveCommand<(Guid Id, FilingStatus NewStatus), Unit>` created with `ReactiveCommand.CreateFromTask<(Guid, FilingStatus), Unit>`; handler: set `IsLoading=true`, call `_updateStatus.HandleAsync(new UpdateFilingStatusCommand(id, newStatus), ct)`, on failure set `ErrorMessage`, reload via `LoadPageAsync(ct)`, finally `IsLoading=false`; canExecute: `this.WhenAnyValue(x => x.IsLoading).Select(loading => !loading)` (depends on T026)

- [ ] T029 [US3] Replace Status `DataGridTextColumn` with `DataGridTemplateColumn` in `src/Rentier.Desktop/Views/FilingsView.axaml`; CellTemplate contains a `ComboBox ItemsSource="{Binding AvailableNextStatuses}" SelectedItem="{Binding Status, Mode=OneTime}" ItemTemplate` displaying items via `FilingStatusDisplayConverter`; add `StatusComboBox_SelectionChanged` event handler in `src/Rentier.Desktop/Views/FilingsView.axaml.cs` that reads `e.AddedItems[0]` as `FilingStatus`, gets the row's `FilingRowViewModel` via `((ComboBox)sender).DataContext`, invokes `ViewModel!.AdvanceStatusCommand.Execute((row.Id, newStatus)).Subscribe()` (depends on T019, T028)

**Checkpoint**: Status advancement is functional. Init→Filed and Filed→Paid transitions succeed; invalid transitions show an error message.

---

## Phase 6: User Story 4 — Enter Payment Reference (Priority: P3)

**Goal**: The Payment Reference column is an editable TextBox for Filed rows only (read-only for Init and Paid). Committing text via LostFocus saves the value; over-200-char input is rejected with an error message.

**Independent Test**: For a Filed row, click the Payment Reference cell and type text; tab away; confirm the value persists (reload). For an Init row, confirm the TextBox is read-only.

### Application — Payment reference command

- [ ] T030 [P] [US4] Create `UpdatePaymentReferenceCommand` positional record in `src/Rentier.Application/Commands/UpdatePaymentReferenceCommand.cs`: `sealed record UpdatePaymentReferenceCommand(Guid FilingId, string? PaymentReference)`; namespace `Rentier.Application.Commands`

- [ ] T031 [US4] Create `UpdatePaymentReferenceCommandHandler` in `src/Rentier.Application/Handlers/UpdatePaymentReferenceCommandHandler.cs` implementing `ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>`; `GetByIdAsync(command.FilingId)` → return `Failure("Filing not found.")` if null; `try { filing.SetPaymentReference(command.PaymentReference); } catch (DomainException ex) { return Failure(ex.Message); }`; `await UpdateAsync(filing, ct)`; return `Success(VoidResult.Instance)` (depends on T030)

- [ ] T032 [P] [US4] Write `UpdatePaymentReferenceCommandHandlerTests` in `tests/Rentier.Application.Tests/UpdatePaymentReferenceCommandHandlerTests.cs` using NSubstitute; test methods: `HandleAsync_WithValidReference_PersistsValueAndReturnsSuccess`, `HandleAsync_WithNullReference_ClearsValueAndReturnsSuccess`, `HandleAsync_WithOver200CharReference_ReturnsFailureWithoutPersisting`, `HandleAsync_FilingNotFound_ReturnsFailure` (depends on T031)

### Desktop — Payment reference command

- [ ] T033 [US4] Add `SavePaymentRefCommand` to `FilingsViewModel` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: `ReactiveCommand<(Guid Id, string? Reference), Unit>` created with `ReactiveCommand.CreateFromTask`; handler: set `IsLoading=true`, call `_updateReference.HandleAsync(new UpdatePaymentReferenceCommand(id, reference), ct)`, on failure set `ErrorMessage`, reload via `LoadPageAsync(ct)`, finally `IsLoading=false` (depends on T031)

- [ ] T034 [US4] Replace Payment Reference `DataGridTextColumn` with `DataGridTemplateColumn` in `src/Rentier.Desktop/Views/FilingsView.axaml`; CellTemplate contains `TextBox Text="{Binding PaymentReference}" IsReadOnly="{Binding IsPaymentReferenceEditable, Converter={x:Static local:InvertBoolConverter.Instance}}"`; add `PaymentRef_LostFocus` event handler in `src/Rentier.Desktop/Views/FilingsView.axaml.cs`: reads the `TextBox.Text`, gets `FilingRowViewModel` from DataContext, invokes `ViewModel!.SavePaymentRefCommand.Execute((row.Id, text)).Subscribe()` (depends on T019, T033)

**Checkpoint**: Payment reference entry is functional. Filed rows accept input; Init and Paid rows are read-only; over-200-char entries are rejected with an error message.

---

## Phase 7: User Story 5 — Delete a Filing (Priority: P3)

**Goal**: Each row has a Delete button. Clicking it shows a ContentDialog asking for confirmation. If the user confirms, the filing is deleted and the list reloads. Cancellation is a no-op.

**Independent Test**: Click Delete on a row; confirm the dialog appears; confirm deletion; verify the row is gone from the list. Click Delete again and cancel; verify the row remains.

### Application — Delete command

- [ ] T035 [P] [US5] Create `DeleteFilingCommand` positional record in `src/Rentier.Application/Commands/DeleteFilingCommand.cs`: `sealed record DeleteFilingCommand(Guid FilingId)`; namespace `Rentier.Application.Commands`

- [ ] T036 [US5] Create `DeleteFilingCommandHandler` in `src/Rentier.Application/Handlers/DeleteFilingCommandHandler.cs` implementing `ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>`; call `IFilingRepository.DeleteAsync(command.FilingId, ct)` (already implemented, no-op if not found); return `Success(VoidResult.Instance)` on success; wrap any unexpected exception as `Failure` (depends on T035)

- [ ] T037 [P] [US5] Write `DeleteFilingCommandHandlerTests` in `tests/Rentier.Application.Tests/DeleteFilingCommandHandlerTests.cs` using NSubstitute; test methods: `HandleAsync_WithExistingFilingId_CallsDeleteAsyncAndReturnsSuccess`, `HandleAsync_WhenFilingNotFound_IsIdempotentAndReturnsSuccess` (depends on T036)

### Infrastructure — Delete tests

- [ ] T038 [P] [US5] Add `DeleteAsync` integration test methods to `tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs` using the existing `IAsyncLifetime` in-memory SQLite fixture; methods: `DeleteAsync_WithExistingId_RemovesFilingFromDatabase`, `DeleteAsync_WithNonExistentId_IsIdempotentAndDoesNotThrow`

### Desktop — Delete command

- [ ] T039 [US5] Add `DeleteFilingCommand` reactive command and confirm-delegate wiring to `FilingsViewModel` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: `ReactiveCommand<Guid, Unit>` created with `ReactiveCommand.CreateFromTask<Guid, Unit>`; handler: `bool confirmed = await _confirmDelete(Strings.Filings_Delete_Confirmation_Message)`; return early without calling handler if `!confirmed`; set `IsLoading=true`; call `_deleteFiling.HandleAsync(new DeleteFilingCommand(id), ct)`; on failure set `ErrorMessage`; reload via `LoadPageAsync(ct)`; decrement `_currentPage` if `Rows.Count == 0 && _currentPage > 1` before reload; finally `IsLoading=false` (depends on T036)

- [ ] T040 [US5] Add Delete button `DataGridTemplateColumn` to `src/Rentier.Desktop/Views/FilingsView.axaml`: CellTemplate contains `Button Content="{x:Static res:Strings.Filings_Delete_Confirm_Button}" Command="{Binding DataContext.DeleteFilingCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}" CommandParameter="{Binding Id}"`; column has no header (depends on T039)

**Checkpoint**: Deletion with confirmation is functional. Confirming removes the row; cancelling leaves it unchanged; database errors surface as an error message.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: DI wiring, ViewModel tests, and final integration verification.

- [ ] T041 Register the four new application handlers with `AddTransient` in `src/Rentier.Desktop/Composition/CompositionRoot.cs` inside `AddDesktopServices()` — **NOT** in `InfrastructureServiceExtensions.cs` (which is reserved for infrastructure-heavy handlers). This matches every comparable CRUD handler in the codebase (SaveTaxpayerProfile, mailbox, importer, holiday handlers are all in CompositionRoot): `services.AddTransient<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>, GetFilingsQueryHandler>()`; `services.AddTransient<ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>, UpdateFilingStatusCommandHandler>()`; `services.AddTransient<ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>, UpdatePaymentReferenceCommandHandler>()`; `services.AddTransient<ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>, DeleteFilingCommandHandler>()`; add required using directives (depends on T012, T026, T031, T036)

- [ ] T042 Register `Func<string, Task<bool>>` `confirmDelete` delegate and update `FilingsViewModel` registration in `src/Rentier.Desktop/Composition/CompositionRoot.cs`; **must add explicit `services.AddTransient<Func<string, Task<bool>>>(provider => async msg => { ... })`** — without this explicit registration, `FilingsViewModel` constructor injection will fail at runtime; delegate implementation: `async msg => { var dialog = new ContentDialog { Title = Strings.Filings_Delete_Confirmation_Title, Content = msg, PrimaryButtonText = Strings.Filings_Delete_Confirm_Button, CloseButtonText = Strings.Filings_Delete_Cancel_Button }; var result = await dialog.ShowAsync(); return result == ContentDialogResult.Primary; }` — use the existing ContentDialog show pattern consistent with other dialogs in the codebase; `FilingsViewModel` is registered as `AddTransient<FilingsViewModel>()`; verify DI resolves correctly (depends on T039, T041)

- [ ] T043 Write `FilingsViewModelTests` in `tests/Rentier.Desktop.Tests/FilingsViewModelTests.cs` using NSubstitute and `new TestScheduler()`; test methods: `OnActivation_TriggersLoadPageWithDefaultUnpaidFilter`, `LoadPage_WhenQuerySucceeds_PopulatesRowsAndClearsError`, `LoadPage_WhenQueryFails_SetsErrorMessageAndLeavesRowsEmpty`, `AdvanceStatusCommand_WhenHandlerSucceeds_ReloadsPage`, `AdvanceStatusCommand_WhenHandlerFails_SetsErrorMessage`, `SavePaymentRefCommand_WhenHandlerSucceeds_ReloadsPage`, `DeleteFilingCommand_WhenUserConfirms_CallsDeleteHandlerAndReloads`, `DeleteFilingCommand_WhenUserCancels_DoesNotCallDeleteHandler`, `ShowAll_WhenSetToTrue_ResetsPageToOneAndReloads` (depends on T020, T023, T028, T033, T039)

- [ ] T044 Update `tests/Rentier.Application.Tests/DiRegistrationSmokeTests.cs` to verify the four new handlers resolve from the DI container; confirm `IQueryHandler<GetFilingsQuery, ...>`, `ICommandHandler<UpdateFilingStatusCommand, ...>`, `ICommandHandler<UpdatePaymentReferenceCommand, ...>`, `ICommandHandler<DeleteFilingCommand, ...>` all resolve without exception (depends on T041, T042)

**Checkpoint**: All 44 tasks complete. Run full test suite (`dotnet test`) and verify zero failures. Build both Debug and Release. Manually navigate to Filings screen to validate end-to-end flow.

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)        → no dependencies
Phase 2 (Foundational) → Phase 1
Phase 3 (US1)          → Phase 2 complete (all T002-T010 done)
Phase 4 (US2)          → Phase 3 (LoadPageCommand + FilingsView base must exist)
Phase 5 (US3)          → Phase 2 (DTOs, repo interface)
Phase 6 (US4)          → Phase 2 (Domain SetPaymentReference; DTOs)
Phase 7 (US5)          → Phase 2 (repo interface; DeleteAsync already in IFilingRepository)
Phase 8 (Polish)       → Phases 3–7 complete
```

### User Story Cross-Dependencies

- **US1 (P1)**: Depends on Phase 2. Entry point for all other stories.
- **US2 (P2)**: Depends on US1 FilingsViewModel and FilingsView being in place.
- **US3 (P2)**: Depends on Phase 2 (DTOs) and US1 FilingsViewModel (needs reload pattern). Can start in parallel with US2.
- **US4 (P3)**: Depends on Phase 2 (Domain SetPaymentReference). Can start in parallel with US3.
- **US5 (P3)**: Depends on Phase 2 (DeleteAsync already in IFilingRepository). Can start in parallel with US3/US4.

### Within Each Phase — Ordering

- Tasks NOT marked [P] must complete before the next task in the same group starts.
- Tasks marked [P] can start as soon as their direct dependencies (noted in parentheses) are satisfied.

### Parallel Opportunities

**Phase 2** (after T002 completes): T003, T004, T005, T006, T008 can all run simultaneously.  
**Phase 3** (after T012 completes): T013, T014 can run in parallel; T015, T016, T017, T018, T019 can run in parallel with each other and with T013/T014.  
**Phase 5 + 6 + 7** (after Phase 3 completes): T025/T030/T035 (the three new command records) can all be created in parallel.

---

## Parallel Execution Examples

### Phase 2 parallel group (after T002)

```
Simultaneous:
  T003 — FilingTests.SetPaymentReference tests
  T004 — FilingFilterMode enum
  T005 — FilingRowDto record
  T006 — FilingsPageResult record
  T008 — FilingConfiguration PaymentReference mapping

Then sequential:
  T007 — IFilingRepository.GetPagedAsync (needs T004)
  T009 — FilingRepository.GetPagedAsync impl (needs T007, T008)
  T010 — EF migration scaffold (needs T002, T008)
```

### Phase 3 parallel group (after T012)

```
Simultaneous tests:
  T013 — GetFilingsQueryHandlerTests
  T014 — FilingRepositoryTests.GetPagedAsync

Simultaneous desktop setup:
  T015 — Strings.resx keys
  T016 — FilingStatusExtensions + Converter  (needs T015)
  T017 — IncomeTypeExtensions + Converter    (needs T015)
  T018 — InvertBoolConverter
  T019 — FilingRowViewModel                  (needs T005)
```

---

## Implementation Strategy

### MVP: User Story 1 Only

1. Complete Phase 1 (Setup)
2. Complete Phase 2 (Foundational — CRITICAL)
3. Complete Phase 3 (US1) — **STOP AND VALIDATE**
4. Confirm: Filings screen loads, DataGrid shows rows, pagination works, error/empty states render
5. Merge or demo MVP

### Incremental Delivery

1. Foundation (Phase 1–2) → US1 (Phase 3) → **Demo read-only list**
2. Add US2 (Phase 4) → **Demo filter toggle**
3. Add US3 (Phase 5) → **Demo status updates**
4. Add US4 (Phase 6) → **Demo payment reference entry**
5. Add US5 (Phase 7) → **Demo delete with confirmation**
6. Polish (Phase 8) → **Full DI wiring + tests + merge**

---

## Architecture Compliance Checklist

Every task must satisfy these rules from the project constitution:

- [ ] **Clean Architecture**: Desktop references only `IQueryHandler`/`ICommandHandler` interfaces — never repositories or EF types directly
- [ ] **Decimal for money**: `TaxPayable` is `decimal` throughout; `TaxPayableDisplay` uses `":N2"` format string; no `float`/`double`
- [ ] **DateOnly for dates**: `FilingDeadline` is `DateOnly` throughout; `DeadlineDisplay` uses `"yyyy-MM-dd"` format string; no `DateTime`
- [ ] **AddTransient only**: All handler and repository registrations use `AddTransient` — never `AddScoped` or `AddSingleton`
- [ ] **EF delete pattern**: `DeleteAsync` uses `FindAsync + Remove + SaveChangesAsync` — not `ExecuteDeleteAsync`
- [ ] **ReactiveCommand.CreateFromTask**: All async VM commands use this factory — no `.Subscribe()` on hot paths
- [ ] **RaiseAndSetIfChanged**: All VM backing fields use `this.RaiseAndSetIfChanged` — no Fody
- [ ] **x:CompileBindings="False"**: FilingsView.axaml root UserControl includes this attribute
- [ ] **Strings.resx**: All user-visible strings (column headers, buttons, errors, status labels, filter labels) sourced from `Strings` class — no hardcoded literals in XAML or VM code
- [ ] **Tests**: xUnit + FluentAssertions + NSubstitute; infrastructure tests use `SqliteConnection(":memory:")` via `IAsyncLifetime`

---

## Summary

| Phase | Stories | Tasks | Key Deliverable |
|-------|---------|-------|-----------------|
| 1: Setup | — | 1 | Feature branch |
| 2: Foundational | — | 9 | Domain, DTOs, Repo, Migration |
| 3: US1 (P1) 🎯 | US1 | 12 | Read-only paginated filings list |
| 4: US2 (P2) | US2 | 2 | Filter toggle (Unpaid / All) |
| 5: US3 (P2) | US3 | 5 | Inline status advancement ComboBox |
| 6: US4 (P3) | US4 | 4 | Payment reference inline TextBox |
| 7: US5 (P3) | US5 | 6 | Delete with ContentDialog confirmation |
| 8: Polish | — | 4 | DI wiring, ViewModel tests, smoke test |
| **Total** | | **44** | |

> **Suggested MVP scope**: Phases 1–3 (US1 only). The filing list with pagination, loading/error states, and deadline-sorted display is independently valuable and testable.
