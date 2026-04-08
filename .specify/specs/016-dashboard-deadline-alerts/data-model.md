# Data Model: Dashboard with Deadline Alerts (016)

**Feature**: `016-dashboard-deadline-alerts`  
**Created**: 2025-07-18  
**Layers affected**: Application · Infrastructure · Desktop

---

## 1. Domain Layer — No Changes

No new entities, value objects, or domain rules. The dashboard is a pure read-only view over existing `Filing` and `Mailbox` entities.

**Existing entities consumed** (read-only):

| Entity | Key Fields for Dashboard |
|--------|------------------------|
| `Filing` | `Id`, `Status` (FilingStatus), `PayingEntity`, `IncomeType`, `FilingDeadline` (DateOnly), `TaxPayableRsd` (decimal) |
| `Mailbox` | `Cursor` (MailboxCursor) |
| `MailboxCursor` (VO) | `LastSyncDate` (DateOnly?) |

**Filing state machine** (unchanged, consumed read-only):

```
FilingStatus.Init ──AdvanceStatus(Filed)──► FilingStatus.Filed
FilingStatus.Filed──AdvanceStatus(Paid)───► FilingStatus.Paid
```

---

## 2. Application DTOs

### `DashboardDto` (root read model)

| Field | Type | Source |
|-------|------|--------|
| `UpcomingDeadlines` | `IReadOnlyList<UpcomingDeadlineDto>` | `IFilingRepository.GetUpcomingAsync` |
| `OverdueFilings` | `IReadOnlyList<OverdueFilingDto>` | `IFilingRepository.GetOverdueAsync` |
| `InitCount` | `int` | `IFilingRepository.GetFilingStatsAsync` |
| `FiledCount` | `int` | `IFilingRepository.GetFilingStatsAsync` |
| `PaidCount` | `int` | `IFilingRepository.GetFilingStatsAsync` |
| `TotalUnpaidRsd` | `decimal` | `IFilingRepository.GetFilingStatsAsync` |
| `LastSyncDate` | `DateOnly?` | `IMailboxRepository.GetAllAsync` → max `Cursor.LastSyncDate` |

### `UpcomingDeadlineDto` (row in upcoming deadlines grid)

| Field | Type | Source |
|-------|------|--------|
| `Id` | `Guid` | `Filing.Id` |
| `PayingEntity` | `string` | `Filing.PayingEntity` |
| `FilingDeadline` | `DateOnly` | `Filing.FilingDeadline` |
| `TaxPayableRsd` | `decimal` | `Filing.TaxPayableRsd` |
| `Status` | `FilingStatus` | `Filing.Status` |
| `IncomeType` | `IncomeType` | `Filing.IncomeType` |

### `OverdueFilingDto` (row in overdue filings list)

| Field | Type | Source |
|-------|------|--------|
| `Id` | `Guid` | `Filing.Id` |
| `PayingEntity` | `string` | `Filing.PayingEntity` |
| `FilingDeadline` | `DateOnly` | `Filing.FilingDeadline` |
| `TaxPayableRsd` | `decimal` | `Filing.TaxPayableRsd` |
| `Status` | `FilingStatus` | `Filing.Status` |

---

## 3. Application Query

### `GetDashboardQuery`

| Field | Type | Default |
|-------|------|---------|
| _(none)_ | — | — |

**Returns**: `Result<DashboardDto, Error>`  
**Behaviour**: Always loads relative to `DateOnly.FromDateTime(DateTime.Today)`. No parameters required.

---

## 4. Repository Interface Extensions

### `IFilingRepository` additions

```csharp
/// <summary>
/// Returns filings with Status in {Init, Filed} and FilingDeadline in [today, today+days],
/// ordered by FilingDeadline ascending.
/// </summary>
Task<IReadOnlyList<Filing>> GetUpcomingAsync(
    DateOnly today, int days, CancellationToken ct = default);

/// <summary>
/// Returns filings with Status in {Init, Filed} and FilingDeadline strictly before today,
/// ordered by FilingDeadline ascending.
/// </summary>
Task<IReadOnlyList<Filing>> GetOverdueAsync(
    DateOnly today, CancellationToken ct = default);

/// <summary>
/// Returns aggregate filing statistics: counts by status and total unpaid RSD.
/// TotalUnpaidRsd = sum of TaxPayableRsd where Status != Paid.
/// </summary>
Task<(int InitCount, int FiledCount, int PaidCount, decimal TotalUnpaidRsd)> GetFilingStatsAsync(
    CancellationToken ct = default);
```

**Query semantics**:

| Method | Filter | Sort | Returns |
|--------|--------|------|---------|
| `GetUpcomingAsync` | `Status IN (Init, Filed) AND FilingDeadline >= today AND FilingDeadline <= today.AddDays(days)` | `FilingDeadline ASC` | `IReadOnlyList<Filing>` (empty if none) |
| `GetOverdueAsync` | `Status IN (Init, Filed) AND FilingDeadline < today` | `FilingDeadline ASC` | `IReadOnlyList<Filing>` (empty if none) |
| `GetFilingStatsAsync` | Full table scan (counts all statuses) | N/A | Tuple of 4 aggregates |

---

## 5. Database Schema — No Changes

**No new migration.** All queries operate on existing columns in the `Filings` and `Mailboxes` tables. FR-018 explicitly prohibits schema changes.

Existing `Filings` table columns consumed:

| Column | Type | Used For |
|--------|------|----------|
| `Id` | TEXT (GUID) | DTO mapping |
| `Status` | INTEGER | Filter (Init=0, Filed=1, Paid=2), counts |
| `PayingEntity` | TEXT | DTO mapping |
| `IncomeType` | INTEGER | DTO mapping (Dividend=0, Interest=1) |
| `FilingDeadline` | TEXT (DateOnly) | Date-range filter, sort, DTO mapping |
| `TaxPayableRsd` | DECIMAL(18,2) | Sum aggregation, DTO mapping |

Existing `Mailboxes` table columns consumed:

| Column | Type | Used For |
|--------|------|----------|
| `Cursor` | TEXT (JSON) | Deserialised to `MailboxCursor` → `LastSyncDate` |

---

## 6. Desktop ViewModel State

### `DashboardViewModel` (reactive, activatable)

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `IsLoading` | `bool` | `false` | Shows loading indicator |
| `ErrorMessage` | `string?` | `null` | Shows error banner |
| `UpcomingDeadlines` | `ObservableCollection<UpcomingDeadlineRowViewModel>` | empty | Bound to DataGrid |
| `OverdueFilings` | `ObservableCollection<OverdueFilingRowViewModel>` | empty | Bound to overdue list |
| `InitCount` | `int` | `0` | Summary card |
| `FiledCount` | `int` | `0` | Summary card |
| `PaidCount` | `int` | `0` | Summary card |
| `TotalUnpaidDisplay` | `string` | `"0.00 RSD"` | Formatted via `CultureInfo.InvariantCulture` |
| `LastSyncDisplay` | `string` | `""` | "yyyy-MM-dd" or localised "no sync" message |
| `HasOverdueFilings` | `bool` | `false` | Controls overdue section visibility |
| `OverdueCount` | `int` | `0` | Badge count |
| `IsEmpty` | `bool` | `true` | No upcoming deadlines |

### Commands

| Command | Type | Trigger |
|---------|------|---------|
| `LoadCommand` | `ReactiveCommand<Unit, Unit>` | `WhenActivated` (auto-load) |
| `NavigateToFilingsCommand` | `ReactiveCommand<Unit, Unit>` | Row click in DataGrid |
| `ClearErrorCommand` | `ReactiveCommand<Unit, Unit>` | Error banner dismiss |

### Row ViewModels (display-only snapshots)

#### `UpcomingDeadlineRowViewModel`

| Property | Type | Source |
|----------|------|--------|
| `Id` | `Guid` | `UpcomingDeadlineDto.Id` |
| `PayingEntity` | `string` | `UpcomingDeadlineDto.PayingEntity` |
| `FilingDeadline` | `DateOnly` | `UpcomingDeadlineDto.FilingDeadline` |
| `DeadlineDisplay` | `string` | `FilingDeadline.ToString("yyyy-MM-dd")` |
| `TaxPayableRsd` | `decimal` | `UpcomingDeadlineDto.TaxPayableRsd` |
| `TaxPayableDisplay` | `string` | `$"{TaxPayableRsd:N2} RSD"` (InvariantCulture) |
| `Status` | `FilingStatus` | `UpcomingDeadlineDto.Status` |
| `IncomeType` | `IncomeType` | `UpcomingDeadlineDto.IncomeType` |

#### `OverdueFilingRowViewModel`

| Property | Type | Source |
|----------|------|--------|
| `Id` | `Guid` | `OverdueFilingDto.Id` |
| `PayingEntity` | `string` | `OverdueFilingDto.PayingEntity` |
| `FilingDeadline` | `DateOnly` | `OverdueFilingDto.FilingDeadline` |
| `DeadlineDisplay` | `string` | `FilingDeadline.ToString("yyyy-MM-dd")` |
| `TaxPayableRsd` | `decimal` | `OverdueFilingDto.TaxPayableRsd` |
| `TaxPayableDisplay` | `string` | `$"{TaxPayableRsd:N2} RSD"` (InvariantCulture) |
| `Status` | `FilingStatus` | `OverdueFilingDto.Status` |
