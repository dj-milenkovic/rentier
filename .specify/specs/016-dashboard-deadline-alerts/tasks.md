# Tasks: Dashboard with Deadline Alerts (016)

**Feature**: `016-dashboard-deadline-alerts`  
**Branch**: `005-dashboard-deadline-alerts`  
**Input**: `.specify/specs/016-dashboard-deadline-alerts/` — spec.md · plan.md · data-model.md · contracts/application-contracts.md · clarify.md  
**Generated**: 2025-07-18

---

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no blocking dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- All paths are relative to repository root `F:\Projects\Rentier\rentier\`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: String resource keys that all Desktop UI phases depend on. No business logic — can be done before any other phase.

- [ ] T001 Add 12 dashboard string resource keys to `src/Rentier.Desktop/Resources/Strings.resx`: `Nav_Dashboard="Dashboard"`, `Dashboard_Summary_Init="Init"`, `Dashboard_Summary_Filed="Filed"`, `Dashboard_Summary_Paid="Paid"`, `Dashboard_Summary_Unpaid="Unpaid"`, `Dashboard_Summary_LastSync="Last sync"`, `Dashboard_NoSync="No sync performed"`, `Dashboard_Overdue_Header="Overdue Filings"`, `Dashboard_Upcoming_Header="Upcoming Deadlines (30 days)"`, `Dashboard_Empty_Upcoming="No upcoming deadlines"`, `Dashboard_Empty_Overdue="No overdue filings"`, `Dashboard_Error_Load="Failed to load dashboard data"`

---

## Phase 2: Foundational (Application Contracts — Blocking All User Stories)

**Purpose**: Application-layer contracts that MUST be in place before any infrastructure or desktop task can compile. No EF migration is needed — all queries operate on existing `Filings` and `Mailboxes` tables.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

- [ ] T002 Add three method signatures to `src/Rentier.Application/Repositories/IFilingRepository.cs`: `Task<IReadOnlyList<Filing>> GetUpcomingAsync(DateOnly today, int days, CancellationToken ct = default)` (Status ∈ {Init,Filed} AND FilingDeadline ∈ [today, today+days], ASC); `Task<IReadOnlyList<Filing>> GetOverdueAsync(DateOnly today, CancellationToken ct = default)` (Status ∈ {Init,Filed} AND FilingDeadline < today, ASC); `Task<(int InitCount, int FiledCount, int PaidCount, decimal TotalUnpaidRsd)> GetFilingStatsAsync(CancellationToken ct = default)` (full-table aggregates; TotalUnpaidRsd = SUM TaxPayableRsd WHERE Status != Paid)
- [ ] T003 [P] Create `src/Rentier.Application/DTOs/UpcomingDeadlineDto.cs` — namespace `Rentier.Application.DTOs`; `public sealed record UpcomingDeadlineDto(Guid Id, string PayingEntity, DateOnly FilingDeadline, decimal TaxPayableRsd, FilingStatus Status, IncomeType IncomeType)` — requires `using Rentier.Domain.Entities; using Rentier.Domain.Enums;`
- [ ] T004 [P] Create `src/Rentier.Application/DTOs/OverdueFilingDto.cs` — namespace `Rentier.Application.DTOs`; `public sealed record OverdueFilingDto(Guid Id, string PayingEntity, DateOnly FilingDeadline, decimal TaxPayableRsd, FilingStatus Status)` — requires `using Rentier.Domain.Entities;`
- [ ] T005 [P] Create `src/Rentier.Application/DTOs/DashboardDto.cs` — namespace `Rentier.Application.DTOs`; `public sealed record DashboardDto(IReadOnlyList<UpcomingDeadlineDto> UpcomingDeadlines, IReadOnlyList<OverdueFilingDto> OverdueFilings, int InitCount, int FiledCount, int PaidCount, decimal TotalUnpaidRsd, DateOnly? LastSyncDate)`
- [ ] T006 Create `src/Rentier.Application/Queries/GetDashboardQuery.cs` — namespace `Rentier.Application.Queries`; `public sealed record GetDashboardQuery()` — no parameters; handler always computes `today = DateOnly.FromDateTime(DateTime.Today)` internally
- [ ] T007 Create `src/Rentier.Application/Handlers/GetDashboardQueryHandler.cs` — namespace `Rentier.Application.Handlers`; implements `IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>`; constructor: `IFilingRepository _filingRepo, IMailboxRepository _mailboxRepo`; `HandleAsync`: (1) `today = DateOnly.FromDateTime(DateTime.Today)`, (2) call `GetUpcomingAsync(today, 30, ct)`, `GetOverdueAsync(today, ct)`, `GetFilingStatsAsync(ct)` (may run concurrently via `Task.WhenAll`), (3) resolve `LastSyncDate`: `(await _mailboxRepo.GetAllAsync(ct)).Select(m => m.Cursor.LastSyncDate).Where(d => d.HasValue).Select(d => d!.Value).OrderByDescending(d => d).FirstOrDefault()` → `DateOnly? lastSyncDate = (result == default) ? null : result`, (4) map `Filing → UpcomingDeadlineDto` via `new UpcomingDeadlineDto(f.Id, f.PayingEntity, f.FilingDeadline, f.TaxPayableRsd, f.Status, f.IncomeType)`, (5) map `Filing → OverdueFilingDto` via `new OverdueFilingDto(f.Id, f.PayingEntity, f.FilingDeadline, f.TaxPayableRsd, f.Status)`, (6) assemble and `return Result<DashboardDto, Error>.Success(new DashboardDto(...))`, (7) wrap entire body in `try/catch(Exception ex)` returning `Result<DashboardDto, Error>.Failure(Error.Infrastructure(ex.Message))`; empty database returns success with empty lists, zero counts, `0m` total, `null` lastSync — NOT an error

**Checkpoint**: Application contracts are in place. Infrastructure and Desktop work can now begin in parallel.

---

## Phase 3: US5 (P1) — Dashboard Navigation Entry

**Goal**: Dashboard is the first sidebar item and the default screen on application startup. Navigating away and back refreshes the dashboard.

**Independent Test**: Launch the application; verify the sidebar shows "Dashboard" at index 0 followed by Filings, Reports, Settings; verify Dashboard view loads immediately with loading indicator then data (or empty state); click Filings, then click Dashboard — verify dashboard reloads.

- [ ] T008 [US5] Register `GetDashboardQueryHandler` in `src/Rentier.Desktop/Composition/CompositionRoot.cs` — add inside `AddDesktopServices()`: `services.AddTransient<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>, GetDashboardQueryHandler>();`; add using statements `Rentier.Application.Queries`, `Rentier.Application.DTOs`, `Rentier.Application.Handlers`; do NOT register `DashboardViewModel` — it is constructed via `ActivatorUtilities.CreateInstance` in `MainWindowViewModel`; use `AddTransient` only (never `AddScoped`)
- [ ] T009 [US5] Update `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs` — (1) add `Action navigateToFilings = () => { var e = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel); if (e is not null) SelectedEntry = e; };` before `reportsVm` creation, (2) add `var dashboardVm = ActivatorUtilities.CreateInstance<DashboardViewModel>(provider, navigateToFilings);` (uses the no-reportId delegate — distinct from the existing `Action<Guid>` used by `ReportsViewModel`), (3) update `NavigationEntries` to `new List<NavigationEntry> { new(Strings.Nav_Dashboard, dashboardVm), new(Strings.Nav_Filings, filingsVm), new(Strings.Nav_Reports, reportsVm), new(Strings.Nav_Settings, settingsVm) }`, (4) set `_selectedEntry = NavigationEntries[0]` and `_currentViewModel = dashboardVm` as startup defaults (replacing current `filingsVm` defaults); keep existing `Action<Guid> navigateToFilings` for `ReportsViewModel` — rename to `navigateToFilingsWithReport` to avoid name collision; use `this.RaiseAndSetIfChanged` for all property setters (already present)
- [ ] T010 [US5] Create `src/Rentier.Desktop/Views/DashboardView.axaml` — `ReactiveUserControl<DashboardViewModel>` with `x:CompileBindings="False"`; root `ScrollViewer` containing a `StackPanel Spacing="12" Margin="16"`; ProgressBar: `IsIndeterminate="True" IsVisible="{Binding IsLoading}" Height="4"`; error banner: `Border IsVisible="{Binding ErrorMessage, Converter={StaticResource NullToBoolConverter}}"` containing `DockPanel` with dismiss `Button Command="{Binding ClearErrorCommand}" DockPanel.Dock="Right"` and `TextBlock Text="{Binding ErrorMessage}"`; include comment placeholders `<!-- Summary Cards (T020) -->`, `<!-- Overdue Section (T018) -->`, `<!-- Upcoming DataGrid (T015) -->` so later tasks can target exact insertion points; register namespace `xmlns:vm="using:Rentier.Desktop.ViewModels"` and `xmlns:resources="using:Rentier.Desktop.Resources"`
- [ ] T011 [P] [US5] Create `src/Rentier.Desktop/Views/DashboardView.axaml.cs` — `public partial class DashboardView : ReactiveUserControl<DashboardViewModel>`; constructor calls `InitializeComponent()`; add `this.WhenActivated(d => { })` stub to wire Avalonia activation so `IActivatableViewModel.WhenActivated` fires correctly when the view is attached

**Checkpoint**: Application launches with Dashboard as default screen. Loading indicator visible on startup.

---

## Phase 4: US1 (P1) — View Upcoming Filing Deadlines

**Goal**: DataGrid shows filings with status Init or Filed whose `FilingDeadline` falls within the next 30 days, sorted ascending. Clicking a row navigates to the Filings pane (no detail view).

**Independent Test**: Seed filings at today+5 (Init), today+15 (Filed), today+35 (Init), today+5 (Paid). Navigate to Dashboard; verify DataGrid has exactly 2 rows (today+5 Init and today+15 Filed), sorted ascending; Paid and beyond-30-day excluded. Click a row; verify sidebar selection switches to Filings.

- [ ] T012 [US1] Implement `GetUpcomingAsync` in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` — add after existing methods: `public async Task<IReadOnlyList<Filing>> GetUpcomingAsync(DateOnly today, int days, CancellationToken ct = default) { var end = today.AddDays(days); var list = await _db.Filings.AsNoTracking().Where(f => (f.Status == FilingStatus.Init || f.Status == FilingStatus.Filed) && f.FilingDeadline >= today && f.FilingDeadline <= end).OrderBy(f => f.FilingDeadline).ToListAsync(ct); return list.AsReadOnly(); }` — no EF migration; reads existing `Filings` table; `AsNoTracking()` required for read-only queries
- [ ] T013 [P] [US1] Create `src/Rentier.Desktop/ViewModels/UpcomingDeadlineRowViewModel.cs` — namespace `Rentier.Desktop.ViewModels`; `sealed class UpcomingDeadlineRowViewModel` (NOT `ReactiveObject` — display-only snapshot); auto properties: `Guid Id`, `string PayingEntity`, `DateOnly FilingDeadline`, `string DeadlineDisplay`, `decimal TaxPayableRsd`, `string TaxPayableDisplay`, `FilingStatus Status`, `IncomeType IncomeType`; `public static UpcomingDeadlineRowViewModel From(UpcomingDeadlineDto dto) => new() { Id = dto.Id, PayingEntity = dto.PayingEntity, FilingDeadline = dto.FilingDeadline, DeadlineDisplay = dto.FilingDeadline.ToString("yyyy-MM-dd"), TaxPayableRsd = dto.TaxPayableRsd, TaxPayableDisplay = dto.TaxPayableRsd.ToString("N2", CultureInfo.InvariantCulture) + " RSD", Status = dto.Status, IncomeType = dto.IncomeType };`; requires `using System.Globalization;`
- [ ] T014 [US1] Create `src/Rentier.Desktop/ViewModels/DashboardViewModel.cs` — namespace `Rentier.Desktop.ViewModels`; `sealed class DashboardViewModel : ReactiveObject, IActivatableViewModel`; constructor: `IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>> handler, Action navigateToFilings, IScheduler? scheduler = null`; store as `_handler`, `_navigateToFilings`, `_scheduler`; `public ViewModelActivator Activator { get; } = new()`; backing fields + `this.RaiseAndSetIfChanged(ref _field, value)` (NO `[Reactive]`) for all mutable props: `bool IsLoading` (default false), `string? ErrorMessage` (default null), `bool HasData` (default false), `ObservableCollection<UpcomingDeadlineRowViewModel> UpcomingDeadlines` (init `new()`), `ObservableCollection<OverdueFilingRowViewModel> OverdueFilings` (init `new()`), `int InitCount`, `int FiledCount`, `int PaidCount`, `string TotalUnpaidDisplay` (default `"0.00 RSD"`), `string LastSyncDisplay` (default `""`); computed read-only: `bool HasOverdueFilings => OverdueFilings.Count > 0`, `int OverdueCount => OverdueFilings.Count`, `bool IsEmpty => UpcomingDeadlines.Count == 0 && !IsLoading`; `LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync, outputScheduler: _scheduler ?? RxApp.MainThreadScheduler)`; `NavigateToFilingsCommand = ReactiveCommand.Create(() => _navigateToFilings(), outputScheduler: _scheduler ?? RxApp.MainThreadScheduler)`; `ClearErrorCommand = ReactiveCommand.Create(() => { ErrorMessage = null; }, outputScheduler: _scheduler ?? RxApp.MainThreadScheduler)`; `this.WhenActivated(d => { LoadCommand.Execute().Subscribe().DisposeWith(d); LoadCommand.ThrownExceptions.Subscribe(ex => ErrorMessage = ex.Message).DisposeWith(d); })`; `private async Task LoadAsync(CancellationToken ct) { IsLoading = true; ErrorMessage = null; try { var result = await _handler.HandleAsync(new GetDashboardQuery(), ct); if (!result.IsSuccess) { ErrorMessage = result.Error.Message; return; } var dto = result.Value; UpcomingDeadlines.Clear(); foreach (var u in dto.UpcomingDeadlines) UpcomingDeadlines.Add(UpcomingDeadlineRowViewModel.From(u)); OverdueFilings.Clear(); foreach (var o in dto.OverdueFilings) OverdueFilings.Add(OverdueFilingRowViewModel.From(o)); InitCount = dto.InitCount; FiledCount = dto.FiledCount; PaidCount = dto.PaidCount; TotalUnpaidDisplay = dto.TotalUnpaidRsd.ToString("N2", CultureInfo.InvariantCulture) + " RSD"; LastSyncDisplay = dto.LastSyncDate?.ToString("yyyy-MM-dd") ?? Strings.Dashboard_NoSync; HasData = true; RaisePropertyChanged(nameof(HasOverdueFilings)); RaisePropertyChanged(nameof(OverdueCount)); RaisePropertyChanged(nameof(IsEmpty)); } finally { IsLoading = false; } }`; requires `using System.Globalization;`, `using Rentier.Desktop.Resources;`, `using ReactiveUI;`, `using DynamicData.Binding;` (or System.Reactive)
- [ ] T015 [US1] Add Upcoming Deadlines section to `src/Rentier.Desktop/Views/DashboardView.axaml` — replace `<!-- Upcoming DataGrid (T015) -->` placeholder; add `TextBlock Text="{x:Static resources:Strings.Dashboard_Upcoming_Header}" FontWeight="Bold"`; `DataGrid ItemsSource="{Binding UpcomingDeadlines}" IsReadOnly="True" AutoGenerateColumns="False" SelectionMode="Single"` with `DataGridTextColumn`s: Header="Entity" Binding="{Binding PayingEntity}", Header="Deadline" Binding="{Binding DeadlineDisplay}", Header="Type" Binding="{Binding IncomeType}", Header="Status" Binding="{Binding Status}", Header="Tax Payable" Binding="{Binding TaxPayableDisplay}"; wire row-click to NavigateToFilingsCommand via `<DataGrid.Styles><Style Selector="DataGridRow"><Setter Property="Cursor" Value="Hand"/></Style></DataGrid.Styles>` and `<i:Interaction.Triggers>` or `DoubleTapped` event in code-behind calling `NavigateToFilingsCommand.Execute().Subscribe()`; empty-state `TextBlock IsVisible="{Binding IsEmpty}" Text="{x:Static resources:Strings.Dashboard_Empty_Upcoming}"`

**Checkpoint**: Dashboard loads and shows Upcoming Deadlines. Row click switches to Filings pane.

---

## Phase 5: US2 (P1) — Overdue Filings Alert

**Goal**: Prominently highlighted (red) count badge and list of filings with `FilingDeadline < today` and `Status ∈ {Init, Filed}`. Paid filings and deadline-equals-today are excluded.

**Independent Test**: Seed 3 filings with past deadlines in Init/Filed status, 1 Paid with past deadline, 1 Init with deadline = today. Navigate to Dashboard; verify overdue count badge shows 3; verify list has 3 items with red visual treatment; Paid and today-deadline excluded.

- [ ] T016 [US2] Implement `GetOverdueAsync` in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` — `public async Task<IReadOnlyList<Filing>> GetOverdueAsync(DateOnly today, CancellationToken ct = default) { var list = await _db.Filings.AsNoTracking().Where(f => (f.Status == FilingStatus.Init || f.Status == FilingStatus.Filed) && f.FilingDeadline < today).OrderBy(f => f.FilingDeadline).ToListAsync(ct); return list.AsReadOnly(); }` — strict `<` (deadline exactly equals today is NOT overdue); no EF migration
- [ ] T017 [P] [US2] Create `src/Rentier.Desktop/ViewModels/OverdueFilingRowViewModel.cs` — namespace `Rentier.Desktop.ViewModels`; `sealed class OverdueFilingRowViewModel` (NOT `ReactiveObject`); auto properties: `Guid Id`, `string PayingEntity`, `DateOnly FilingDeadline`, `string DeadlineDisplay`, `decimal TaxPayableRsd`, `string TaxPayableDisplay`, `FilingStatus Status`; `public static OverdueFilingRowViewModel From(OverdueFilingDto dto) => new() { Id = dto.Id, PayingEntity = dto.PayingEntity, FilingDeadline = dto.FilingDeadline, DeadlineDisplay = dto.FilingDeadline.ToString("yyyy-MM-dd"), TaxPayableRsd = dto.TaxPayableRsd, TaxPayableDisplay = dto.TaxPayableRsd.ToString("N2", CultureInfo.InvariantCulture) + " RSD", Status = dto.Status };`; requires `using System.Globalization;`
- [ ] T018 [US2] Add Overdue Filings alert section to `src/Rentier.Desktop/Views/DashboardView.axaml` — replace `<!-- Overdue Section (T018) -->` placeholder; add `Border IsVisible="{Binding HasOverdueFilings}" Background="#FFF0F0" BorderBrush="#FFCCCC" BorderThickness="1" Padding="12" CornerRadius="4"`; inside: `DockPanel` with `TextBlock DockPanel.Dock="Top" Foreground="Red" FontWeight="Bold" Text="{Binding OverdueCount, StringFormat='{}{0} Overdue Filings'}"` and `ItemsControl ItemsSource="{Binding OverdueFilings}"` with `DataTemplate` showing `TextBlock` row: `"{Binding PayingEntity} — {Binding DeadlineDisplay} — {Binding TaxPayableDisplay}"`; add separate `TextBlock IsVisible="{Binding HasOverdueFilings, Converter={StaticResource InverseBoolConverter}}" Text="{x:Static resources:Strings.Dashboard_Empty_Overdue}"`

**Checkpoint**: Dashboard shows red alert section when overdue filings exist. Count badge reflects live data.

---

## Phase 6: US3 (P2) — View Summary Statistics

**Goal**: Summary cards show Init count, Filed count, Paid count, and total unpaid tax (`SUM(TaxPayableRsd)` WHERE `Status != Paid`), formatted with `CultureInfo.InvariantCulture`.

**Independent Test**: Seed 3 Init (TaxPayableRsd 1000.00, 2500.50, 500.00), 2 Filed (3000.00, 1500.00), 1 Paid. Navigate to Dashboard; verify Init=3, Filed=2, Paid=1, TotalUnpaid="8,500.50 RSD". Seed empty DB; verify all show 0.

- [ ] T019 [US3] Implement `GetFilingStatsAsync` in `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` — `public async Task<(int InitCount, int FiledCount, int PaidCount, decimal TotalUnpaidRsd)> GetFilingStatsAsync(CancellationToken ct = default) { var filings = await _db.Filings.AsNoTracking().ToListAsync(ct); var initCount = filings.Count(f => f.Status == FilingStatus.Init); var filedCount = filings.Count(f => f.Status == FilingStatus.Filed); var paidCount = filings.Count(f => f.Status == FilingStatus.Paid); var totalUnpaidRsd = filings.Where(f => f.Status != FilingStatus.Paid).Sum(f => f.TaxPayableRsd); return (initCount, filedCount, paidCount, totalUnpaidRsd); }` — single `AsNoTracking` full-table load then in-memory aggregation; acceptable for ≤10,000 filings (single-user SQLite); returns `(0,0,0,0m)` on empty table; no EF migration
- [ ] T020 [US3] Add Summary Cards row to `src/Rentier.Desktop/Views/DashboardView.axaml` — replace `<!-- Summary Cards (T020) -->` placeholder (insert at top of main StackPanel, before overdue section); add `WrapPanel Orientation="Horizontal" ItemWidth="160"`; inside, four `Border` cards each with `StackPanel`: card 1: label `TextBlock Text="{x:Static resources:Strings.Dashboard_Summary_Init}"` + value `TextBlock Text="{Binding InitCount}"`; card 2: label `Strings.Dashboard_Summary_Filed` + `FiledCount`; card 3: label `Strings.Dashboard_Summary_Paid` + `PaidCount`; card 4: label `Strings.Dashboard_Summary_Unpaid` + `TotalUnpaidDisplay`; apply `Padding="12"`, `Margin="0,0,8,8"`, `BorderThickness="1"` to each card `Border`

**Checkpoint**: Summary cards visible at top of dashboard with live counts and total unpaid amount.

---

## Phase 7: US4 (P3) — View Last Sync Timestamp

**Goal**: A card on the dashboard shows the maximum `Cursor.LastSyncDate` across all mailboxes. Shows `Strings.Dashboard_NoSync` when no mailbox exists or all `LastSyncDate` values are null.

**Independent Test**: Configure a mailbox with `Cursor.LastSyncDate = DateOnly(2025, 7, 15)`; navigate to Dashboard; verify "Last sync" card shows "2025-07-15". Remove mailbox (or set cursor date to null); verify card shows "No sync performed".

> **Note**: `LastSyncDisplay` is already populated in `DashboardViewModel.LoadAsync` (T014) — the handler resolves `LastSyncDate` via `_mailboxRepo.GetAllAsync(ct)` LINQ chain and sets `dto.LastSyncDate`. This phase only adds the View card.

- [ ] T021 [US4] Add Last Sync card to Summary Cards row in `src/Rentier.Desktop/Views/DashboardView.axaml` — append a fifth `Border` card to the `WrapPanel` from T020: label `TextBlock Text="{x:Static resources:Strings.Dashboard_Summary_LastSync}"` + value `TextBlock Text="{Binding LastSyncDisplay}"`; same card styling as the four stat cards; `LastSyncDisplay` is bound from `DashboardViewModel.LastSyncDisplay` which is already set in `LoadAsync` as `dto.LastSyncDate?.ToString("yyyy-MM-dd") ?? Strings.Dashboard_NoSync`

**Checkpoint**: All five summary cards visible: Init | Filed | Paid | Unpaid | Last sync.

---

## Phase 8: Tests

**Purpose**: Automated coverage for Application handler, Infrastructure repository methods, and Desktop ViewModel. Tests for all boundary conditions defined in plan.md §Phase 1 Test Strategy.

### Application Handler Tests

- [ ] T022 [P] Create `tests/Rentier.Application.Tests/GetDashboardQueryHandlerTests.cs` — 13 xUnit [Fact] tests; use NSubstitute for `IFilingRepository` and `IMailboxRepository`; cover: (1) `HandleAsync_EmptyDatabase_ReturnsZeroedDto` — both repos return empty lists → `UpcomingDeadlines.Count==0`, `OverdueFilings.Count==0`, all counts `0`, `TotalUnpaidRsd==0m`, `LastSyncDate==null`, `IsSuccess==true`; (2) `HandleAsync_UpcomingFilings_FiltersBy30DayWindow` — stub GetUpcomingAsync returns 3 filings → DTO has 3 items; (3) `HandleAsync_UpcomingFilings_ExcludesPaid` — stub returns 0 for window with Paid filing (repository already excludes it); (4) `HandleAsync_UpcomingFilings_SortedByDeadlineAscending` — stub returns filings in reverse order → assert DTO order matches repository return order (mapping preserves repo sort); (5) `HandleAsync_UpcomingFilings_IncludesTodayBoundary` — today is included (boundary test via stub); (6) `HandleAsync_UpcomingFilings_IncludesDay30Boundary` — today+30 included; (7) `HandleAsync_OverdueFilings_StrictlyBeforeToday` — stub GetOverdueAsync returns 2 → DTO has 2 overdue; (8) `HandleAsync_OverdueFilings_TodayDeadlineExcluded` — repo called with correct `today` value (verify argument via NSubstitute `Arg.Is<DateOnly>`); (9) `HandleAsync_Stats_CountsByStatus` — stub returns (2,1,3,5000m) → dto.InitCount==2, FiledCount==1, PaidCount==3; (10) `HandleAsync_Stats_TotalUnpaidRsd` — TotalUnpaidRsd==5000m; (11) `HandleAsync_LastSyncDate_MaxAcrossMailboxes` — two mailboxes with dates 2025-07-10 and 2025-07-15 → LastSyncDate==DateOnly(2025,7,15); (12) `HandleAsync_LastSyncDate_AllNull` — mailbox with `Cursor.LastSyncDate==null` → `LastSyncDate==null`; (13) `HandleAsync_RepositoryThrows_ReturnsFailure` — GetUpcomingAsync throws `InvalidOperationException("db error")` → `result.IsSuccess==false`, `result.Error.Message` contains "db error"

### Infrastructure Repository Tests

- [ ] T023 [P] Create `tests/Rentier.Infrastructure.Tests/FilingRepositoryDashboardTests.cs` — 10 xUnit [Fact] integration tests using SQLite in-memory `AppDbContext` (same setup pattern as existing infrastructure tests in this project); cover: (1) `GetUpcomingAsync_ReturnsOnlyInitAndFiled` — seed Init+Filed+Paid within window → only Init and Filed returned; (2) `GetUpcomingAsync_ReturnsOnlyWithinWindow` — seed today-1 (excluded), today (included), today+30 (included), today+31 (excluded) → 2 items; (3) `GetUpcomingAsync_OrderedByDeadlineAscending` — seed today+15 then today+5 → returned order is today+5 first; (4) `GetUpcomingAsync_EmptyTable_ReturnsEmptyList` — no filings → empty list, no exception; (5) `GetOverdueAsync_StrictlyBeforeToday` — seed today-1 (included), today (excluded) → 1 item; (6) `GetOverdueAsync_ExcludesPaid` — Paid filing with past deadline → not returned; (7) `GetOverdueAsync_OrderedByDeadlineAscending` — seed today-5 then today-10 → today-10 first; (8) `GetFilingStatsAsync_CorrectCountsByStatus` — seed 3 Init, 2 Filed, 1 Paid → counts match; (9) `GetFilingStatsAsync_TotalUnpaidRsd_SumsNonPaid` — Init(1000m)+Filed(2500.50m)+Paid(500m) → TotalUnpaidRsd==3500.50m; (10) `GetFilingStatsAsync_EmptyTable_ReturnsAllZeros` — empty → (0, 0, 0, 0m)

### Desktop ViewModel Tests

- [ ] T024 [P] Create `tests/Rentier.Desktop.Tests/DashboardViewModelTests.cs` — 8 xUnit [Fact] tests; use NSubstitute for `IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>`; use `ImmediateScheduler.Instance` for synchronous command execution; cover: (1) `LoadCommand_Success_PopulatesAllProperties` — handler returns valid `DashboardDto` → `UpcomingDeadlines.Count` matches, `OverdueFilings.Count` matches, `InitCount`/`FiledCount`/`PaidCount` set, `TotalUnpaidDisplay` set, `LastSyncDisplay` set, `HasData==true`, `ErrorMessage==null`; (2) `LoadCommand_Failure_SetsErrorMessage` — handler returns `Result.Failure(Error.Infrastructure("fail"))` → `ErrorMessage=="fail"`, lists remain empty; (3) `LoadCommand_SetsIsLoadingDuringExecution` — use `TaskCompletionSource` to pause handler; assert `IsLoading==true` during; `IsLoading==false` after; (4) `NavigateToFilingsCommand_Execute_InvokesNavigateDelegate` — set up `Action` spy; call `NavigateToFilingsCommand.Execute().Subscribe()`; assert spy invoked exactly once; (5) `TotalUnpaidDisplay_FormattedWithInvariantCulture` — dto with `TotalUnpaidRsd=8500.50m` → `TotalUnpaidDisplay=="8,500.50 RSD"`; (6) `LastSyncDisplay_WhenLastSyncDateIsNull_ShowsDashboardNoSync` — dto with `LastSyncDate==null` → `LastSyncDisplay==Strings.Dashboard_NoSync`; (7) `LastSyncDisplay_WhenSet_ShowsFormattedDate` — dto with `LastSyncDate==DateOnly(2025,7,15)` → `LastSyncDisplay=="2025-07-15"`; (8) `ClearErrorCommand_Execute_SetsErrorMessageToNull` — set `ErrorMessage="err"`, execute `ClearErrorCommand` → `ErrorMessage==null`

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final build verification, test gate, and architecture compliance audit.

- [ ] T025 [P] Run `dotnet build F:\Projects\Rentier\rentier\Rentier.slnx` and resolve any compile errors — expected areas: new `using` directives in `CompositionRoot.cs`, `MainWindowViewModel.cs`, row ViewModel factory methods, `GetDashboardQueryHandler.cs`; confirm build exits 0 with 0 errors
- [ ] T026 [P] Run `dotnet test F:\Projects\Rentier\rentier\Rentier.slnx --filter "FullyQualifiedName~Dashboard"` and verify all 31 new test cases pass: 13 in `GetDashboardQueryHandlerTests`, 10 in `FilingRepositoryDashboardTests`, 8 in `DashboardViewModelTests`; fix any failing assertions before merge
- [ ] T027 Perform architecture compliance audit across all new and modified files — verify: (a) `DashboardView.axaml` has `x:CompileBindings="False"`, (b) `DashboardViewModel.cs` uses `this.RaiseAndSetIfChanged(ref _field, value)` for all property setters — NO `[Reactive]` attribute anywhere, (c) `CompositionRoot.cs` uses `AddTransient` only — NO `AddScoped`, (d) no `ExecuteDeleteAsync` introduced (this feature is read-only — no delete operations), (e) all `decimal` display formatting uses `CultureInfo.InvariantCulture`, (f) no `DateTime` in Application or Domain layers — only `DateOnly` and `DateOnly?`, (g) `GetDashboardQueryHandler` does NOT reference any Infrastructure types directly, (h) `DashboardViewModel` does NOT reference `IFilingRepository` or `IMailboxRepository` directly (only `IQueryHandler`)

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)          — No deps. Start immediately.
Phase 2 (Foundational)   — Depends on Phase 1. BLOCKS all user stories.
Phase 3 (US5)            — Depends on Phase 2. BLOCKS integration testing of all other stories.
Phase 4 (US1)            — Depends on Phase 2. Can start in parallel with Phase 3.
Phase 5 (US2)            — Depends on Phase 2 + T013 (OverdueFilingRowViewModel ← T017 is parallel).
Phase 6 (US3)            — Depends on Phase 2.
Phase 7 (US4)            — Depends on Phase 2 (LastSyncDate derivation is in T007/handler).
Phase 8 (Tests)          — Depends on Phases 2–7. T022/T023/T024 can run in parallel.
Phase 9 (Polish)         — Depends on Phase 8. T025/T026 can run in parallel.
```

### User Story Dependencies

| Story | Priority | Depends On | Blocks |
|-------|----------|------------|--------|
| US5 — Navigation Entry | P1 | Phase 2 (Foundational) | None |
| US1 — Upcoming Deadlines | P1 | Phase 2 | None |
| US2 — Overdue Alert | P1 | Phase 2 + T014 (DashboardViewModel) | None |
| US3 — Summary Stats | P2 | Phase 2 + T014 | None |
| US4 — Last Sync | P3 | Phase 2 + T020 (Summary Cards view) | None |

> US2 depends on T014 because `OverdueFilings` collection lives in `DashboardViewModel`. US3/US4 require the ViewModel for bindings but their infrastructure tasks (T019, T021) are independent.

### Within Each Phase

1. **Phase 2**: T002 first (interface extension) → T003/T004/T005/T006 in parallel → T007 last (handler needs all DTOs and query)
2. **Phase 3**: T008 → T009 (needs DashboardViewModel type from T014 — compile stub first) → T010/T011 parallel
3. **Phase 4**: T012/T013 parallel → T014 (ViewModel needs RowVM from T013) → T015 (View needs VM from T014)
4. **Phase 5**: T016/T017 parallel → T018 (View update)
5. **Phase 6**: T019 → T020 (View update)
6. **Phase 7**: T021 only (handler already done in T007, VM in T014)

---

## Parallel Execution Examples

### Phase 2 — Application Contracts

```
Parallel group A (after T002):
  Task: "Create UpcomingDeadlineDto.cs"   [T003]
  Task: "Create OverdueFilingDto.cs"       [T004]
  Task: "Create DashboardDto.cs"           [T005]
  Task: "Create GetDashboardQuery.cs"      [T006]
Sequential:
  Task: "Create GetDashboardQueryHandler.cs" [T007]  ← requires T003-T006
```

### Phase 4 — US1 Upcoming Deadlines

```
Parallel group:
  Task: "Implement GetUpcomingAsync in FilingRepository.cs"  [T012]
  Task: "Create UpcomingDeadlineRowViewModel.cs"             [T013]
Sequential:
  Task: "Create DashboardViewModel.cs"   [T014]  ← requires T013
  Task: "Add Upcoming DataGrid to DashboardView.axaml" [T015]  ← requires T014
```

### Phase 8 — Tests

```
Parallel group (all independent):
  Task: "Create GetDashboardQueryHandlerTests.cs"      [T022]
  Task: "Create FilingRepositoryDashboardTests.cs"     [T023]
  Task: "Create DashboardViewModelTests.cs"            [T024]
```

---

## Implementation Strategy

### MVP First (User Stories US5 + US1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational — CRITICAL, blocks everything (T002–T007)
3. Complete Phase 3: US5 Navigation Entry (T008–T011)
4. Complete Phase 4: US1 Upcoming Deadlines (T012–T015)
5. **STOP and VALIDATE**: Launch app → Dashboard appears by default → DataGrid shows upcoming filings → Row click navigates to Filings
6. Ship this increment as functional MVP

### Incremental Delivery

```
MVP:    Phase 1 + Phase 2 + Phase 3 + Phase 4  → Dashboard visible, upcoming grid functional
+US2:   Phase 5  → Overdue alert section added
+US3:   Phase 6  → Summary cards added
+US4:   Phase 7  → Last sync timestamp added
+Tests: Phase 8  → Full coverage (can run in parallel with US3/US4)
Final:  Phase 9  → Build clean, tests green, architecture compliant
```

### Summary

| Metric | Value |
|--------|-------|
| Total tasks | 27 |
| Phase 1 (Setup) | 1 |
| Phase 2 (Foundational) | 6 |
| Phase 3 (US5) | 4 |
| Phase 4 (US1) | 4 |
| Phase 5 (US2) | 3 |
| Phase 6 (US3) | 2 |
| Phase 7 (US4) | 1 |
| Phase 8 (Tests) | 3 |
| Phase 9 (Polish) | 3 |
| Parallelizable tasks [P] | 12 |
| New test cases | 31 (13 handler + 10 infra + 8 VM) |
| New files | 12 |
| Modified files | 5 |
| EF migrations | 0 |

---

## Notes

- **No EF migration**: All 3 new repository methods query existing `Filings` and `Mailboxes` tables. Confirmed FR-018.
- **`[P]` tasks** involve different files with no shared write conflicts — safe to run in parallel.
- **`[Story]` labels** map each task to the acceptance scenario it satisfies in `spec.md`.
- **`DashboardViewModel` is NOT DI-registered** — use `ActivatorUtilities.CreateInstance<DashboardViewModel>(provider, navigateToFilings)` in `MainWindowViewModel` (mirrors `ReportsViewModel` pattern).
- **`MailboxCursor.LastSyncDate`** field confirmed as `DateOnly?` from `Rentier.Domain.ValueObjects.MailboxCursor` record (see `Mailbox.cs`).
- **`FilingStatus` values**: `Init=0`, `Filed=1`, `Paid=2` — `GetUpcomingAsync`/`GetOverdueAsync` filter on `Init` and `Filed`; `GetFilingStatsAsync` counts all three.
- Commit after each checkpoint (end of phases 3, 4, 5, 6, 7) to maintain a working main branch.
