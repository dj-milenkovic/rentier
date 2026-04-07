# Tasks: Reports List & Manual Import (014)

**Branch**: `feature/003-reports-manual-import`  
**Input**: `.specify/specs/014-reports-list-manual-import/` — spec.md, plan.md, data-model.md, contracts/application-contracts.md, clarify.md  
**Total tasks**: 36 | **User stories**: 4 | **Phases**: 7

---

## Format: `[ID] [P?] [Story?] Description — file path`

- **[P]**: Parallelisable — operates on a different file with no dependency on incomplete tasks in the same phase
- **[US1–US4]**: Maps to user story from spec.md
- All file paths are relative to repository root
- **No EF migration** — this feature adds no new columns or tables; `ReportId` already exists on `Filings`

---

## Phase 1: Setup

**Purpose**: Branch verification — no schema changes.

- [ ] T001 Verify the working branch is `feature/003-reports-manual-import` (create from master if absent); confirm no pending EF migrations are needed (run `dotnet ef migrations list --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop` and verify the latest migration contains `FilingsTable` or similar — no new migration is required by this feature)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Application contracts, repository interface extensions, DTO and command records, and `GetFilingsQuery` extension. All four user stories depend on this phase.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T002 Add two new method signatures to `IFilingRepository` in `src/Rentier.Application/Repositories/IFilingRepository.cs`: (1) `Task<int> GetFilingCountByReportIdAsync(Guid reportId, CancellationToken ct = default)` — returns count of Filing records linked to reportId, used by `GetReportsQueryHandler` to populate `ReportRowDto.FilingCount`; (2) `Task DeleteByReportIdAsync(Guid reportId, CancellationToken ct = default)` — deletes all filings for a report, called by `DeleteReportCommandHandler` before deleting the parent report; add XML doc comments matching contracts/application-contracts.md

- [ ] T003 [P] Implement `FilingRepository.GetFilingCountByReportIdAsync` in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs`: `return await _db.Filings.AsNoTracking().CountAsync(f => f.ReportId == reportId, ct)` — single expression body, `AsNoTracking()` for read-only count query (depends on T002)

- [ ] T004 [P] Implement `FilingRepository.DeleteByReportIdAsync` in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs`: load-then-remove pattern: `var filings = await _db.Filings.Where(f => f.ReportId == reportId).ToListAsync(ct); if (filings.Count == 0) return; _db.Filings.RemoveRange(filings); await _db.SaveChangesAsync(ct)` — **NEVER use `ExecuteDeleteAsync`** (breaks SQLite in-memory tests); idempotent when no filings exist (depends on T002)
  > ⚠️ **ANALYSIS NOTE [H1]**: `spec.md:138` ("Repository Extensions Required") and `clarify.md §Decision 4` both incorrectly specify `ExecuteDeleteAsync`. Those references are **wrong and must be ignored**. The load-then-remove pattern above is the only correct implementation. `ExecuteDeleteAsync` breaks the SQLite in-memory test suite.

- [ ] T005 [P] Create `ReportRowDto` positional record in `src/Rentier.Application/DTOs/ReportRowDto.cs`: `public sealed record ReportRowDto(Guid Id, string ReportName, DateOnly ImportDate, string ImporterName, ReportStatus Status, int FilingCount)` — namespace `Rentier.Application.DTOs`; using `Rentier.Domain.Enums`

- [ ] T006 [P] Create `GetReportsQuery` record in `src/Rentier.Application/Queries/GetReportsQuery.cs`: `public sealed record GetReportsQuery` — no parameters; namespace `Rentier.Application.Queries`; add XML doc comment: "Returns all Report records as display rows with resolved importer name and filing count. No pagination — all reports returned in a single call."

- [ ] T007 [P] Create `ImportReportCommand` record in `src/Rentier.Application/Commands/ImportReportCommand.cs`: `public sealed record ImportReportCommand(Guid ImporterId, string FileName, byte[] CsvContent)` — namespace `Rentier.Application.Commands`; add XML doc: "Imports a CSV brokerage statement manually. CsvContent is the raw file bytes read by the Desktop layer via Avalonia StorageProvider BEFORE this command is dispatched — the handler never touches the file system."

- [ ] T008 [P] Create `DeleteReportCommand` record in `src/Rentier.Application/Commands/DeleteReportCommand.cs`: `public sealed record DeleteReportCommand(Guid ReportId)` — namespace `Rentier.Application.Commands`; add XML doc: "Deletes a Report and all linked Filings. Cascade deletion is performed at the application layer."

- [ ] T009 [P] Extend `GetFilingsQuery` record in `src/Rentier.Application/Queries/GetFilingsQuery.cs` — add optional `Guid? ReportIdFilter = null` as a fourth positional parameter: `public sealed record GetFilingsQuery(FilingFilterMode Filter, int Page, int PageSize = 20, Guid? ReportIdFilter = null)` — when `ReportIdFilter` is non-null, the handler bypasses pagination and returns only filings for that report; existing callers that pass 3 arguments are unaffected (default null)

**Checkpoint**: Application contracts compile. All four handler types are now definable. Infra implementations are ready. Foundation is complete.

---

## Phase 3: User Story 1 — Browse the Reports List (Priority: P1) 🎯 MVP

**Goal**: Replace the stub `ReportsView` placeholder with a DataGrid showing all reports. Each row shows Report Name, Import Date (yyyy-MM-dd), Importer Name, Status, and Filing Count. The list auto-loads on pane activation via `IActivatableViewModel`. Sync functionality is preserved.

**Independent Test**: Launch the app with seeded report data, navigate to the Reports pane, and verify all rows appear with correct column values. No import or delete required.

### Application — Query handler

- [ ] T010 [US1] Create `GetReportsQueryHandler` in `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs` implementing `IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>`; inject `IReportRepository`, `IImporterRepository`, `IFilingRepository`; algorithm: (1) `var reports = await _reportRepository.GetAllAsync(ct)` — uses existing method; (2) `var importers = await _importerRepository.GetAllAsync(ct)` then build `Dictionary<Guid, string> importerNames` keyed by `importer.Id`, value `importer.DisplayName`; (3) for each report: `importerName = importerNames.GetValueOrDefault(report.ImporterId, "Unknown")`, `filingCount = await _filingRepository.GetFilingCountByReportIdAsync(report.Id, ct)`, yield `new ReportRowDto(report.Id, report.ReportName, report.ImportDate, importerName, report.Status, filingCount)`; (4) return `Result<IReadOnlyList<ReportRowDto>, Error>.Success(rows.AsReadOnly())`; wrap entire body in try/catch → `Result.Failure(new Error("GET_REPORTS_FAILED", ex.Message))` on exception (depends on T002, T003, T005, T006)
  > ⚠️ **ANALYSIS NOTE [H2/M1]**: `spec.md:137` ("Repository Extensions Required") specifies `IReportRepository.GetAllWithFilingCountAsync` as a new method. This is **superseded by `plan.md §R-001`** — do NOT add `GetAllWithFilingCountAsync` to `IReportRepository`. Use the existing `GetAllAsync` + per-report `GetFilingCountByReportIdAsync` approach exactly as described above. `IReportRepository` needs no new methods for this feature.

### Application — Tests

- [ ] T011 [P] [US1] Write `GetReportsQueryHandlerTests` in `tests/Rentier.Application.Tests/GetReportsQueryHandlerTests.cs` using NSubstitute; test methods: `HandleAsync_WithNoReports_ReturnsEmptyList`, `HandleAsync_MapsAllDtoFieldsCorrectly`, `HandleAsync_ResolvesImporterNameFromDictionary`, `HandleAsync_WhenImporterNotFound_UsesUnknownFallback`, `HandleAsync_ReturnsCorrectFilingCountPerReport`, `HandleAsync_WhenRepositoryThrows_ReturnsFailure`; mock `IReportRepository.GetAllAsync`, `IImporterRepository.GetAllAsync`, `IFilingRepository.GetFilingCountByReportIdAsync` using NSubstitute (depends on T010)

### Infrastructure — Tests

- [ ] T012 [P] [US1] Add `GetFilingCountByReportIdAsync` integration test methods to `tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs` using the existing `IAsyncLifetime` in-memory SQLite fixture; methods: `GetFilingCountByReportIdAsync_WhenNoFilingsExist_ReturnsZero`, `GetFilingCountByReportIdAsync_WhenFilingsLinked_ReturnsCorrectCount`, `GetFilingCountByReportIdAsync_WithUnknownReportId_ReturnsZero` (depends on T003)

### Desktop — Strings and converters

- [ ] T013 [P] [US1] Add all **27** new string keys to `src/Rentier.Desktop/Resources/Strings.resx` (check existing keys first to avoid duplicates; add only missing ones): `Reports_Col_Name=Report Name`, `Reports_Col_ImportDate=Import Date`, `Reports_Col_Importer=Importer`, `Reports_Col_Status=Status`, `Reports_Col_FilingCount=Filings`, `Reports_Button_Import=Import…`, `Reports_Button_Sync=Sync Mailboxes`, `Reports_Button_ViewFilings=View Filings`, `Reports_Button_Delete=Delete`, `Reports_Error_Dismiss=Dismiss`, `Reports_Empty=No reports found.`, `Reports_Delete_Confirmation_Title=Delete Report`, `Reports_Delete_Confirmation_Message=This will permanently delete the report and all linked filings. This action cannot be undone.`, `Reports_Delete_Confirm_Button=Delete`, `Reports_Delete_Cancel_Button=Cancel`, `Reports_Import_Title=Import Report`, `Reports_Import_NoImporters=No importers are configured. Please add an importer before importing a report.`, `Reports_Import_FilePickerTitle=Select CSV File`, `Reports_Import_FilePickerFilter=CSV Files`, `Reports_Error_ImportFailed=Import failed. Please check the file format and try again.`, `Reports_Error_DuplicateReport=A report with this name already exists for the selected importer.`, `Reports_Error_InvalidCsv=The selected file is not a valid IBKR CSV export.`, `Reports_Error_DeleteFailed=Failed to delete the report. Please try again.`, `Reports_Error_LoadFailed=Failed to load reports. Please try again.`, `ReportStatus_Init=Init`, `ReportStatus_Processed=Processed`, `ReportStatus_Error=Error`
  > ℹ️ **ANALYSIS NOTE [L1]**: Task originally said "25 new string keys" — corrected to **27** (includes the 3 `ReportStatus_*` keys).

- [ ] T014 [P] [US1] Create `ReportStatusDisplayConverter` in `src/Rentier.Desktop/Converters/ReportStatusDisplayConverter.cs`: `public static readonly IValueConverter Instance = new FuncValueConverter<ReportStatus, string>(s => s switch { ReportStatus.Init => Strings.ReportStatus_Init, ReportStatus.Processed => Strings.ReportStatus_Processed, ReportStatus.Error => Strings.ReportStatus_Error, _ => s.ToString() })` — namespace `Rentier.Desktop.Converters`; using Avalonia `FuncValueConverter<TIn, TOut>` and `Rentier.Desktop.Resources` (depends on T013)

### Desktop — ViewModels

- [ ] T015 [P] [US1] Create `ReportRowViewModel` sealed class in `src/Rentier.Desktop/ViewModels/ReportRowViewModel.cs`: no `ReactiveObject` needed (immutable display model); properties `Guid Id`, `string ReportName`, `DateOnly ImportDate`, `string ImporterName`, `ReportStatus Status`, `int FilingCount` (all get-only, set via private constructor); computed `string ImportDateDisplay => ImportDate.ToString("yyyy-MM-dd")`; static factory `From(ReportRowDto dto)` → `new ReportRowViewModel(dto)`; private constructor maps all DTO fields; namespace `Rentier.Desktop.ViewModels` (depends on T005)

- [ ] T016 [US1] Full rewrite of `ReportsViewModel` in `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` — `public sealed class ReportsViewModel : ReactiveObject, IActivatableViewModel`; constructor parameters: `ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> syncHandler`, `IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>> getReports`, `ICommandHandler<ImportReportCommand, Result<Guid, Error>> importReport`, `ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>> deleteReport`, `Func<string, string, Task<bool>> confirmDelete`, `Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>> showImportDialog`, `Action<Guid> navigateToFilings`, `IScheduler? scheduler = null`; backing fields (all `this.RaiseAndSetIfChanged`): `_isLoading (bool)`, `_isSyncing (bool)`, `_errorMessage (string?)`, `_syncStatusMessage (string?)`, `_syncProgressValue (int)`; public properties: `IsLoading`, `IsSyncing`, `ErrorMessage`, `SyncStatusMessage`, `SyncProgressValue`, `bool IsEmpty => Rows.Count == 0 && !IsLoading`; `ObservableCollection<ReportRowViewModel> Rows`; `public ViewModelActivator Activator { get; } = new()`; commands (all `ReactiveCommand.CreateFromTask`): `LoadReportsCommand` (Unit→Unit), `SyncCommand` (Unit→Unit — preserves existing IMAP sync logic verbatim from current `HandleSyncAsync`), `ImportCommand` (Unit→Unit), `DeleteCommand` (Guid→Unit), `ViewFilingsCommand` (Guid→Unit), `ClearErrorCommand` (`ReactiveCommand.Create(() => ErrorMessage = null)`); `WhenActivated` block: `this.WhenActivated(disposables => { LoadReportsCommand.Execute().Subscribe().DisposeWith(disposables); })`; `LoadReportsAsync`: `IsLoading=true; ErrorMessage=null; try { var r = await _getReports.HandleAsync(new GetReportsQuery(), ct); if (!r.IsSuccess) { ErrorMessage = r.Error.Message; return; } Rows.Clear(); foreach (var dto in r.Value) Rows.Add(ReportRowViewModel.From(dto)); this.RaisePropertyChanged(nameof(IsEmpty)); } finally { IsLoading = false; }`; `ImportAsync`: invoke `showImportDialog()` → if null return; destructure `(ImporterId, FileName, Content)`; `IsLoading=true`; dispatch `ImportReportCommand`; on failure `ErrorMessage = result.Error.Message`; always `await LoadReportsAsync(ct)`; `finally IsLoading=false`; `DeleteAsync(Guid reportId)`: `bool confirmed = await _confirmDelete(Strings.Reports_Delete_Confirmation_Title, Strings.Reports_Delete_Confirmation_Message); if (!confirmed) return; IsLoading=true; var r = await _deleteReport.HandleAsync(new DeleteReportCommand(reportId), ct); if (!r.IsSuccess) ErrorMessage = r.Error.Message; await LoadReportsAsync(ct); finally IsLoading=false`; `ViewFilingsAsync(Guid reportId)`: `_navigateToFilings(reportId)` (synchronous call, no await); `SyncCommand.IsExecuting.Subscribe(v => IsSyncing = v)` in constructor (depends on T005, T006, T007, T008, T013, T014, T015)

### Desktop — View

- [ ] T017 [US1] Replace `src/Rentier.Desktop/Views/ReportsView.axaml` with full DataGrid view: root `<UserControl x:CompileBindings="False" ...>` (**must have `x:CompileBindings="False"`**); declare xmlns `res="using:Rentier.Desktop.Resources"`, `local="using:Rentier.Desktop.Converters"`, `vm="using:Rentier.Desktop.ViewModels"`; outer `DockPanel` or `Grid`; toolbar `StackPanel Orientation="Horizontal"`: `Button Content="{x:Static res:Strings.Reports_Button_Import}" Command="{Binding ImportCommand}" IsEnabled="{Binding !IsLoading}"`, `Button Content="{x:Static res:Strings.Reports_Button_Sync}" Command="{Binding SyncCommand}" IsEnabled="{Binding !IsSyncing}"`, `ProgressBar Value="{Binding SyncProgressValue}" IsVisible="{Binding IsSyncing}"`, `TextBlock Text="{Binding SyncStatusMessage}" IsVisible="{Binding IsSyncing}"`; `ProgressBar IsIndeterminate="True" IsVisible="{Binding IsLoading}"`; error banner `StackPanel IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"`: `TextBlock Text="{Binding ErrorMessage}"`, `Button Content="{x:Static res:Strings.Reports_Error_Dismiss}" Command="{Binding ClearErrorCommand}"`; `DataGrid ItemsSource="{Binding Rows}" AutoGenerateColumns="False" IsReadOnly="True"` columns: (1) `DataGridTextColumn Header="{x:Static res:Strings.Reports_Col_Name}" Binding="{Binding ReportName}"`, (2) `DataGridTextColumn Header="{x:Static res:Strings.Reports_Col_ImportDate}" Binding="{Binding ImportDateDisplay}"`, (3) `DataGridTextColumn Header="{x:Static res:Strings.Reports_Col_Importer}" Binding="{Binding ImporterName}"`, (4) `DataGridTextColumn Header="{x:Static res:Strings.Reports_Col_Status}" Binding="{Binding Status, Converter={x:Static local:ReportStatusDisplayConverter.Instance}}"`, (5) `DataGridTextColumn Header="{x:Static res:Strings.Reports_Col_FilingCount}" Binding="{Binding FilingCount}"`, (6) `DataGridTemplateColumn` CellTemplate with `StackPanel Orientation="Horizontal"` containing `Button Content="{x:Static res:Strings.Reports_Button_ViewFilings}" Command="{Binding DataContext.ViewFilingsCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}" CommandParameter="{Binding Id}"` and `Button Content="{x:Static res:Strings.Reports_Button_Delete}" Command="{Binding DataContext.DeleteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}" CommandParameter="{Binding Id}"`; `TextBlock Text="{x:Static res:Strings.Reports_Empty}" IsVisible="{Binding IsEmpty}"` (depends on T013, T014, T015, T016)
  > ⚠️ **ANALYSIS NOTE [M3]**: `plan.md` structural AXAML snippet (line ~656) uses `Converter={StaticResource ReportStatusDisplayConverter}` — this is **incorrect**. Use `{x:Static local:ReportStatusDisplayConverter.Instance}` as specified in this task (consistent with T014 which defines the static `Instance` field). The `{StaticResource}` approach would require a `<UserControl.Resources>` block and is not how static FuncValueConverter instances are used in this codebase.

- [ ] T018 [P] [US1] Update `src/Rentier.Desktop/Views/ReportsView.axaml.cs` code-behind to `public partial class ReportsView : ReactiveUserControl<ReportsViewModel>`; add using `ReactiveUI`; remove any placeholder binding code; the `ReactiveUserControl<T>` base class handles `WhenActivated` propagation automatically (depends on T016, T017)

**Checkpoint**: Reports pane loads, DataGrid shows all reports with correct columns, Sync button remains functional, list refreshes on pane activation. US1 is fully functional and independently testable.

---

## Phase 4: User Story 2 — Manually Import a CSV Report (Priority: P2)

**Goal**: The Import button opens a file picker (CSV only) and an importer selection dialog; on confirmation the CSV is validated, saved, and the processing pipeline runs. The report then appears in the list with status Processed (or Error).

**Independent Test**: With at least one importer configured, click Import, select a valid IBKR CSV, choose the importer, confirm — verify the report appears with status Processed. Cancel at any step — verify no report created.

### Application — Import command handler

- [ ] T019 [US2] Create `ImportReportCommandHandler` in `src/Rentier.Application/Handlers/ImportReportCommandHandler.cs` implementing `ICommandHandler<ImportReportCommand, Result<Guid, Error>>`; inject `IReportRepository`, `IStatementParser`, `ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>`; wrap entire body in try/catch → `Result.Failure(new Error("IMPORT_FAILED", ex.Message))`; algorithm: (1) `using var stream = new MemoryStream(command.CsvContent); var parseResult = await _statementParser.ParseAsync(stream, ct); if (!parseResult.IsSuccess) return Result<Guid,Error>.Failure(new Error("INVALID_CSV", parseResult.Error.Message))`; (2) `var exists = await _reportRepository.ExistsByImporterAndNameAsync(command.ImporterId, command.FileName, ct); if (exists) return Result<Guid,Error>.Failure(new Error("DUPLICATE_REPORT", $"A report named '{command.FileName}' already exists for this importer."))`; (3) `var report = Report.Create(command.ImporterId, command.FileName, command.CsvContent, mailboxMessageId: null); await _reportRepository.AddAsync(report, ct)`; (4) `var processResult = await _processReportsHandler.HandleAsync(new ProcessReportsCommand(), ct); if (!processResult.IsSuccess) return Result<Guid,Error>.Failure(processResult.Error)`; (5) `return Result<Guid,Error>.Success(report.Id)` — **note**: per plan R-002, validate CSV FIRST (step 1) before duplicate check (step 2) so no record is ever persisted on an invalid file (depends on T007)

### Application — Tests

- [ ] T020 [P] [US2] Write `ImportReportCommandHandlerTests` in `tests/Rentier.Application.Tests/ImportReportCommandHandlerTests.cs` using NSubstitute; test methods: `HandleAsync_WithValidCsvAndNoExistingReport_PersistsReportAndReturnsId`, `HandleAsync_WithValidCsvAndNoExistingReport_TriggersProcessReportsCommand`, `HandleAsync_WhenCsvParseFailsBeforeDuplicateCheck_ReturnsInvalidCsvFailure`, `HandleAsync_WhenCsvParseFailsBeforeDuplicateCheck_DoesNotPersistReport`, `HandleAsync_WhenDuplicateExists_ReturnsDuplicateReportFailure`, `HandleAsync_WhenDuplicateExists_DoesNotPersistReport`, `HandleAsync_WhenProcessingPipelineFails_ReturnsFailure`, `HandleAsync_WhenRepositoryThrows_ReturnsImportFailedError` (depends on T019)

### Desktop — Import dialog delegate

- [ ] T021 [US2] Implement the `showImportDialog` delegate that is registered in `CompositionRoot` as `Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>>`: use Avalonia `TopLevel.GetTopLevel(...)?.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Strings.Reports_Import_FilePickerTitle, FileTypeFilter = [new("CSV") { Patterns = ["*.csv"] }], AllowMultiple = false })` — if user cancels (returns empty list) return `null`; read file bytes via `await file.OpenReadAsync()` + `MemoryStream`; then show importer selection dialog via `ContentDialog` with a `ComboBox` populated by `provider.GetRequiredService<IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>>().HandleAsync(new GetImportersQuery(), CancellationToken.None)`; if no importers, show info dialog (`Strings.Reports_Import_NoImporters`) and return `null`; auto-select first importer when exactly one exists; on Cancel return `null`; on Confirm return `(SelectedImporter.Id, file.Name, bytes)`; register delegate in `CompositionRoot.AddDesktopServices()` using `provider => async () => { ... }` (depends on T013, T016)

**Checkpoint**: Import flow is functional end-to-end. Valid CSV → saved + processed; invalid CSV → rejected before persist; duplicate → rejected with message; cancel → no side effect.

---

## Phase 5: User Story 3 — View Filings for a Report (Priority: P3)

**Goal**: Each report row has a "View Filings" button. Clicking it navigates to the Filings pane filtered to show only that report's filings. The `navigateToFilings` delegate is wired in `MainWindowViewModel`.

**Independent Test**: With a report that has linked filings, click "View Filings" — verify the Filings pane opens showing only that report's filings. With a report with no filings — verify the Filings pane opens showing an empty list.

### Application — GetFilingsQueryHandler extension

- [ ] T022 [US3] Extend `GetFilingsQueryHandler.HandleAsync` in `src/Rentier.Application/Handlers/GetFilingsQueryHandler.cs` — add a branch **before** the existing `GetPagedAsync` call: `if (query.ReportIdFilter.HasValue) { var filings = await _filings.GetByReportIdAsync(query.ReportIdFilter.Value, ct); var rows = filings.Select(f => new FilingRowDto(f.Id, f.Status, f.IncomeType, f.PayingEntity, f.FilingDeadline, f.TaxPayableRsd, f.PaymentReference)).ToList().AsReadOnly(); return Result<FilingsPageResult, Error>.Success(new FilingsPageResult(rows, rows.Count, 1)); }` — existing paginated path is unchanged; `GetByReportIdAsync` already exists on `IFilingRepository` (no new method required) (depends on T009)

- [ ] T023 [P] [US3] Write extension tests for `GetFilingsQueryHandler` with `ReportIdFilter` in `tests/Rentier.Application.Tests/GetFilingsQueryHandlerTests.cs` (extend existing test class): `HandleAsync_WhenReportIdFilterSet_CallsGetByReportIdAsyncInsteadOfGetPagedAsync`, `HandleAsync_WhenReportIdFilterSet_ReturnsAllFilingsAsSinglePage`, `HandleAsync_WhenReportIdFilterSet_ReturnsFilingCountAsTotal`, `HandleAsync_WhenReportIdFilterSetAndNoFilings_ReturnsEmptyPageResult` (depends on T022)

### Desktop — FilingsViewModel extension

- [ ] T024 [US3] Add `ReportIdFilter` property to `FilingsViewModel` in `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`: add backing field `private Guid? _reportIdFilter`; add property `public Guid? ReportIdFilter { get => _reportIdFilter; set { this.RaiseAndSetIfChanged(ref _reportIdFilter, value); _currentPage = 1; this.RaisePropertyChanged(nameof(CurrentPage)); LoadPageCommand.Execute().Subscribe(); } }`; update `LoadPageAsync` to pass the filter: change `new GetFilingsQuery(filter, _currentPage, 20)` to `new GetFilingsQuery(filter, _currentPage, 20, _reportIdFilter)` (depends on T009, T022)

### Desktop — MainWindowViewModel wiring

- [ ] T025 [US3] Update `MainWindowViewModel` constructor in `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs` to wire the `navigateToFilings` delegate and construct `ReportsViewModel` directly:
  (1) **Inject `IServiceProvider`** into `MainWindowViewModel` constructor alongside `FilingsViewModel filingsVm` and `SettingsViewModel settingsVm` — remove the existing `ReportsViewModel reportsVm` constructor parameter (it will no longer be injected from DI).
  (2) **Wire delegate**: `Action<Guid> navigateToFilings = reportId => { filingsVm.ReportIdFilter = reportId; SelectedEntry = NavigationEntries.First(e => e.ViewModel is FilingsViewModel); };`
  (3) **Construct ReportsViewModel**: `var reportsVm = ActivatorUtilities.CreateInstance<ReportsViewModel>(provider, navigateToFilings);` — this resolves all other ReportsViewModel constructor parameters from DI and supplies the delegate as an additional argument.
  (4) Build `NavigationEntries` using the constructed `reportsVm` as before.
  (5) **`NavigationEntry.ViewModel`** is confirmed exposed as `ReactiveObject ViewModel` in `NavigationEntry.cs` — the LINQ `.First(e => e.ViewModel is FilingsViewModel)` pattern works. ✅ (depends on T016, T024)
  > ⚠️ **ANALYSIS NOTE [H3/H4]**: The original T025 text said "DI already resolves both filingsVm and reportsVm" — this is **incorrect**. T029 removes `ReportsViewModel` from DI. `MainWindowViewModel` must construct it manually via `ActivatorUtilities.CreateInstance`. The `IServiceProvider` injection is the correct approach. Do NOT use `provider.GetRequiredService<ReportsViewModel>()` — it will throw because ReportsViewModel is no longer registered. Do NOT restore `services.AddTransient<ReportsViewModel>()`.
  > ⚠️ **ANALYSIS NOTE [M4]**: `clarify.md §Decision 3` hardcodes `SelectedEntry = NavigationEntries[0]` — ignore this; use `NavigationEntries.First(e => e.ViewModel is FilingsViewModel)` as specified above (index-independent and robust).

**Checkpoint**: Clicking "View Filings" on any report row navigates to the Filings pane filtered to that report's filings. Empty filtered list when report has no filings.

---

## Phase 6: User Story 4 — Delete a Report (Priority: P4)

**Goal**: Each report row has a Delete button. Clicking it shows a confirmation dialog warning that linked filings will be permanently deleted. On confirmation, the report and all its filings are removed.

**Independent Test**: With a report that has linked filings, click Delete, confirm — verify the report and its filings are gone. Click Delete and cancel — verify nothing changed.

### Infrastructure — Tests

- [ ] T026 [P] [US4] Add `DeleteByReportIdAsync` integration test methods to `tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs` using the existing `IAsyncLifetime` in-memory SQLite fixture; methods: `DeleteByReportIdAsync_WhenFilingsExist_DeletesAllMatchingFilings`, `DeleteByReportIdAsync_WhenNoFilingsExist_IsIdempotentAndDoesNotThrow`, `DeleteByReportIdAsync_WhenCalledTwice_IsIdempotent`, `DeleteByReportIdAsync_OnlyDeletesFilingsMatchingReportId` (depends on T004)

### Application — Delete command handler

- [ ] T027 [US4] Create `DeleteReportCommandHandler` in `src/Rentier.Application/Handlers/DeleteReportCommandHandler.cs` implementing `ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>`; inject `IReportRepository`, `IFilingRepository`; algorithm wrapped in try/catch: `try { await _filingRepository.DeleteByReportIdAsync(command.ReportId, ct); await _reportRepository.DeleteAsync(command.ReportId, ct); return Result<VoidResult, Error>.Success(VoidResult.Value); } catch (Exception ex) { return Result<VoidResult, Error>.Failure(new Error("DELETE_REPORT_FAILED", ex.Message)); }` — **step order is critical**: filings MUST be deleted before the report to avoid FK violations; both operations are idempotent if the record is not found (depends on T002, T004, T008)

### Application — Tests

- [ ] T028 [P] [US4] Write `DeleteReportCommandHandlerTests` in `tests/Rentier.Application.Tests/DeleteReportCommandHandlerTests.cs` using NSubstitute; test methods: `HandleAsync_WhenReportHasFilings_DeletesFilingsThenReport`, `HandleAsync_WhenReportHasNoFilings_DeletesReportWithoutError`, `HandleAsync_DeletesFilingsBeforeReport` (verify call order using NSubstitute `Received().InOrder`), `HandleAsync_WhenDeleteByReportIdThrows_ReturnsFailureAndDoesNotCallDeleteReport`, `HandleAsync_WhenDeleteReportThrows_ReturnsFailure` (depends on T027)

**Checkpoint**: Deletion with confirmation is functional. Report and all linked filings are removed on confirm; cancellation is a no-op; database errors surface as `ErrorMessage`.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: DI registration, ViewModel tests, smoke test verification, and final integration wiring.

- [ ] T029 Register all new application handlers and delegates in `src/Rentier.Desktop/Composition/CompositionRoot.cs` inside `AddDesktopServices()` — all `AddTransient`; add after existing Filing handler registrations: `services.AddTransient<IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>, GetReportsQueryHandler>()`; `services.AddTransient<ICommandHandler<ImportReportCommand, Result<Guid, Error>>, ImportReportCommandHandler>()`; `services.AddTransient<ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>, DeleteReportCommandHandler>()`; `services.AddTransient<Func<string, string, Task<bool>>>(provider => (title, msg) => ConfirmDialogHelper.ShowAsync(title, msg, Strings.Reports_Delete_Confirm_Button, Strings.Reports_Delete_Cancel_Button))`; register `Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>>` (import dialog delegate — implementation from T021); **remove the existing `services.AddTransient<ReportsViewModel>()` stub** (ReportsViewModel is now constructed manually in MainWindowViewModel via `ActivatorUtilities.CreateInstance` — see T025); add all required using directives (depends on T019, T021, T025, T027)
  > ⚠️ **ANALYSIS NOTE [H3]**: Removing `services.AddTransient<ReportsViewModel>()` requires that `MainWindowViewModel` no longer injects `ReportsViewModel` as a constructor parameter. T025 handles this by injecting `IServiceProvider` and calling `ActivatorUtilities.CreateInstance<ReportsViewModel>(provider, navigateToFilings)` instead. If T029 is executed before T025, the application will fail at startup. Execute T025 first (or in the same commit).

- [ ] T030 [P] Write `ReportsViewModelTests` in `tests/Rentier.Desktop.Tests/ReportsViewModelTests.cs` using NSubstitute and a `TestScheduler`; test methods: `OnActivation_TriggersLoadReportsCommand`, `LoadReports_WhenQuerySucceeds_PopulatesRowsAndClearsError`, `LoadReports_WhenQueryFails_SetsErrorMessageAndLeavesRowsEmpty`, `ImportCommand_WhenDialogCancelled_DoesNotCallImportHandler`, `ImportCommand_WhenHandlerSucceeds_ReloadsList`, `ImportCommand_WhenHandlerFails_SetsErrorMessage`, `DeleteCommand_WhenUserCancels_DoesNotCallDeleteHandler`, `DeleteCommand_WhenUserConfirms_CallsDeleteHandlerAndReloads`, `DeleteCommand_WhenHandlerFails_SetsErrorMessage`, `ViewFilingsCommand_InvokesNavigateToFilingsDelegate`, `SyncCommand_WhenHandlerSucceeds_SetsSyncStatusMessage`, `IsEmpty_WhenRowsEmpty_IsTrue` (depends on T016, T019, T027)

- [ ] T031 [P] Update (or create) `tests/Rentier.Application.Tests/DiRegistrationSmokeTests.cs` to verify the three new report handlers resolve from the DI container without exception: `IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>`, `ICommandHandler<ImportReportCommand, Result<Guid, Error>>`, `ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>`; also verify `Func<string, string, Task<bool>>` (reports confirm delegate) resolves (depends on T029)

**Checkpoint**: All four user stories are complete, all DI registrations resolve, ViewModel tests pass, and the application builds without warnings.

---

## Dependencies (Story Completion Order)

```
Phase 2 (Foundational)
   ├── T002: IFilingRepository interface
   │     ├── T003: FilingRepository.GetFilingCountByReportIdAsync  [feeds T010]
   │     └── T004: FilingRepository.DeleteByReportIdAsync          [feeds T027]
   ├── T005: ReportRowDto          [feeds T010, T015]
   ├── T006: GetReportsQuery       [feeds T010]
   ├── T007: ImportReportCommand   [feeds T019]
   ├── T008: DeleteReportCommand   [feeds T027]
   └── T009: GetFilingsQuery ext.  [feeds T022, T024]
         │
Phase 3 (US1) ← MVP — deliverable after this phase
Phase 4 (US2) ← ImportReportCommandHandler (T019, T020, T021)
Phase 5 (US3) ← GetFilingsQueryHandler ext. (T022), FilingsViewModel.ReportIdFilter (T024), MainWindowViewModel (T025)
Phase 6 (US4) ← FilingRepository infra tests (T026), DeleteReportCommandHandler (T027, T028)
Phase 7 (Polish) ← DI wiring (T029), ViewModel tests (T030), smoke tests (T031)
```

## Parallel Execution Examples

### Phase 2 (Foundational) — after T002, all unblocked:
- T003, T004, T005, T006, T007, T008, T009 can all run in parallel (different files)

### Phase 3 (US1) — after T010 (handler), all unblocked:
- T011 (handler tests), T012 (infra tests), T013 (Strings.resx), T014 (converter), T015 (RowViewModel) can all run in parallel

### Phase 6 (US4):
- T026 (infra tests) and T027 (handler impl) can run in parallel once T004 is done

---

## Implementation Strategy

**MVP scope**: Phase 1 + Phase 2 + Phase 3 (US1) delivers a read-only Reports DataGrid. This alone satisfies immediate business value — users can see all imported reports with correct metadata.

**Incremental delivery**:
1. **Phase 1–3**: Read-only Reports list (MVP)
2. **Phase 4**: Manual CSV import
3. **Phase 5**: "View Filings" cross-pane navigation
4. **Phase 6**: Report deletion with cascade
5. **Phase 7**: Tests + DI polish

**Architecture constraints** (every task must respect these):
- `AddTransient` ONLY in `CompositionRoot.AddDesktopServices()` — `AddScoped` throws at startup
- **No `ExecuteDeleteAsync`** — use `Where(...).ToListAsync() → RemoveRange → SaveChangesAsync`
- `this.RaiseAndSetIfChanged` for all VM properties — no `[Reactive]` / Fody
- `WhenActivated` subscriptions MUST call `.DisposeWith(disposables)`
- `x:CompileBindings="False"` on `ReportsView` root `<UserControl>`
- `Result<T,Error>.Success(v)` / `.Failure(e)` — exact static factory method names
- All dates `DateOnly` end-to-end — no `DateTime` for `ImportDate`
