# Analysis: 016 Dashboard & Deadline Alerts

**Generated**: 2025-07-18  
**Branch**: `005-dashboard-deadline-alerts`  
**Source files read**: Filing.cs, Mailbox.cs, MailboxCursor.cs, IncomeType.cs, IFilingRepository.cs,
IMailboxRepository.cs, IQueryHandler.cs, Result.cs, Error.cs, FilingRepository.cs, MailboxRepository.cs,
MainWindowViewModel.cs, MainWindow.axaml.cs, ReportsViewModel.cs, FilingsViewModel.cs,
CompositionRoot.cs, ViewLocator.cs, NavigationEntry.cs, Strings.resx, all view files

---

## Verified Type Signatures

### `Filing` entity (Rentier.Domain.Entities — inside `Filing.cs`)

```csharp
public Guid Id { get; private set; }
public FilingStatus Status { get; private set; }        // FilingStatus enum — see below
public IncomeType IncomeType { get; private set; }      // Rentier.Domain.Enums.IncomeType
public string PayingEntity { get; private set; }
public DateOnly FilingDeadline { get; private set; }
public decimal TaxPayableRsd { get; private set; }
public Guid TaxpayerProfileId { get; private set; }
public DateOnly TaxPeriod { get; private set; }
public DateOnly IncomeDate { get; private set; }
public decimal GrossIncomeRsd { get; private set; }
public decimal WhtPaidRsd { get; private set; }
public decimal GrossTaxPayableRsd { get; private set; }
public Guid? ReportId { get; private set; }
public string? PaymentReference { get; private set; }
```

### `FilingStatus` enum

⚠️ **CRITICAL NAMESPACE GOTCHA**: `FilingStatus` is defined **inside `Filing.cs`** in the
`Rentier.Domain.Entities` namespace — NOT in `Rentier.Domain.Enums`.

```csharp
// namespace Rentier.Domain.Entities  (same file as Filing class)
public enum FilingStatus
{
    Init   = 0,
    Filed  = 1,
    Paid   = 2
}
```

All new files referencing `FilingStatus` must use `using Rentier.Domain.Entities;`.

### `IncomeType` enum

```csharp
// namespace Rentier.Domain.Enums  (Rentier.Domain/Enums/IncomeType.cs)
public enum IncomeType { Dividend, Interest }
```

Uses `using Rentier.Domain.Enums;`.

### `MailboxCursor` value object (Rentier.Domain.ValueObjects)

```csharp
// Simple positional record — NOT a discriminated union
public record MailboxCursor(DateOnly? LastSyncDate, long? LastUid);
```

Field path: `mailbox.Cursor.LastSyncDate` (type: `DateOnly?`).
Both fields nullable — null means "no sync has occurred".

### `Mailbox` entity fields relevant to dashboard

```csharp
public Guid Id { get; private set; }
public MailboxCursor Cursor { get; private set; }   // always non-null post-construction
public DateOnly InitialSyncDate { get; private set; }
```

### `IQueryHandler<TQuery, TResult>` (Rentier.Application.Interfaces)

```csharp
public interface IQueryHandler<TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
```

### `Result<TValue, TError>` (Rentier.Application.Common)

```csharp
public sealed class Result<TValue, TError>
{
    public bool IsSuccess { get; }
    public TValue Value => ...;   // throws if failure
    public TError Error => ...;   // throws if success

    public static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error);
}
```

### `Error` (Rentier.Application.Common)

```csharp
public sealed record Error(string Code, string Message)
{
    public static Error Domain(string message)          => new("DOMAIN_ERROR", message);
    public static Error NotFound(string message)        => new("NOT_FOUND", message);
    public static Error Infrastructure(string message)  => new("INFRASTRUCTURE_ERROR", message);
}
```

### `IFilingRepository` — existing 11 methods (do not redeclare)

```csharp
Task<Filing?> GetByIdAsync(Guid id, CancellationToken ct = default);
Task<IReadOnlyList<Filing>> GetAllAsync(CancellationToken ct = default);
Task<Filing?> GetByTaxPeriodAsync(Guid taxpayerProfileId, DateOnly taxPeriod, CancellationToken ct = default);
Task<bool> ExistsByIncomeAsync(Guid taxpayerProfileId, string payingEntity, DateOnly incomeDate, decimal grossIncomeRsd, CancellationToken ct = default);
Task<IReadOnlyList<Filing>> GetByReportIdAsync(Guid reportId, CancellationToken ct = default);
Task AddAsync(Filing filing, CancellationToken ct = default);
Task UpdateAsync(Filing filing, CancellationToken ct = default);
Task DeleteAsync(Guid id, CancellationToken ct = default);
Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(FilingFilterMode filter, int skip, int take, CancellationToken ct = default);
Task<int> GetFilingCountByReportIdAsync(Guid reportId, CancellationToken ct = default);
Task DeleteByReportIdAsync(Guid reportId, CancellationToken ct = default);
```

**Add three new methods** (T002):

```csharp
Task<IReadOnlyList<Filing>> GetUpcomingAsync(DateOnly today, int days, CancellationToken ct = default);
Task<IReadOnlyList<Filing>> GetOverdueAsync(DateOnly today, CancellationToken ct = default);
Task<(int InitCount, int FiledCount, int PaidCount, decimal TotalUnpaidRsd)> GetFilingStatsAsync(CancellationToken ct = default);
```

### `NavigationEntry` record (Rentier.Desktop.ViewModels)

```csharp
public record NavigationEntry(string Label, ReactiveObject ViewModel);
```

---

## Exact Code Patterns to Reuse

### Pattern 1 — FilingRepository EF read query (copy for T012, T016, T019)

All read methods follow the same 3-step pattern:

```csharp
// Step 1: start with AsNoTracking
var query = _db.Filings.AsNoTracking();

// Step 2: chain Where + OrderBy
var list = await query
    .Where(f => (f.Status == FilingStatus.Init || f.Status == FilingStatus.Filed)
                && f.FilingDeadline >= today
                && f.FilingDeadline <= end)
    .OrderBy(f => f.FilingDeadline)
    .ToListAsync(ct);

// Step 3: return as readonly
return list.AsReadOnly();
```

`GetFilingStatsAsync` aggregates in-memory (single full-table load, then LINQ counts):

```csharp
var filings = await _db.Filings.AsNoTracking().ToListAsync(ct);
var initCount   = filings.Count(f => f.Status == FilingStatus.Init);
var filedCount  = filings.Count(f => f.Status == FilingStatus.Filed);
var paidCount   = filings.Count(f => f.Status == FilingStatus.Paid);
var totalUnpaid = filings.Where(f => f.Status != FilingStatus.Paid).Sum(f => f.TaxPayableRsd);
return (initCount, filedCount, paidCount, totalUnpaid);
```

Empty table yields `(0, 0, 0, 0m)` — no special handling needed.

### Pattern 2 — ReactiveObject property with backing field (copy for T014)

Taken verbatim from `FilingsViewModel.cs`:

```csharp
private bool _isLoading;

public bool IsLoading
{
    get => _isLoading;
    private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
}
```

**Never use `[Reactive]` attribute** — the project does not use ReactiveUI source generators.

### Pattern 3 — WhenActivated auto-load with DisposeWith (copy for T014)

Taken from `FilingsViewModel.cs` (constructor body, last statement):

```csharp
this.WhenActivated(disposables =>
{
    LoadPageCommand.Execute().Subscribe().DisposeWith(disposables);
    ExportCommand.ThrownExceptions
        .Subscribe(ex => ErrorMessage = ex.Message)
        .DisposeWith(disposables);
});
```

For `DashboardViewModel` (T014):

```csharp
this.WhenActivated(disposables =>
{
    LoadCommand.Execute().Subscribe().DisposeWith(disposables);
    LoadCommand.ThrownExceptions
        .Subscribe(ex => ErrorMessage = ex.Message)
        .DisposeWith(disposables);
});
```

### Pattern 4 — ReactiveCommand.CreateFromTask patterns (copy for T014)

```csharp
// No parameter, no canExecute
LoadCommand = ReactiveCommand.CreateFromTask(
    LoadAsync, outputScheduler: _scheduler);

// No parameter, synchronous
ClearErrorCommand = ReactiveCommand.Create(
    () => { ErrorMessage = null; }, outputScheduler: _scheduler);
```

### Pattern 5 — ReactiveUserControl AXAML header (copy for T010)

Taken from `FilingsView.axaml` (line 1–10):

```xml
<reactive:ReactiveUserControl
    x:TypeArguments="vm:DashboardViewModel"
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:reactive="http://reactiveui.net"
    xmlns:vm="using:Rentier.Desktop.ViewModels"
    xmlns:res="using:Rentier.Desktop.Resources"
    xmlns:local="using:Rentier.Desktop.Converters"
    x:Class="Rentier.Desktop.Views.DashboardView"
    x:CompileBindings="False">
```

### Pattern 6 — ReactiveUserControl code-behind (copy for T011)

Taken from `FilingsView.axaml.cs`:

```csharp
using Avalonia.ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class DashboardView : ReactiveUserControl<DashboardViewModel>
{
    public DashboardView() => InitializeComponent();
}
```

Add `DataGrid` row-click handler here (see Gotchas §3):

```csharp
private void UpcomingGrid_DoubleTapped(object? sender, TappedEventArgs e)
{
    ViewModel?.NavigateToFilingsCommand.Execute().Subscribe();
}
```

### Pattern 7 — ActivatorUtilities.CreateInstance for ViewModels (copy for T009)

Taken from `MainWindowViewModel.cs` (existing `ReportsViewModel` creation):

```csharp
var reportsVm = ActivatorUtilities.CreateInstance<ReportsViewModel>(
    provider, navigateToFilings);   // extra args passed positionally after DI-resolved args
```

For `DashboardViewModel`:

```csharp
Action navigateToFilings = () =>
{
    var e = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
    if (e is not null) SelectedEntry = e;
};

var dashboardVm = ActivatorUtilities.CreateInstance<DashboardViewModel>(
    provider, navigateToFilings);   // Action is the non-DI extra arg
```

Note: rename the existing `navigateToFilings` (`Action<Guid>`) to `navigateToFilingsWithReport`
to avoid the name collision with the new `Action` (no Guid) delegate.

### Pattern 8 — DI query handler registration (copy for T008)

Taken from `CompositionRoot.cs`:

```csharp
services.AddTransient<
    IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>,
    GetDashboardQueryHandler>();
```

### Pattern 9 — MainWindowViewModel NavigationEntries after T009

```csharp
NavigationEntries = new List<NavigationEntry>
{
    new(Strings.Nav_Dashboard, dashboardVm),    // index 0 — NEW
    new(Strings.Nav_Filings,   filingsVm),      // index 1 — was 0
    new(Strings.Nav_Reports,   reportsVm),      // index 2 — was 1
    new(Strings.Nav_Settings,  settingsVm),     // index 3 — was 2
};
_selectedEntry   = NavigationEntries[0];    // Dashboard is default (was filingsVm)
_currentViewModel = dashboardVm;            // Dashboard is default (was filingsVm)
```

### Pattern 10 — LastSyncDate resolution LINQ chain (T007)

```csharp
var mailboxes = await _mailboxRepo.GetAllAsync(ct);
DateOnly? lastSyncDate = mailboxes
    .Select(m => m.Cursor.LastSyncDate)
    .Where(d => d.HasValue)
    .Select(d => d!.Value)
    .OrderByDescending(d => d)
    .Cast<DateOnly?>()
    .FirstOrDefault();
// Returns null when no mailbox or all LastSyncDate values are null
```

### Pattern 11 — Navigation wiring in MainWindow.axaml.cs (DO NOT CHANGE)

```csharp
// Existing subscriber in MainWindow.axaml.cs — already handles SelectedEntry→CurrentViewModel
this.WhenActivated(disposables =>
{
    this.WhenAnyValue(x => x.ViewModel!.SelectedEntry)
        .Subscribe(entry =>
        {
            if (entry is not null && ViewModel is not null)
                ViewModel.CurrentViewModel = entry.ViewModel;
        })
        .DisposeWith(disposables);
});
```

**Setting `SelectedEntry` alone is sufficient for navigation** — `CurrentViewModel` is synced
automatically by this subscriber. Do NOT manually set `CurrentViewModel` in delegates.

---

## Gotchas & Non-Obvious Constraints

### Gotcha 1 — `FilingStatus` lives in `Rentier.Domain.Entities`, not `Rentier.Domain.Enums`

`FilingStatus` is declared inside `Filing.cs` in the `Rentier.Domain.Entities` namespace.
`Rentier.Domain.Enums` contains only `IncomeType`, `ReportType`, `ReportStatus`.

**Impact**: All new DTOs and the handler that reference `FilingStatus` must use:

```csharp
using Rentier.Domain.Entities;   // for FilingStatus
using Rentier.Domain.Enums;      // for IncomeType (separate)
```

Failing to include `Rentier.Domain.Entities` produces a `CS0246` compile error on `FilingStatus`.

### Gotcha 2 — `NullToBoolConverter` does not exist in the project

`T010` references `{StaticResource NullToBoolConverter}` to show the error banner when
`ErrorMessage != null`. **This converter is not in the project** — only `InvertBoolConverter` exists.

**Correct approach**: Avalonia 11 ships `ObjectConverters.IsNotNull` which can be used directly:

```xml
<Border IsVisible="{Binding ErrorMessage,
    Converter={x:Static ObjectConverters.IsNotNull}}" ...>
```

Alternatively, add a `NullToBoolConverter.cs` to `Rentier.Desktop/Converters/` following the
same pattern as `InvertBoolConverter.cs`:

```csharp
public static class NullToBoolConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<object?, bool>(v => v is not null);
}
```

### Gotcha 3 — Avalonia DataGrid has no built-in row-click-to-command binding

Avalonia `DataGrid` does not support binding a `RowCommand` directly in XAML. The `DoubleTapped`
or `PointerPressed` event must be handled in code-behind (the established pattern in this project —
see `FilingsView.axaml.cs` for the code-behind event handler pattern).

The spec calls for single-click navigation. Use `PointerPressed` or `DoubleTapped` in the axaml
code-behind:

```csharp
private void UpcomingGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (sender is DataGrid { SelectedItem: UpcomingDeadlineRowViewModel })
        ViewModel?.NavigateToFilingsCommand.Execute().Subscribe();
}
```

Wire via `<DataGrid ... PointerPressed="UpcomingGrid_PointerPressed">`.

### Gotcha 4 — Avalonia `WrapPanel` does not have `ItemWidth`

`T020` references `<WrapPanel Orientation="Horizontal" ItemWidth="160">`. Avalonia's `WrapPanel`
does **not** have an `ItemWidth` property (unlike WPF). Use explicit `Width` on each card `Border`
or use a `UniformGrid` with `Columns="5"` for the five summary cards:

```xml
<UniformGrid Columns="5" Margin="0,0,0,8">
    <Border Width="150" Padding="12" Margin="0,0,8,0" ...>
```

Or set `MinWidth` on the StackPanel inside each card.

### Gotcha 5 — Navigation: setting `SelectedEntry` is sufficient; do NOT also set `CurrentViewModel`

`MainWindow.axaml.cs` subscribes to `SelectedEntry` changes and automatically updates
`CurrentViewModel` (see Pattern 11). In the navigation delegates for `DashboardViewModel` and
in `T009`, only `SelectedEntry` needs to be set. Setting both redundantly is harmless but
confusing — only set `SelectedEntry`.

### Gotcha 6 — `OverdueFilingDto` INCLUDES `FilingStatus Status` (clarify.md is superseded)

`clarify.md Q3` states `OverdueFilingDto` does **not** include `Status`. However, all later
artifacts (`data-model.md`, `application-contracts.md`, `tasks.md T004/T017`) consistently
define it **with** `FilingStatus Status`. Follow `tasks.md` (the authoritative implementation
artifact):

```csharp
public sealed record OverdueFilingDto(
    Guid Id,
    string PayingEntity,
    DateOnly FilingDeadline,
    decimal TaxPayableRsd,
    FilingStatus Status);        // ← include this
```

### Gotcha 7 — `NavigateToFilingsCommand` type is `ReactiveCommand<Unit, Unit>`, not `ReactiveCommand<Guid, Unit>`

`data-model.md` Section 6 shows `ReactiveCommand<Guid, Unit>`, but `tasks.md T014` specifies
`ReactiveCommand<Unit, Unit>`. No filing ID is passed — the command just switches to the Filings
pane. Use `ReactiveCommand<Unit, Unit>` (tasks.md is authoritative).

### Gotcha 8 — `ReportsView.axaml` uses plain `UserControl` in AXAML but `ReactiveUserControl<T>` in code-behind

`ReportsView.axaml` root element is `<UserControl ...>` without `reactive:ReactiveUserControl`.
`DashboardView.axaml` should follow the `FilingsView.axaml` pattern instead (uses
`<reactive:ReactiveUserControl x:TypeArguments="vm:DashboardViewModel">`), because
`DashboardViewModel` implements `IActivatableViewModel` and requires the Avalonia activation
lifecycle to fire `WhenActivated`. Without `ReactiveUserControl`, `WhenActivated` will not be
invoked when the view attaches to the visual tree.

### Gotcha 9 — `DashboardViewModel` constructor extra arg is `Action` (not `Action<Guid>`)

The existing `navigateToFilings` in `MainWindowViewModel` has type `Action<Guid>` (passes a report
ID to `FilingsViewModel`). For `DashboardViewModel`, the navigate delegate is a plain `Action`
(no parameter). Rename the existing one to `navigateToFilingsWithReport` to avoid a local variable
name collision when both are in scope in the updated `MainWindowViewModel` constructor (T009).

### Gotcha 10 — `GetFilingStatsAsync` EF note: `FilingStatus` comparison in queries

In the EF queries for the three new repository methods, the WHERE clause comparisons like
`f.Status == FilingStatus.Init` work with EF Core's SQLite provider because `FilingStatus` is
stored as INTEGER. No special conversion is needed — EF Core handles the enum-to-int mapping
automatically via the existing entity configuration.

### Gotcha 11 — `decimal` formatting must use `CultureInfo.InvariantCulture`

All display strings for money amounts must be formatted with `CultureInfo.InvariantCulture`
(constitution Principle II):

```csharp
TaxPayableDisplay = dto.TaxPayableRsd.ToString("N2", CultureInfo.InvariantCulture) + " RSD";
TotalUnpaidDisplay = dto.TotalUnpaidRsd.ToString("N2", CultureInfo.InvariantCulture) + " RSD";
```

Output for 8500.50 → `"8,500.50 RSD"` (comma as thousands separator, InvariantCulture).

### Gotcha 12 — Task.WhenAll for concurrent repository calls in handler (T007)

The handler contract (`application-contracts.md`) says the three filing repo calls *may* run
concurrently via `Task.WhenAll`. However, SQLite is single-writer and all three calls share the
same `AppDbContext` which is NOT thread-safe. **Do not use `Task.WhenAll`** for the three
`FilingRepository` calls — run them sequentially. Only the `MailboxRepository.GetAllAsync` call
(different DbContext instance — `IMailboxRepository` vs `IFilingRepository`) could theoretically
run concurrently, but sequential is safer and simpler with SQLite.

---

## Task-by-Task Notes

### Phase 1: Setup

**T001** — No issues. Add 12 keys to `Strings.resx`. None of the `Nav_Dashboard` or `Dashboard_*`
keys exist yet. Follow the existing naming pattern (section prefix + underscore + description).
Verify the designer picks up new keys by rebuilding after edit.

### Phase 2: Foundational

**T002** — Add three method signatures to `IFilingRepository.cs`. The file currently has 11
methods. Note the file uses `using Rentier.Application.Enums;` (for `FilingFilterMode`) —
`FilingStatus` comes from `using Rentier.Domain.Entities;` which is already present.

**T003** — `UpcomingDeadlineDto` needs `using Rentier.Domain.Entities;` (FilingStatus) and
`using Rentier.Domain.Enums;` (IncomeType). Both are separate using directives.

**T004** — `OverdueFilingDto` includes `FilingStatus Status` — see Gotcha 6. Use
`using Rentier.Domain.Entities;`.

**T005** — `DashboardDto` references `UpcomingDeadlineDto` and `OverdueFilingDto`. Add the full
using for both DTOs. All within `Rentier.Application.DTOs` namespace.

**T006** — `GetDashboardQuery` is a parameterless record: `public sealed record GetDashboardQuery();`
The trailing `()` is required for a parameterless positional record.

**T007** — Handler implementation notes:
- Constructor: `IFilingRepository _filingRepo, IMailboxRepository _mailboxRepo`
- Run three `FilingRepository` calls **sequentially** (not `Task.WhenAll`) — see Gotcha 12
- Use the LINQ chain from Pattern 10 for `LastSyncDate` resolution
- The try/catch wraps the entire body and returns `Result.Failure(Error.Infrastructure(ex.Message))`
- Empty database returns `Result.Success(new DashboardDto([],[],0,0,0,0m,null))` — NOT an error
- Required usings: `Rentier.Application.DTOs`, `Rentier.Application.Queries`,
  `Rentier.Application.Repositories`, `Rentier.Application.Common`,
  `Rentier.Application.Interfaces`, `Rentier.Domain.Entities`

### Phase 3: Navigation Entry

**T008** — Add to `CompositionRoot.AddDesktopServices()`. Also add three using directives:
`Rentier.Application.Queries`, `Rentier.Application.DTOs`, `Rentier.Application.Handlers`.
Do NOT register `DashboardViewModel` — it is created via `ActivatorUtilities.CreateInstance`.

**T009** — Critical changes to `MainWindowViewModel.cs`:
1. Rename existing `navigateToFilings` (`Action<Guid>`) to `navigateToFilingsWithReport`
2. Create new `Action navigateToFilings = () => { ... }` (no-arg version) before `dashboardVm`
3. Create `dashboardVm` via `ActivatorUtilities.CreateInstance<DashboardViewModel>(provider, navigateToFilings)`
4. Update `NavigationEntries` list (Pattern 9)
5. `_selectedEntry = NavigationEntries[0]` — Dashboard at index 0
6. `_currentViewModel = dashboardVm` — Dashboard as default content
7. Do NOT touch `MainWindow.axaml.cs` — the `WhenActivated` subscriber already handles the rest

**T010** — AXAML header must use `ReactiveUserControl<DashboardViewModel>` (Pattern 5), NOT plain
`UserControl`. Use `ObjectConverters.IsNotNull` for the error banner visibility binding (Gotcha 2).
No `NullToBoolConverter` static resource is needed.

**T011** — Code-behind: `public partial class DashboardView : ReactiveUserControl<DashboardViewModel>`.
Add the DataGrid row-click handler (Gotcha 3). The `this.WhenActivated(d => { })` stub in the
tasks is sufficient — but a cleaner approach is to just omit it and let the AXAML controls'
bindings drive activation. The `ReactiveUserControl` base class already handles lifecycle wiring.

### Phase 4: Upcoming Deadlines

**T012** — Implement `GetUpcomingAsync`. Use `end = today.AddDays(days)`. Boundaries are inclusive
(`>= today` AND `<= end`). `AsNoTracking()` is mandatory for all read-only repository queries.

**T013** — `UpcomingDeadlineRowViewModel` is a plain C# class (NOT `ReactiveObject`). Only
auto-properties and a static factory `From(UpcomingDeadlineDto dto)`. This is a display-only
snapshot — no change notification needed.

**T014** — `DashboardViewModel` is the most complex task. Key implementation notes:
- Constructor signature: `(IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>> handler, Action navigateToFilings, IScheduler? scheduler = null)` — ActivatorUtilities resolves `IQueryHandler` from DI; `navigateToFilings` is passed as an extra arg
- `ObservableCollection<UpcomingDeadlineRowViewModel>` and `ObservableCollection<OverdueFilingRowViewModel>` are initialized in field declarations (`= new()`)
- `HasOverdueFilings`, `OverdueCount`, `IsEmpty` are computed read-only properties — raise them
  manually with `this.RaisePropertyChanged(nameof(...))` after loading
- `_scheduler ?? RxApp.MainThreadScheduler` pattern for testability
- After `LoadAsync`, call `this.RaisePropertyChanged` for `HasOverdueFilings`, `OverdueCount`, `IsEmpty`
- `HasData` property (bool, default false, set to true on first successful load) — present in tasks.md
  but absent from data-model.md. Include it as specified in T014.

**T015** — DataGrid row-click must go through code-behind (Gotcha 3). Use `PointerPressed` event
on the DataGrid element, checked in code-behind as described in Gotcha 3.

### Phase 5: Overdue Alert

**T016** — `GetOverdueAsync` filter: `f.FilingDeadline < today` (STRICT less-than). Deadline equal
to today is NOT overdue — this matches user story acceptance scenario 4.

**T017** — `OverdueFilingRowViewModel` includes `FilingStatus Status` property (Gotcha 6) even
though the ViewModel may not display it in the current AXAML design. Include it for completeness.

**T018** — For the inverse visibility (empty state TextBlock), use
`{Binding HasOverdueFilings, Converter={x:Static local:InvertBoolConverter.Instance}}`.
`InvertBoolConverter` already exists in `Rentier.Desktop.Converters`.

### Phase 6: Summary Statistics

**T019** — `GetFilingStatsAsync` does a full table load into memory then aggregates with LINQ.
This is intentional (spec §Non-Functional: acceptable for ≤10,000 filings on single-user SQLite).
Returns `(0, 0, 0, 0m)` on empty table automatically (LINQ Count and Sum return 0 on empty sequence).

**T020** — Use `UniformGrid Columns="5"` instead of `WrapPanel ItemWidth="160"` (Gotcha 4).
Alternatively use a horizontal `StackPanel` with fixed-width `Border` cards. The `WrapPanel`
approach works but `ItemWidth` must be replaced with explicit `Width="160"` on each `Border`.

### Phase 7: Last Sync Timestamp

**T021** — `LastSyncDisplay` is already populated in `DashboardViewModel.LoadAsync` (T014).
This task only adds the View card. No code changes are needed beyond the AXAML edit.

### Phase 8: Tests

**T022** — Application handler tests. Key notes:
- For the concurrent-call boundary tests, note that the handler runs calls sequentially in the
  recommended implementation — test that all three repositories are called with correct arguments
- For `HandleAsync_LastSyncDate_MaxAcrossMailboxes`, construct `MailboxCursor` with the two dates:
  `new MailboxCursor(LastSyncDate: DateOnly.Parse("2025-07-10"), LastUid: null)` etc.
- NSubstitute mock for `IMailboxRepository.GetAllAsync(ct)` returns a list of `Mailbox` objects;
  construct `Mailbox` via the public constructor (not `Mailbox.Create` which resets the cursor)

**T023** — Infrastructure repo tests. Notes:
- Use the same SQLite in-memory `AppDbContext` setup as existing tests in `Rentier.Infrastructure.Tests`
- For `GetFilingStatsAsync_TotalUnpaidRsd_SumsNonPaid`: 1000m + 2500.50m = 3500.50m (only Init + Filed)
- When seeding `Filing` entities for infrastructure tests, use `Filing.CreateFromIncome(...)` factory
  method — the private parameterless constructor is EF-only. For status progression use `AdvanceStatus`

**T024** — Desktop ViewModel tests. Notes:
- `ImmediateScheduler.Instance` (from `System.Reactive.Concurrency`) for synchronous test execution
- For `LoadCommand_SetsIsLoadingDuringExecution`: use `TaskCompletionSource<Result<DashboardDto,Error>>`
  to pause the mock handler; sample `IsLoading` before and after `tcs.SetResult(...)`

### Phase 9: Polish

**T025** — Build expected compile errors: missing usings in `CompositionRoot.cs`,
`MainWindowViewModel.cs` (rename collision if not resolved), row ViewModel factory methods.

**T026** — Run with `--filter "FullyQualifiedName~Dashboard"`.

**T027** — Architecture compliance audit. All 8 gates expected to pass.

---

## Potential Merge Conflicts with 015

### Branch status (as of analysis date)

Both `004-one-click-sync-workflow` (feature/015) and `005-dashboard-deadline-alerts` (feature/016)
point to the **same commit** as `master` — neither has been implemented yet. There are no current
conflicts; the risk is hypothetical but likely if development proceeds in parallel.

### Files touched by BOTH features

| File | 016 Change | 015 Expected Change | Conflict Risk |
|------|------------|---------------------|---------------|
| `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs` | Add `DashboardViewModel`, new nav entries, rename delegate | Add sync navigation entry (if dedicated pane) or modify existing sync in `ReportsViewModel` | **HIGH** — both modify `NavigationEntries` list and constructor |
| `src/Rentier.Desktop/Composition/CompositionRoot.cs` | Add `GetDashboardQueryHandler` registration | Add sync handler registrations | **MEDIUM** — append-only; easy to merge |
| `src/Rentier.Desktop/Resources/Strings.resx` | Add 12 `Dashboard_*` / `Nav_Dashboard` keys | Likely adds `Nav_Sync` or similar key | **LOW** — append-only; merge conflict unlikely unless same key names |

### Recommended resolution strategy

1. **Implement 016 first** (current branch is already `005-dashboard-deadline-alerts`).
   Merge into `master` once all 31 tests pass.
2. When 015 (`004-one-click-sync-workflow`) is implemented, rebase onto updated master.
3. If 015 adds a dedicated Sync navigation entry to `MainWindowViewModel`, it should insert after
   Dashboard (index 1) or after Settings (last) — not at index 0 (Dashboard is fixed at 0 per spec).

### Dashboard navigation index after 015 merge

Per `clarify.md Q4`, Dashboard is **always index 0**. If 015 adds a Sync entry:
- Dashboard: 0
- Filings: 1
- Reports: 2
- Sync (if added): 3
- Settings: last

The `NavigationEntries[0]` default selection in `MainWindowViewModel` is hardcoded after T009 —
it does not change if new entries are appended.

### Note on existing SyncCommand in ReportsViewModel

The existing `ReportsViewModel` already has a `SyncCommand` that runs IMAP sync. Feature 015
("one-click sync workflow") may extract this into a dedicated view or enhance the existing
`ReportsViewModel` flow. If 015 does NOT add a new navigation entry (only changes sync UX within
Reports), there is **no conflict** with `MainWindowViewModel` navigation at all.

---

## Architecture Compliance Checklist

The 8 constitution gates verified against this feature's design artifacts:

| Gate | Status | Evidence |
|------|--------|---------|
| **1. Clean Architecture boundaries** | ✅ PASS | `DashboardViewModel` → `IQueryHandler` only (no repo refs). `GetDashboardQueryHandler` → `IFilingRepository` + `IMailboxRepository` (Application interfaces). Infrastructure repos implement Application interfaces. |
| **2. Monetary values as `decimal`** | ✅ PASS | `TaxPayableRsd`, `TotalUnpaidRsd`, `TotalUnpaidDisplay` all `decimal`. Display formatting uses `CultureInfo.InvariantCulture`. No `float`/`double` anywhere. |
| **3. Business dates as `DateOnly`** | ✅ PASS | `FilingDeadline` = `DateOnly`. `LastSyncDate` = `DateOnly?`. `today` = `DateOnly.FromDateTime(DateTime.Today)`. No `DateTime` in Application or Domain layers. |
| **4. No unapproved network calls** | ✅ PASS | Feature is purely read-only from local SQLite. Zero outbound calls. |
| **5. All I/O async** | ✅ PASS | All three new repo methods are `Task<T>`. Handler uses `async Task`. ViewModel uses `ReactiveCommand.CreateFromTask`. No `.Result` or `.Wait()`. |
| **6. `AddTransient` only (no `AddScoped`)** | ✅ PASS | `GetDashboardQueryHandler` registered as `AddTransient`. `DashboardViewModel` not registered in DI at all (ActivatorUtilities). `MainWindowViewModel` remains `AddSingleton` (unchanged). |
| **7. `x:CompileBindings="False"` on views** | ✅ PASS | `DashboardView.axaml` must include `x:CompileBindings="False"` — all existing views use this. T027 verifies compliance. |
| **8. No `[Reactive]` / no `ExecuteDeleteAsync`** | ✅ PASS | No source generators used. All properties use `this.RaiseAndSetIfChanged(ref _field, value)`. Feature is read-only — no delete operations, no `ExecuteDeleteAsync`. |

---

## Cross-Artifact Inconsistency Register

| # | Location | Issue | Resolution |
|---|----------|-------|------------|
| I1 | `clarify.md Q3` vs `tasks.md T004/T017` | `OverdueFilingDto.Status`: clarify.md omits it; all later artifacts include it | **Follow tasks.md** — include `FilingStatus Status` |
| I2 | `data-model.md` Commands table vs `tasks.md T014` | `NavigateToFilingsCommand` type: data-model says `ReactiveCommand<Guid, Unit>`, tasks.md says `ReactiveCommand<Unit, Unit>` | **Follow tasks.md** — use `ReactiveCommand<Unit, Unit>` |
| I3 | `tasks.md T010` | References `NullToBoolConverter` static resource | **Use `ObjectConverters.IsNotNull`** (built into Avalonia 11) — no new converter file needed |
| I4 | `tasks.md T020` | References `WrapPanel ItemWidth="160"` | **Use `UniformGrid Columns="5"`** or set `Width` on each `Border` — `WrapPanel.ItemWidth` is WPF-only |
| I5 | `tasks.md T007` | Suggests `Task.WhenAll` for concurrent repo calls | **Run sequentially** — SQLite + shared DbContext is not thread-safe |
| I6 | `data-model.md` `OverdueFilingRowViewModel` table | Does not list `Status` property | **Include `Status`** — needed for `From(OverdueFilingDto dto)` mapping in T017 |

---

## Known Facts Checklist

For the implementer to verify before starting each phase:

- [ ] `FilingStatus` using = `Rentier.Domain.Entities` (NOT `Rentier.Domain.Enums`)
- [ ] `IncomeType` using = `Rentier.Domain.Enums`
- [ ] `MailboxCursor` is a simple `record(DateOnly? LastSyncDate, long? LastUid)`
- [ ] `IFilingRepository` currently has 11 methods — add 3 (GetUpcomingAsync, GetOverdueAsync, GetFilingStatsAsync)
- [ ] `IMailboxRepository.GetAllAsync()` already exists — no changes needed
- [ ] `NavigationEntry` is `record(string Label, ReactiveObject ViewModel)` in its own file
- [ ] `ViewLocator` is convention-based: `FooViewModel` → `FooView` (namespace + class name replace)
- [ ] `MainWindow.axaml.cs` syncs `CurrentViewModel` from `SelectedEntry` — do NOT manually set `CurrentViewModel` in delegates
- [ ] `NullToBoolConverter` does NOT exist — use `ObjectConverters.IsNotNull`
- [ ] `Avalonia WrapPanel` has NO `ItemWidth` — use `UniformGrid` or per-element `Width`
- [ ] DataGrid row-click = code-behind event handler (not XAML command binding)
- [ ] `Task.WhenAll` for SQLite repo calls = NOT safe — run sequentially
- [ ] `OverdueFilingDto` includes `FilingStatus Status` (follow tasks.md, not clarify.md)
- [ ] `NavigateToFilingsCommand` is `ReactiveCommand<Unit, Unit>` (follow tasks.md)
- [ ] No `[Reactive]` attribute — all properties use `this.RaiseAndSetIfChanged(ref _field, value)`
- [ ] `DashboardViewModel` NOT registered in DI — constructed via `ActivatorUtilities.CreateInstance`
- [ ] Rename existing `navigateToFilings` in MainWindowViewModel to `navigateToFilingsWithReport` before adding the new `Action` version
- [ ] `decimal` display = `ToString("N2", CultureInfo.InvariantCulture) + " RSD"`
- [ ] `DateOnly` display = `ToString("yyyy-MM-dd")`
- [ ] Empty database → `Result.Success` with zeroed DTO (NOT `Result.Failure`)
- [ ] `FilingDeadline == today` → NOT overdue (strictly `< today` for overdue filter)
- [ ] `FilingDeadline == today` → IS upcoming (inclusive lower bound `>= today` for upcoming filter)
- [ ] Feature 015 branch (`004-one-click-sync-workflow`) is at same commit as master — no current conflicts, but `MainWindowViewModel.cs` is a merge conflict risk if both features proceed in parallel
