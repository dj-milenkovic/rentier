# Data Model: Reports List & Manual Import

**Feature**: 014-reports-list-manual-import  
**Branch**: `feature/003-reports-manual-import`  
**Date**: 2026-04-07

---

## Domain Entities (Existing — Used As-Is)

### `Report`

```
Rentier.Domain.Entities.Report
```

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `Guid` | PK, non-null | Set by `Report.Create(...)` factory |
| `ImportDate` | `DateOnly` | non-null | `DateOnly.FromDateTime(DateTime.UtcNow)` in factory |
| `ImporterId` | `Guid` | FK → Importer, non-null | Provided at creation |
| `Status` | `ReportStatus` | non-null, default Init | Mutated via `SetStatus(...)` |
| `ReportName` | `string` | non-null, max 500 chars | Filename-derived; trimmed |
| `AttachmentContent` | `byte[]?` | nullable | Raw CSV bytes; null for email-only records |
| `MailboxMessageId` | `long?` | nullable | Null for manually imported reports |

**Factory method**: `Report.Create(Guid importerId, string reportName, byte[]? attachmentContent, long? mailboxMessageId)`  
**Mutation**: `Report.SetStatus(ReportStatus status)` — called by `ProcessReportsCommandHandler`

**No changes to this entity in this feature.**

---

### `Filing`

```
Rentier.Domain.Entities.Filing
```

Key field relevant to this feature:

| Field | Type | Notes |
|---|---|---|
| `ReportId` | `Guid?` | FK back to `Report.Id`; used for count queries and cascade delete |

**No changes to this entity in this feature.**

---

### `Importer`

```
Rentier.Domain.Entities.Importer
```

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `DisplayName` | `string` | Displayed in import dialog dropdown and `ImporterName` column |

**No changes to this entity in this feature.**

---

## Application Read-Model DTOs

### `ReportRowDto` *(NEW)*

```csharp
// Rentier.Application/DTOs/ReportRowDto.cs
namespace Rentier.Application.DTOs;

public sealed record ReportRowDto(
    Guid         Id,
    string       ReportName,
    DateOnly     ImportDate,    // DateOnly — no time component
    string       ImporterName, // resolved from Importer.DisplayName
    ReportStatus Status,
    int          FilingCount);
```

**Projection source**: `Report` entity + importer dictionary lookup + `FilingRepository.GetFilingCountByReportIdAsync`

---

## Repository Interface Extensions

### `IFilingRepository` — two new methods

```csharp
// Rentier.Application/Repositories/IFilingRepository.cs

/// <summary>
/// Returns the count of Filing records linked to the given Report.
/// Used by GetReportsQueryHandler to populate ReportRowDto.FilingCount.
/// </summary>
Task<int> GetFilingCountByReportIdAsync(Guid reportId, CancellationToken ct = default);

/// <summary>
/// Deletes all Filing records whose ReportId matches reportId.
/// Called by DeleteReportCommandHandler BEFORE deleting the parent Report.
/// Implementation: load list → RemoveRange → SaveChangesAsync (NOT ExecuteDeleteAsync).
/// </summary>
Task DeleteByReportIdAsync(Guid reportId, CancellationToken ct = default);
```

**`IReportRepository`** — no new methods. `GetAllAsync` (already present) is used by `GetReportsQueryHandler`.

---

## Query Contracts

### `GetFilingsQuery` — extended

```csharp
// Rentier.Application/Queries/GetFilingsQuery.cs  — EXTEND
public sealed record GetFilingsQuery(
    FilingFilterMode Filter,
    int              Page,
    int              PageSize       = 20,
    Guid?            ReportIdFilter = null);   // NEW — when set, filters to one report
```

---

## State Transitions

### Report Status Machine

```
         Report.Create(...)
               │
               ▼
           [ Init ]
               │
    ProcessReportsCommand runs
               │
       ┌───────┴────────┐
       │                │
       ▼                ▼
  [ Processed ]      [ Error ]
```

- `Init` → `Processed`: successful parse + all filings created
- `Init` → `Error`: exception during `ProcessReportAsync`
- Manually imported reports start at `Init` (same as IMAP-synced reports)
- `MailboxMessageId` is `null` for manually imported reports

---

## Infrastructure Implementations

### `FilingRepository.GetFilingCountByReportIdAsync`

```csharp
public async Task<int> GetFilingCountByReportIdAsync(
    Guid reportId, CancellationToken ct = default)
    => await _db.Filings
           .AsNoTracking()
           .CountAsync(f => f.ReportId == reportId, ct);
```

### `FilingRepository.DeleteByReportIdAsync`

```csharp
// CRITICAL: Do NOT use ExecuteDeleteAsync — breaks SQLite in-memory tests
public async Task DeleteByReportIdAsync(
    Guid reportId, CancellationToken ct = default)
{
    var filings = await _db.Filings
        .Where(f => f.ReportId == reportId)
        .ToListAsync(ct);

    if (filings.Count == 0) return;   // idempotent

    _db.Filings.RemoveRange(filings);
    await _db.SaveChangesAsync(ct);
}
```

---

## No Schema Changes

This feature introduces **no new database columns or tables** and requires **no EF Core migration**.

- `Report` entity: unchanged
- `Filing` entity: unchanged — `ReportId` column already exists
- `Importer` entity: unchanged

---

## ViewModel Display Model

### `ReportRowViewModel` *(NEW)*

```csharp
// Rentier.Desktop/ViewModels/ReportRowViewModel.cs
// Immutable display model — no ReactiveObject needed
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
