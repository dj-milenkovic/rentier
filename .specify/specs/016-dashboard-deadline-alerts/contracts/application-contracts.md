# Application Layer Contracts: Dashboard with Deadline Alerts (016)

**Feature**: `016-dashboard-deadline-alerts`  
**Created**: 2025-07-18  
**Scope**: Public interfaces exposed by the Application layer consumed by Desktop

---

## Contract Overview

The Desktop layer (`DashboardViewModel`) communicates with the Application layer through a single
typed query handler interface. No direct repository access or infrastructure calls are permitted
from the Desktop.

```
DashboardViewModel
  └── IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>
```

---

## Query Contract

### `GetDashboardQuery` → `DashboardDto`

**Interface**: `IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>`  
**Handler**: `GetDashboardQueryHandler`  
**Namespace**: `Rentier.Application.Handlers`

#### Input

```csharp
public sealed record GetDashboardQuery();
// No parameters — always loads relative to DateOnly.FromDateTime(DateTime.Today)
```

#### Output (success)

```csharp
public sealed record DashboardDto(
    IReadOnlyList<UpcomingDeadlineDto> UpcomingDeadlines,
    IReadOnlyList<OverdueFilingDto> OverdueFilings,
    int InitCount,
    int FiledCount,
    int PaidCount,
    decimal TotalUnpaidRsd,
    DateOnly? LastSyncDate);

public sealed record UpcomingDeadlineDto(
    Guid Id,
    string PayingEntity,
    DateOnly FilingDeadline,
    decimal TaxPayableRsd,
    FilingStatus Status,
    IncomeType IncomeType);

public sealed record OverdueFilingDto(
    Guid Id,
    string PayingEntity,
    DateOnly FilingDeadline,
    decimal TaxPayableRsd,
    FilingStatus Status);
```

#### Output (failure)

```csharp
Result<DashboardDto, Error>.Failure(Error.Infrastructure("..."))
// Only for unexpected repository/infrastructure exceptions
// An empty database is NOT an error — returns zeroed DashboardDto with empty lists
```

#### Behaviour

- Computes `today = DateOnly.FromDateTime(DateTime.Today)` at the start of the handler.
- Calls three repository methods concurrently (all read-only, no shared state):
  1. `IFilingRepository.GetUpcomingAsync(today, 30, ct)` → maps each `Filing` → `UpcomingDeadlineDto`
  2. `IFilingRepository.GetOverdueAsync(today, ct)` → maps each `Filing` → `OverdueFilingDto`
  3. `IFilingRepository.GetFilingStatsAsync(ct)` → provides `InitCount`, `FiledCount`, `PaidCount`, `TotalUnpaidRsd`
- Resolves `LastSyncDate`:
  1. Calls `IMailboxRepository.GetAllAsync(ct)`
  2. Selects max `Cursor.LastSyncDate` across all mailboxes where `LastSyncDate.HasValue`
  3. Returns `null` if no mailbox exists or all `LastSyncDate` values are null
- Returns `Result.Success(dashboardDto)` with assembled data.
- Returns `Result.Failure(Error.Infrastructure(ex.Message))` if any repository call throws.
- **Empty database**: Returns success with empty lists, zero counts, `0m` total, `null` last sync.
- **No pagination**: Upcoming deadlines bounded by 30-day window; overdue list expected small.

---

## Repository Contract Extensions

### `IFilingRepository.GetUpcomingAsync`

**Namespace**: `Rentier.Application.Repositories`

```csharp
Task<IReadOnlyList<Filing>> GetUpcomingAsync(
    DateOnly today, int days, CancellationToken ct = default);
```

| Parameter | Type | Contract |
|-----------|------|----------|
| `today` | `DateOnly` | Lower bound (inclusive); computed by handler |
| `days` | `int` | Range size; handler passes `30` |
| `ct` | `CancellationToken` | Respected by all async operations |
| Returns | `IReadOnlyList<Filing>` | `Status ∈ {Init, Filed}` AND `FilingDeadline ∈ [today, today+days]`, ordered `FilingDeadline ASC`; empty list if none |

### `IFilingRepository.GetOverdueAsync`

```csharp
Task<IReadOnlyList<Filing>> GetOverdueAsync(
    DateOnly today, CancellationToken ct = default);
```

| Parameter | Type | Contract |
|-----------|------|----------|
| `today` | `DateOnly` | Upper bound (exclusive); computed by handler |
| `ct` | `CancellationToken` | Respected by all async operations |
| Returns | `IReadOnlyList<Filing>` | `Status ∈ {Init, Filed}` AND `FilingDeadline < today`, ordered `FilingDeadline ASC`; empty list if none |

### `IFilingRepository.GetFilingStatsAsync`

```csharp
Task<(int InitCount, int FiledCount, int PaidCount, decimal TotalUnpaidRsd)> GetFilingStatsAsync(
    CancellationToken ct = default);
```

| Parameter | Type | Contract |
|-----------|------|----------|
| `ct` | `CancellationToken` | Respected by all async operations |
| Returns `InitCount` | `int` | Count of filings with `Status == Init` |
| Returns `FiledCount` | `int` | Count of filings with `Status == Filed` |
| Returns `PaidCount` | `int` | Count of filings with `Status == Paid` |
| Returns `TotalUnpaidRsd` | `decimal` | Sum of `TaxPayableRsd` where `Status != Paid`; `0m` if none |

---

## DI Registration Contract

### CompositionRoot.AddDesktopServices additions

```csharp
// Query handler registration
services.AddTransient<
    IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>,
    GetDashboardQueryHandler>();

// DashboardViewModel is NOT registered in DI
// Constructed via ActivatorUtilities.CreateInstance in MainWindowViewModel (same pattern as ReportsViewModel)
```

### MainWindowViewModel Constructor Change

```csharp
// BEFORE (current):
public MainWindowViewModel(
    FilingsViewModel filingsVm,
    IServiceProvider provider,
    SettingsViewModel settingsVm)

// AFTER:
public MainWindowViewModel(
    FilingsViewModel filingsVm,
    IServiceProvider provider,
    SettingsViewModel settingsVm)
{
    // New: create navigateToFilings delegate
    Action navigateToFilings = () =>
    {
        var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
        if (filingsEntry is not null)
            SelectedEntry = filingsEntry;
    };

    // New: create DashboardViewModel via ActivatorUtilities (same pattern as ReportsViewModel)
    var dashboardVm = ActivatorUtilities.CreateInstance<DashboardViewModel>(
        provider, navigateToFilings);

    // Existing: create ReportsViewModel (unchanged)
    Action<Guid> navigateToFilingsWithReport = reportId => { ... };
    var reportsVm = ActivatorUtilities.CreateInstance<ReportsViewModel>(
        provider, navigateToFilingsWithReport);

    // Updated: Dashboard at index 0
    NavigationEntries = new List<NavigationEntry>
    {
        new(Strings.Nav_Dashboard, dashboardVm),    // NEW — index 0
        new(Strings.Nav_Filings, filingsVm),         // was index 0, now index 1
        new(Strings.Nav_Reports, reportsVm),         // was index 1, now index 2
        new(Strings.Nav_Settings, settingsVm)        // was index 2, now index 3
    };

    _selectedEntry = NavigationEntries[0];       // Dashboard is default
    _currentViewModel = dashboardVm;             // Dashboard is default
}
```

---

## String Resource Contract

### New `Strings.resx` keys

| Key | Example Value | Used By |
|-----|---------------|---------|
| `Nav_Dashboard` | `"Dashboard"` | `NavigationEntry` label |
| `Dashboard_Summary_Init` | `"Init"` | Summary card label |
| `Dashboard_Summary_Filed` | `"Filed"` | Summary card label |
| `Dashboard_Summary_Paid` | `"Paid"` | Summary card label |
| `Dashboard_Summary_Unpaid` | `"Unpaid"` | Summary card label |
| `Dashboard_Summary_LastSync` | `"Last sync"` | Summary card label |
| `Dashboard_NoSync` | `"No sync performed"` | When LastSyncDate is null |
| `Dashboard_Overdue_Header` | `"Overdue Filings"` | Section header |
| `Dashboard_Upcoming_Header` | `"Upcoming Deadlines (30 days)"` | Section header |
| `Dashboard_Empty_Upcoming` | `"No upcoming deadlines"` | Empty state message |
| `Dashboard_Empty_Overdue` | `"No overdue filings"` | Empty state message |
| `Dashboard_Error_Load` | `"Failed to load dashboard data"` | Error banner prefix |
