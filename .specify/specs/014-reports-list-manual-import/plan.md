# Implementation Plan: Reports List & Manual Import

**Branch**: `feature/003-reports-manual-import` | **Date**: 2026-04-07 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `.specify/specs/014-reports-list-manual-import/spec.md`

---

## Summary

Replace the stub `ReportsViewModel` / `ReportsView` placeholder with a fully functional
Reports DataGrid that displays all imported reports, supports manual CSV import, cross-pane
navigation to linked filings, and cascade deletion. The feature spans all four Clean-Architecture
layers:

- **Domain**: No new domain entities or value objects. `Report.Create(...)` factory and
  `Report.SetStatus(...)` method are used as-is.
- **Application**: Introduce `ReportRowDto`, `GetReportsQuery`, `ImportReportCommand`,
  `DeleteReportCommand`, and their three handlers. Extend `IFilingRepository` with
  `GetFilingCountByReportIdAsync` and `DeleteByReportIdAsync`. Extend `GetFilingsQuery` with
  `Guid? ReportIdFilter` parameter.
- **Infrastructure**: Add `FilingRepository.GetFilingCountByReportIdAsync` and
  `FilingRepository.DeleteByReportIdAsync`. No EF migration required — no schema changes.
- **Desktop**: Full rewrite of `ReportsViewModel` (IActivatableViewModel + three commands).
  New `ReportRowViewModel` per-row state holder. `ReportsView.axaml` replaced with a DataGrid.
  `FilingsViewModel` gains `Guid? ReportIdFilter`. `MainWindowViewModel` wires navigation
  delegate. All strings in `Strings.resx`. `CompositionRoot` updated with three new handler
  registrations.

---

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11, ReactiveUI, EF Core 8 (SQLite), Microsoft.Extensions.DependencyInjection  
**Storage**: SQLite via EF Core; **no new migration** — this feature adds no new columns or tables  
**Testing**: xUnit + FluentAssertions + NSubstitute; SQLite in-memory for infrastructure tests  
**Target Platform**: Windows + macOS desktop (cross-platform Avalonia)  
**Performance Goals**: Reports list load ≤ 2 s for ≤ 500 reports (SC-001); import pipeline ≤ 30 s for a typical IBKR CSV (SC-002); delete ≤ 3 s (SC-005)  
**Constraints**:
- `AddTransient` ONLY in `CompositionRoot.AddDesktopServices()` — Desktop uses root `ServiceProvider`; `AddScoped` throws at startup
- No `ExecuteDeleteAsync` — breaks SQLite in-memory tests; use `FindAsync + RemoveRange + SaveChangesAsync`
- No `[Reactive]` / Fody — manual `this.RaiseAndSetIfChanged(ref _field, value)` throughout
- All `WhenActivated` subscriptions MUST call `.DisposeWith(disposables)`
- `x:CompileBindings="False"` on `ReportsView` root element
- All dates `DateOnly`, all money `decimal`
- `Result<T,Error>.Success(v)` / `Result<T,Error>.Failure(e)` — exact method names

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- [x] **Clean Architecture boundary preserved**: `ReportsViewModel` injects `IQueryHandler` /
  `ICommandHandler` interfaces only — no direct repository or infrastructure references.
  Navigation delegate wired in `MainWindowViewModel` (Desktop composition) not in the Application
  layer.
- [x] **Monetary values as `decimal`**: No monetary values are involved in the Reports entity or
  `ReportRowDto`. Filing monetary values flow through the existing `FilingRowDto` path — unchanged.
- [x] **Business dates as `DateOnly`**: `Report.ImportDate` is `DateOnly` in the domain entity,
  `ReportRowDto`, and ViewModel display binding. `DateOnly.FromDateTime(DateTime.UtcNow)` is
  called inside `Report.Create(...)` — unchanged.
- [x] **Security/privacy constraints**: All data is local (SQLite). Raw CSV bytes stored in
  `Report.AttachmentContent` (local only). No credentials or PII transmitted externally. File
  picker is handled by Avalonia's `StorageProvider` API — no custom file I/O code in the handler.
- [x] **No unapproved network usage**: This feature makes zero outbound calls. IMAP sync
  (`SyncCommand`) is an existing, preserved feature — not modified.
- [x] **All I/O async**: File picker invoked via `await StorageProvider.OpenFilePickerAsync(...)`.
  All repository calls and `ProcessReportsCommand` are awaited. `ReactiveCommand.CreateFromTask`
  used for all ViewModel commands.
- [x] **Test coverage defined**: Application unit tests for all three handlers (success + failure
  paths). Infrastructure integration tests for `GetFilingCountByReportIdAsync` and
  `DeleteByReportIdAsync`. Desktop ViewModel tests for `LoadReportsCommand`, `ImportCommand`,
  `DeleteCommand`, and `ViewFilingsCommand` state changes using NSubstitute mocks.
- [x] **Mapped to approved spec task**: Feature `014-reports-list-manual-import` tracked in
  `.specify/`.

**Post-design re-check**: No violations identified. All five clarification decisions are
consistent with the constitution. `ExecuteDeleteAsync` explicitly excluded; no scoped DI
lifetimes; no `[Reactive]` attributes.

---

## Project Structure

### Documentation (this feature)

```text
.specify/specs/014-reports-list-manual-import/
├── plan.md              ← this file
├── data-model.md        ← Phase 1 output (see sibling file)
├── contracts/
│   └── application-contracts.md  ← Phase 1 output
└── tasks.md             ← Phase 2 output (generated by /speckit.tasks)
```

### Source Code (affected paths)

```text
src/
├── Rentier.Domain/
│   └── Entities/
│       └── Report.cs                              ← NO CHANGE (used as-is)
│
├── Rentier.Application/
│   ├── DTOs/
│   │   └── ReportRowDto.cs                        ← NEW
│   ├── Queries/
│   │   ├── GetReportsQuery.cs                     ← NEW
│   │   └── GetFilingsQuery.cs                     ← EXTEND (add Guid? ReportIdFilter)
│   ├── Commands/
│   │   ├── ImportReportCommand.cs                 ← NEW
│   │   └── DeleteReportCommand.cs                 ← NEW
│   ├── Handlers/
│   │   ├── GetReportsQueryHandler.cs              ← NEW
│   │   ├── ImportReportCommandHandler.cs          ← NEW
│   │   ├── DeleteReportCommandHandler.cs          ← NEW
│   │   └── GetFilingsQueryHandler.cs              ← EXTEND (handle ReportIdFilter branch)
│   └── Repositories/
│       └── IFilingRepository.cs                   ← EXTEND (add 2 new methods)
│
├── Rentier.Infrastructure/
│   └── Repositories/
│       └── FilingRepository.cs                    ← EXTEND (implement 2 new methods)
│
└── Rentier.Desktop/
    ├── ViewModels/
    │   ├── ReportsViewModel.cs                    ← FULL REWRITE
    │   ├── ReportRowViewModel.cs                  ← NEW
    │   ├── FilingsViewModel.cs                    ← EXTEND (add ReportIdFilter property)
    │   └── MainWindowViewModel.cs                 ← EXTEND (wire navigateToFilings delegate)
    ├── Views/
    │   ├── ReportsView.axaml                      ← FULL REPLACEMENT
    │   └── ReportsView.axaml.cs                   ← UPDATE (WhenActivated hook)
    ├── Resources/
    │   └── Strings.resx                           ← EXTEND (new string keys)
    └── Composition/
        └── CompositionRoot.cs                     ← EXTEND (3 new handler registrations)

tests/
├── Rentier.Application.Tests/
│   ├── GetReportsQueryHandlerTests.cs             ← NEW
│   ├── ImportReportCommandHandlerTests.cs         ← NEW
│   └── DeleteReportCommandHandlerTests.cs         ← NEW
└── Rentier.Infrastructure.Tests/
    └── FilingRepositoryTests.cs                   ← EXTEND (filing-count + delete-by-report tests)
```

---

## Phase 0: Research

> All NEEDS CLARIFICATION items were resolved in `clarify.md` prior to planning. No external
> tooling decisions are blocked. Findings are consolidated here for traceability.

### R-001 — DTO shape & filing count strategy (clarify.md §Decision 1)

**Decision**: `ReportRowDto(Guid Id, string ReportName, DateOnly ImportDate, string ImporterName,
ReportStatus Status, int FilingCount)`.  
**Mechanism**: `GetReportsQueryHandler` calls the existing `IReportRepository.GetAllAsync()` (no
new method needed on `IReportRepository`), then `IImporterRepository.GetAllAsync()` once to build
a `Dictionary<Guid, string>` keyed by importer Id for name resolution, then calls
`IFilingRepository.GetFilingCountByReportIdAsync(report.Id, ct)` per report. This avoids loading
full Filing entities for a count-only need while staying within the existing repository contract.  
**Alternatives considered**: (a) `GetAllWithFilingCountAsync` on `IReportRepository` using a
single EF GroupJoin — more efficient at scale but adds complexity; deferred to future
optimisation if SC-001 is not met. (b) N+1 via `GetByReportIdAsync(...).Count` — rejected as it
loads entities when only a count is needed.

### R-002 — `ImportReportCommand` file I/O boundary (clarify.md §Decision 2)

**Decision**: Desktop layer reads raw bytes via Avalonia 11
`await window.StorageProvider.OpenFilePickerAsync(...)` before constructing the command. The
handler receives `byte[] CsvContent` and never touches the file system.  
**Rationale**: Keeps the Application layer infrastructure-agnostic; handler remains testable with
in-memory byte arrays.

### R-003 — "View Filings" navigation mechanism (clarify.md §Decision 3)

**Decision**: Callback delegate pattern — `Action<Guid> navigateToFilings` injected into
`ReportsViewModel`'s constructor. `MainWindowViewModel` wires the delegate inline.  
**Rationale**: Consistent with existing `MainWindowViewModel` navigation pattern. Avoids
introducing a new `INavigationService` abstraction that is not used elsewhere in the codebase.

### R-004 — Delete cascade strategy (clarify.md §Decision 4)

**Decision**: Application-layer two-step delete via `DeleteReportCommandHandler`.  
**Critical constraint**: `IFilingRepository.DeleteByReportIdAsync` MUST use
`_db.Filings.Where(f => f.ReportId == reportId).ToListAsync()` → `RemoveRange` →
`SaveChangesAsync`. NEVER `ExecuteDeleteAsync` — this breaks SQLite in-memory tests used in the
infrastructure test suite.  
**Rationale**: Explicit deletion order (filings before report) avoids FK constraint issues.
Consistent with existing `DeleteAsync` implementations in the repository layer. Surfaceable errors
via `Result.Failure(...)`.

### R-005 — `ReportsViewModel` activation pattern (clarify.md §Decision 5)

**Decision**: `ReportsViewModel` implements `IActivatableViewModel`. `WhenActivated` subscribes
`LoadReportsCommand.Execute()` and calls `.DisposeWith(disposables)` — identical to
`FilingsViewModel` pattern already in use.

### R-006 — `GetFilingsQuery` extension for filtered navigation

**Decision**: Add `Guid? ReportIdFilter = null` to `GetFilingsQuery`. When set,
`GetFilingsQueryHandler` calls `IFilingRepository.GetByReportIdAsync(ReportIdFilter.Value, ct)`
instead of `GetPagedAsync`, wrapping the result in a single-page `FilingsPageResult`.  
**Rationale**: Minimal surface change — existing handler gains one branch; no new interface method
required; `GetByReportIdAsync` already exists on `IFilingRepository`.

---

## Phase 1: Design

### 1.1 Domain Layer

No changes to Domain entities, value objects, or services. `Report.Create(...)`,
`Report.SetStatus(...)`, and `ReportStatus` enum are used as-is.

---

### 1.2 Application Layer

#### New DTO: `ReportRowDto`

```csharp
// Rentier.Application/DTOs/ReportRowDto.cs
using Rentier.Domain.Enums;

namespace Rentier.Application.DTOs;

public sealed record ReportRowDto(
    Guid         Id,
    string       ReportName,
    DateOnly     ImportDate,
    string       ImporterName,
    ReportStatus Status,
    int          FilingCount);
```

#### New query: `GetReportsQuery`

```csharp
// Rentier.Application/Queries/GetReportsQuery.cs
namespace Rentier.Application.Queries;

/// <summary>Returns all reports as display rows with resolved importer name and filing count.</summary>
public sealed record GetReportsQuery;
```

#### `GetReportsQueryHandler`

```csharp
// Rentier.Application/Handlers/GetReportsQueryHandler.cs
// Implements IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>
//
// Algorithm:
// 1. await _reportRepository.GetAllAsync(ct)
//    → returns IReadOnlyList<Report>
// 2. await _importerRepository.GetAllAsync(ct)
//    → build Dictionary<Guid, string> importerNames (Id → DisplayName)
// 3. For each report:
//    a. importerName = importerNames.GetValueOrDefault(report.ImporterId, "Unknown")
//    b. filingCount  = await _filingRepository.GetFilingCountByReportIdAsync(report.Id, ct)
//    c. yield ReportRowDto(report.Id, report.ReportName, report.ImportDate,
//                          importerName, report.Status, filingCount)
// 4. Return Result<IReadOnlyList<ReportRowDto>, Error>.Success(rows)
//    Wrap the entire body in try/catch; any exception →
//    Result.Failure(new Error("GET_REPORTS_FAILED", ex.Message))
//
// Injected: IReportRepository, IImporterRepository, IFilingRepository
```

#### Extended query: `GetFilingsQuery`

```csharp
// Rentier.Application/Queries/GetFilingsQuery.cs  — EXTEND (add ReportIdFilter)
using Rentier.Application.Enums;

namespace Rentier.Application.Queries;

public sealed record GetFilingsQuery(
    FilingFilterMode Filter,
    int              Page,
    int              PageSize       = 20,
    Guid?            ReportIdFilter = null);   // ← NEW parameter
```

#### `GetFilingsQueryHandler` extension

```csharp
// Rentier.Application/Handlers/GetFilingsQueryHandler.cs  — EXTEND
//
// Before calling GetPagedAsync, add a branch:
// if (query.ReportIdFilter.HasValue)
// {
//     var filings = await _filingRepository.GetByReportIdAsync(query.ReportIdFilter.Value, ct);
//     var rows    = filings.Select(MapToDto).ToList();
//     return Result.Success(new FilingsPageResult(rows, rows.Count, 1));
// }
// else
// {
//     // existing GetPagedAsync path — unchanged
// }
```

#### New command: `ImportReportCommand`

```csharp
// Rentier.Application/Commands/ImportReportCommand.cs
namespace Rentier.Application.Commands;

public sealed record ImportReportCommand(
    Guid   ImporterId,
    string FileName,
    byte[] CsvContent);
```

#### `ImportReportCommandHandler`

```csharp
// Rentier.Application/Handlers/ImportReportCommandHandler.cs
// Implements ICommandHandler<ImportReportCommand, Result<Guid, Error>>
//
// Algorithm:
// 1. Validate CsvContent:
//    using var stream = new MemoryStream(command.CsvContent);
//    var parseResult  = await _statementParser.ParseAsync(stream, ct);
//    if (!parseResult.IsSuccess)
//        return Result<Guid, Error>.Failure(new Error("INVALID_CSV", parseResult.Error.Message));
//
// 2. Duplicate check:
//    var exists = await _reportRepository.ExistsByImporterAndNameAsync(
//                     command.ImporterId, command.FileName, ct);
//    if (exists)
//        return Result<Guid, Error>.Failure(new Error("DUPLICATE_REPORT",
//            $"A report named '{command.FileName}' already exists for this importer."));
//
// 3. Persist:
//    var report = Report.Create(command.ImporterId, command.FileName,
//                               command.CsvContent, mailboxMessageId: null);
//    await _reportRepository.AddAsync(report, ct);
//
// 4. Trigger pipeline:
//    var processResult = await _processReportsHandler.HandleAsync(
//                            new ProcessReportsCommand(), ct);
//    if (!processResult.IsSuccess)
//        return Result<Guid, Error>.Failure(processResult.Error);
//
// 5. Return Result<Guid, Error>.Success(report.Id)
//
// Error handling: entire body wrapped in try/catch;
//    any unexpected exception → Result.Failure(new Error("IMPORT_FAILED", ex.Message))
//
// Injected: IReportRepository, IStatementParser,
//           ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>
```

#### New command: `DeleteReportCommand`

```csharp
// Rentier.Application/Commands/DeleteReportCommand.cs
namespace Rentier.Application.Commands;

public sealed record DeleteReportCommand(Guid ReportId);
```

#### `DeleteReportCommandHandler`

```csharp
// Rentier.Application/Handlers/DeleteReportCommandHandler.cs
// Implements ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>
//
// Algorithm:
// try
// {
//     // Step 1: delete linked filings first (application-layer cascade)
//     await _filingRepository.DeleteByReportIdAsync(command.ReportId, ct);
//
//     // Step 2: delete the report
//     await _reportRepository.DeleteAsync(command.ReportId, ct);
//
//     return Result<VoidResult, Error>.Success(VoidResult.Value);
// }
// catch (Exception ex)
// {
//     return Result<VoidResult, Error>.Failure(
//         new Error("DELETE_REPORT_FAILED", ex.Message));
// }
//
// Injected: IReportRepository, IFilingRepository
```

#### `IFilingRepository` extensions

```csharp
// Rentier.Application/Repositories/IFilingRepository.cs  — ADD two new methods

/// <summary>
/// Returns the number of filings linked to the given report.
/// Used by GetReportsQueryHandler to populate FilingCount without loading full entities.
/// </summary>
Task<int> GetFilingCountByReportIdAsync(Guid reportId, CancellationToken ct = default);

/// <summary>
/// Deletes all Filing records whose ReportId matches the given reportId.
/// Implementation MUST use FindAsync/RemoveRange/SaveChangesAsync — NOT ExecuteDeleteAsync.
/// Used by DeleteReportCommandHandler before deleting the parent Report.
/// </summary>
Task DeleteByReportIdAsync(Guid reportId, CancellationToken ct = default);
```

---

### 1.3 Infrastructure Layer

**No EF migration required.** The `Filings` table already has a `ReportId` column. No new columns
or tables are introduced by this feature.

#### `FilingRepository.GetFilingCountByReportIdAsync`

```csharp
// Rentier.Infrastructure/Repositories/FilingRepository.cs  — ADD
public async Task<int> GetFilingCountByReportIdAsync(
    Guid reportId, CancellationToken ct = default)
    => await _db.Filings
           .AsNoTracking()
           .CountAsync(f => f.ReportId == reportId, ct);
```

#### `FilingRepository.DeleteByReportIdAsync`

```csharp
// Rentier.Infrastructure/Repositories/FilingRepository.cs  — ADD
//
// IMPORTANT: Do NOT use ExecuteDeleteAsync — it breaks SQLite in-memory tests.
public async Task DeleteByReportIdAsync(
    Guid reportId, CancellationToken ct = default)
{
    var filings = await _db.Filings
        .Where(f => f.ReportId == reportId)
        .ToListAsync(ct);

    if (filings.Count == 0) return;   // nothing to do; idempotent

    _db.Filings.RemoveRange(filings);
    await _db.SaveChangesAsync(ct);
}
```

---

### 1.4 Desktop Layer

#### New `ReportRowViewModel`

```csharp
// Rentier.Desktop/ViewModels/ReportRowViewModel.cs
// Purpose: immutable per-row snapshot bound to DataGrid rows.
// No ReactiveObject inheritance needed — display model only.
// Mutations go through ReportsViewModel commands.

public sealed class ReportRowViewModel
{
    public Guid         Id           { get; }
    public string       ReportName   { get; }
    public DateOnly     ImportDate   { get; }
    public string       ImporterName { get; }
    public ReportStatus Status       { get; }
    public int          FilingCount  { get; }

    // Derived display helpers
    public string ImportDateDisplay => ImportDate.ToString("yyyy-MM-dd");
    public string StatusDisplay     => Status.ToString();   // localised via converter in XAML

    public static ReportRowViewModel From(ReportRowDto dto) => new(dto);

    private ReportRowViewModel(ReportRowDto dto)
    {
        Id           = dto.Id;
        ReportName   = dto.ReportName;
        ImportDate   = dto.ImportDate;
        ImporterName = dto.ImporterName;
        Status       = dto.Status;
        FilingCount  = dto.FilingCount;
    }
}
```

#### `ReportsViewModel` full rewrite

```csharp
// Rentier.Desktop/ViewModels/ReportsViewModel.cs
// Inherits: ReactiveObject, IActivatableViewModel
//
// Injected:
//   ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>          syncHandler
//   IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>> getReports
//   ICommandHandler<ImportReportCommand, Result<Guid, Error>>               importReport
//   ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>         deleteReport
//   Func<string, string, Task<bool>>  confirmDelete      (wired in CompositionRoot)
//   Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>> showImportDialog (wired in CompositionRoot)
//   Action<Guid>                      navigateToFilings  (wired in MainWindowViewModel)
//   IScheduler?                       scheduler          (defaults to RxApp.MainThreadScheduler)
//
// State fields (all via this.RaiseAndSetIfChanged):
//   bool        _isLoading
//   bool        _isSyncing
//   string?     _errorMessage
//   string?     _syncStatusMessage
//   int         _syncProgressValue
//
// Collections:
//   ObservableCollection<ReportRowViewModel> Rows
//
// Commands (all ReactiveCommand.CreateFromTask):
//   LoadReportsCommand  — ReactiveCommand<Unit, Unit>
//   SyncCommand         — ReactiveCommand<Unit, Unit>  ← PRESERVED from original
//   ImportCommand       — ReactiveCommand<Unit, Unit>
//   DeleteCommand       — ReactiveCommand<Guid, Unit>
//   ViewFilingsCommand  — ReactiveCommand<Guid, Unit>
//   ClearErrorCommand   — ReactiveCommand.Create(() => ErrorMessage = null)
//
// Activation (matches FilingsViewModel pattern exactly):
//   public ViewModelActivator Activator { get; } = new();
//   this.WhenActivated(disposables =>
//   {
//       LoadReportsCommand.Execute().Subscribe().DisposeWith(disposables);
//   });
//
// LoadReportsAsync:
//   1. IsLoading = true; ErrorMessage = null
//   2. var result = await _getReports.HandleAsync(new GetReportsQuery(), ct)
//   3. if success: Rows.Clear(); foreach dto → Rows.Add(ReportRowViewModel.From(dto))
//   4. if failure: ErrorMessage = result.Error.Message
//   5. finally: IsLoading = false
//
// ImportAsync:
//   1. Invoke showImportDialog() (async) → null if cancelled → return early
//   2. Destructure (ImporterId, FileName, Content)
//   3. IsLoading = true
//   4. var result = await _importReport.HandleAsync(
//          new ImportReportCommand(ImporterId, FileName, Content), ct)
//   5. if failure: ErrorMessage = result.Error.Message
//   6. await LoadReportsAsync(ct)   ← always refresh
//   7. finally: IsLoading = false
//
// DeleteAsync(Guid reportId):
//   1. var confirmed = await _confirmDelete(
//          Strings.Reports_Delete_Confirmation_Title,
//          Strings.Reports_Delete_Confirmation_Message)
//   2. if !confirmed: return
//   3. IsLoading = true
//   4. var result = await _deleteReport.HandleAsync(
//          new DeleteReportCommand(reportId), ct)
//   5. if failure: ErrorMessage = result.Error.Message
//   6. await LoadReportsAsync(ct)
//   7. finally: IsLoading = false
//
// ViewFilingsAsync(Guid reportId):
//   _navigateToFilings(reportId)   ← fire and forget; synchronous delegate call
```

#### `FilingsViewModel` extension

```csharp
// Rentier.Desktop/ViewModels/FilingsViewModel.cs  — ADD ReportIdFilter property
//
// Add backing field:
//   private Guid? _reportIdFilter;
//
// Add property (after ShowAll):
//   public Guid? ReportIdFilter
//   {
//       get => _reportIdFilter;
//       set
//       {
//           this.RaiseAndSetIfChanged(ref _reportIdFilter, value);
//           _currentPage = 1;
//           this.RaisePropertyChanged(nameof(CurrentPage));
//           LoadPageCommand.Execute().Subscribe();
//       }
//   }
//
// Update LoadPageAsync to pass filter to query:
//   var q = new GetFilingsQuery(_filter, _currentPage, 20, _reportIdFilter);
```

#### `MainWindowViewModel` wiring

```csharp
// Rentier.Desktop/ViewModels/MainWindowViewModel.cs  — EXTEND constructor
//
// After resolving reportsVm and filingsVm from DI, wire the delegate:
//   var filingsVm = provider.GetRequiredService<FilingsViewModel>();
//   Action<Guid> navigateToFilings = reportId =>
//   {
//       filingsVm.ReportIdFilter = reportId;
//       SelectedEntry = NavigationEntries.First(e => e.ViewModel is FilingsViewModel);
//   };
//   var reportsVm = ... constructed with navigateToFilings delegate
//
// Note: ReportsViewModel must be constructed manually (not via AddTransient GetRequiredService)
// because it receives navigateToFilings which references filingsVm — resolve order matters.
// Register ReportsViewModel as a factory (Func<Action<Guid>, ReportsViewModel>) or construct
// it directly in MainWindowViewModel.
```

#### `ReportsView.axaml` full replacement (structural contract)

```xml
<!-- Key structural elements (exact XAML written during implementation) -->
<UserControl x:CompileBindings="False"
             xmlns:res="using:Rentier.Desktop.Resources">

  <!-- Toolbar row -->
  <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,0,0,8">
    <Button Content="{x:Static res:Strings.Reports_Button_Import}"
            Command="{Binding ImportCommand}"
            IsEnabled="{Binding !IsLoading}" />
    <Button Content="{x:Static res:Strings.Reports_Button_Sync}"
            Command="{Binding SyncCommand}"
            IsEnabled="{Binding !IsSyncing}" />
    <ProgressBar Minimum="0" Maximum="100"
                 Value="{Binding SyncProgressValue}"
                 IsVisible="{Binding IsSyncing}"
                 Width="120" />
    <TextBlock Text="{Binding SyncStatusMessage}"
               TextWrapping="Wrap"
               IsVisible="{Binding IsSyncing}" />
  </StackPanel>

  <!-- Loading indicator -->
  <ProgressBar IsIndeterminate="True" IsVisible="{Binding IsLoading}" />

  <!-- Error banner -->
  <StackPanel Orientation="Horizontal"
              IsVisible="{Binding ErrorMessage, Converter={...NotNull}}">
    <TextBlock Text="{Binding ErrorMessage}" />
    <Button Content="{x:Static res:Strings.Reports_Error_Dismiss}"
            Command="{Binding ClearErrorCommand}" />
  </StackPanel>

  <!-- DataGrid -->
  <DataGrid ItemsSource="{Binding Rows}"
            AutoGenerateColumns="False"
            IsReadOnly="True">
    <DataGrid.Columns>
      <DataGridTextColumn Header="{x:Static res:Strings.Reports_Col_Name}"
                          Binding="{Binding ReportName}" />
      <DataGridTextColumn Header="{x:Static res:Strings.Reports_Col_ImportDate}"
                          Binding="{Binding ImportDateDisplay}" />
      <DataGridTextColumn Header="{x:Static res:Strings.Reports_Col_Importer}"
                          Binding="{Binding ImporterName}" />
      <DataGridTextColumn Header="{x:Static res:Strings.Reports_Col_Status}"
                          Binding="{Binding Status,
                              Converter={StaticResource ReportStatusDisplayConverter}}" />
      <DataGridTextColumn Header="{x:Static res:Strings.Reports_Col_FilingCount}"
                          Binding="{Binding FilingCount}" />

      <!-- Action buttons column -->
      <DataGridTemplateColumn>
        <DataGridTemplateColumn.CellTemplate>
          <DataTemplate>
            <StackPanel Orientation="Horizontal" Spacing="4">
              <Button Content="{x:Static res:Strings.Reports_Button_ViewFilings}"
                      Command="{Binding DataContext.ViewFilingsCommand,
                                RelativeSource={RelativeSource AncestorType=DataGrid}}"
                      CommandParameter="{Binding Id}" />
              <Button Content="{x:Static res:Strings.Reports_Button_Delete}"
                      Command="{Binding DataContext.DeleteCommand,
                                RelativeSource={RelativeSource AncestorType=DataGrid}}"
                      CommandParameter="{Binding Id}" />
            </StackPanel>
          </DataTemplate>
        </DataGridTemplateColumn.CellTemplate>
      </DataGridTemplateColumn>
    </DataGrid.Columns>
  </DataGrid>

  <!-- Empty state -->
  <TextBlock Text="{x:Static res:Strings.Reports_Empty}"
             IsVisible="{Binding IsEmpty}" />
</UserControl>
```

#### `Strings.resx` new keys

| Key | Value |
|-----|-------|
| `Reports_Col_Name` | `Report Name` |
| `Reports_Col_ImportDate` | `Import Date` |
| `Reports_Col_Importer` | `Importer` |
| `Reports_Col_Status` | `Status` |
| `Reports_Col_FilingCount` | `Filings` |
| `Reports_Button_Import` | `Import…` |
| `Reports_Button_Sync` | `Sync Mailboxes` |
| `Reports_Button_ViewFilings` | `View Filings` |
| `Reports_Button_Delete` | `Delete` |
| `Reports_Error_Dismiss` | `Dismiss` |
| `Reports_Empty` | `No reports found.` |
| `Reports_Delete_Confirmation_Title` | `Delete Report` |
| `Reports_Delete_Confirmation_Message` | `This will permanently delete the report and all linked filings. This action cannot be undone.` |
| `Reports_Delete_Confirm_Button` | `Delete` |
| `Reports_Delete_Cancel_Button` | `Cancel` |
| `Reports_Import_Title` | `Import Report` |
| `Reports_Import_NoImporters` | `No importers are configured. Please add an importer before importing a report.` |
| `Reports_Import_FilePickerTitle` | `Select CSV File` |
| `Reports_Import_FilePickerFilter` | `CSV Files` |
| `Reports_Error_ImportFailed` | `Import failed. Please check the file format and try again.` |
| `Reports_Error_DuplicateReport` | `A report with this name already exists for the selected importer.` |
| `Reports_Error_InvalidCsv` | `The selected file is not a valid IBKR CSV export.` |
| `Reports_Error_DeleteFailed` | `Failed to delete the report. Please try again.` |
| `Reports_Error_LoadFailed` | `Failed to load reports. Please try again.` |
| `ReportStatus_Init` | `Init` |
| `ReportStatus_Processed` | `Processed` |
| `ReportStatus_Error` | `Error` |

#### `CompositionRoot` additions

```csharp
// Rentier.Desktop/Composition/CompositionRoot.cs
// In AddDesktopServices() — add AFTER existing filing handler registrations:

// Reports query handler
services.AddTransient<
    IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>,
    GetReportsQueryHandler>();

// Reports command handlers
services.AddTransient<
    ICommandHandler<ImportReportCommand, Result<Guid, Error>>,
    ImportReportCommandHandler>();
services.AddTransient<
    ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>,
    DeleteReportCommandHandler>();

// Confirmation delegate for report delete
services.AddTransient<Func<string, string, Task<bool>>>(provider => (title, msg) =>
    ConfirmDialogHelper.ShowAsync(
        title,
        msg,
        Strings.Reports_Delete_Confirm_Button,
        Strings.Reports_Delete_Cancel_Button));

// Import dialog delegate — reads file bytes + selects importer in Avalonia StorageProvider
// Returns null if the user cancels at any step
services.AddTransient<Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>>>(
    provider => async () =>
    {
        // Implementation detail: shown from App.axaml.cs / top-level window
        // Uses IImporterRepository via provider.GetRequiredService to populate dropdown
        // Full implementation written in Desktop layer during task execution
        throw new NotImplementedException("Wired during implementation");
    });

// NOTE: ReportsViewModel cannot be registered as AddTransient because it requires
// the navigateToFilings Action<Guid> delegate that references FilingsViewModel.
// MainWindowViewModel constructs ReportsViewModel directly, passing the delegate.
// ReportsViewModel registration is therefore REMOVED from the earlier stub and
// MainWindowViewModel becomes responsible for construction (see §1.4 MainWindowViewModel).
```

---

### 1.5 Converters needed

| Converter | Purpose |
|-----------|---------|
| `ReportStatusDisplayConverter` | Maps `ReportStatus` enum → localised string from `Strings.resx` |
| `NotNullToBoolConverter` | `object? → bool` (already exists if used in FilingsView; add if absent) |

---

## Constraints & Architecture Rules (Encoded)

> These rules MUST be verified in every task implementation and PR review.

1. **`AddTransient` ONLY** — Desktop uses root `ServiceProvider`; `AddScoped` throws at startup.
   All handler and ViewModel registrations use `services.AddTransient<...>`.

2. **No new EF migration** — This feature adds no new columns or tables. Any `dotnet ef migrations add` call is an error.

3. **EF delete pattern** — `DeleteByReportIdAsync` MUST follow:
   ```csharp
   var filings = await _db.Filings.Where(f => f.ReportId == reportId).ToListAsync(ct);
   _db.Filings.RemoveRange(filings);
   await _db.SaveChangesAsync(ct);
   ```
   `ExecuteDeleteAsync` is **forbidden** — it breaks SQLite in-memory tests.

4. **Handlers in `CompositionRoot`** — New Application-layer handlers are registered in
   `CompositionRoot.AddDesktopServices()`, NOT in `InfrastructureServiceExtensions`.
   `InfrastructureServiceExtensions` registers infrastructure services (repositories, parsers,
   HTTP clients) only.

5. **`WhenActivated` subscriptions** — ALL subscriptions inside `WhenActivated(disposables => {...})`
   MUST call `.DisposeWith(disposables)`. Missing `.DisposeWith` is a subscription leak.

6. **`x:CompileBindings="False"`** — ReportsView root `<UserControl>` MUST carry this attribute.
   It is required for DataGrid template column bindings to resolve correctly at runtime.

7. **ReactiveUI VMs** — All backing-field properties MUST use
   `this.RaiseAndSetIfChanged(ref _field, value)`. The `[Reactive]` Fody attribute is not used
   in this project.

8. **All dates `DateOnly`, all money `decimal`** — `ImportDate` is `DateOnly` end-to-end
   (domain → DTO → ViewModel). No `DateTime` or `double` introduced.

9. **`Result<T,Error>` API** — Use `Result<T,Error>.Success(value)` and
   `Result<T,Error>.Failure(error)` exactly — these are the static factory methods on the
   sealed class.
