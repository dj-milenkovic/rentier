# Data Model — 010 IMAP Email Sync

## Entities

### `Report` (Domain/Entities/Report.cs)

| Field | Type | Nullable | Constraint | Notes |
|---|---|---|---|---|
| `Id` | `Guid` | No | PK | `Guid.NewGuid()` on create |
| `ImportDate` | `DateOnly` | No | — | Set to `DateOnly.FromDateTime(DateTime.UtcNow)` at creation |
| `ImporterId` | `Guid` | No | FK → Importers(Id), CASCADE DELETE | Links to originating importer |
| `Status` | `ReportStatus` | No | DEFAULT 0 | Mutable via `SetStatus()` only |
| `ReportName` | `string` | No | max 500, UNIQUE with ImporterId | Format: `"{subject}_{filename}"` |
| `AttachmentContent` | `byte[]?` | Yes | BLOB | Raw bytes of matched attachment |
| `MailboxMessageId` | `long?` | Yes | — | IMAP UID cast from `uint` to `long` |

**Unique index**: `IX_Reports_ImporterId_ReportName` on `(ImporterId, ReportName)`  
**Migration**: `0007_ReportEnrichment` ✅ applied

**Factory method**:
```csharp
public static Report Create(
    Guid importerId,
    string reportName,       // validated: not empty, max 500 chars
    byte[]? attachmentContent,
    long? mailboxMessageId)
```

**State transitions**:
```text
Status.Init → Status.Processed   (ProcessReportsCommand — feature 011)
Status.Init → Status.Error        (ProcessReportsCommand — feature 011)
```
Transitions performed via `report.SetStatus(ReportStatus status)`.

---

### `ReportStatus` (Domain/Enums/ReportStatus.cs)

```csharp
public enum ReportStatus { Init = 0, Processed = 1, Error = 2 }
```

---

### `Mailbox` (Domain/Entities/Mailbox.cs) — existing, no schema change

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `Host` | `string` | IMAP server hostname |
| `Port` | `int` | Typically 993 |
| `Username` | `string` | IMAP login username |
| `InitialSyncDate` | `DateOnly` | Seed date for first sync query |
| `Cursor` | `MailboxCursor` | Owned VO — see below |

Password is **NOT** stored here. Retrieved exclusively from `ICredentialStore` using
key `Rentier/Mailbox/{Id}/password`.

---

### `MailboxCursor` (Domain/ValueObjects/MailboxCursor.cs) — existing VO

```csharp
public record MailboxCursor(DateOnly? LastSyncDate, long? LastUid);
```

| State | `LastSyncDate` | `LastUid` | Search strategy |
|---|---|---|---|
| Never synced | `= InitialSyncDate` | `null` | `DeliveredAfter(LastSyncDate)` |
| After first sync | unchanged | `= maxUid` | `Uids(minUid, MaxValue)` where `minUid = LastUid + 1` |
| After subsequent | unchanged | `= new maxUid` | same UID range strategy |

Cursor is immutable (record). Updated only via `Mailbox.UpdateCursor(MailboxCursor)`.
Cursor is only advanced on **successful** sync completion.

EF mapping (owned type in `MailboxConfiguration`):
```csharp
builder.OwnsOne(m => m.Cursor, c =>
{
    c.Property(x => x.LastSyncDate).HasColumnName("CursorLastSyncDate").HasColumnType("TEXT");
    c.Property(x => x.LastUid).HasColumnName("CursorLastUid").HasColumnType("INTEGER");
});
```

---

### `Importer` (Domain/Entities/Importer.cs) — existing, no schema change

Relevant fields for email sync:

| Field | Type | Notes |
|---|---|---|
| `MailboxId` | `Guid?` | FK to Mailboxes; `null` means file-import-only importer |
| `FromFilter` | `string` | Substring match on `From` header. Empty = no filter. |
| `SubjectFilter` | `string` | Substring match on `Subject` header. Empty = no filter. |
| `AttachmentRegex` | `string` | Regex applied to attachment filename. Empty = skip all attachments. |

---

## Repository Contracts

### `IReportRepository` (Application/Repositories)

```csharp
public interface IReportRepository
{
    Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetByImporterAsync(Guid importerId, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetByStatusAsync(ReportStatus status, CancellationToken ct = default);
    Task<bool> ExistsByImporterAndNameAsync(Guid importerId, string reportName, CancellationToken ct = default);
    Task AddAsync(Report report, CancellationToken ct = default);
    Task UpdateAsync(Report report, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

Methods added for this feature vs. original `IReportRepository`:
- `GetByStatusAsync` — used by downstream ProcessReports pipeline
- `ExistsByImporterAndNameAsync` — deduplication guard before `Report.Create`
- `UpdateAsync` — used when downstream processing updates `Status`

---

## EF Core Schema Snapshot

```sql
CREATE TABLE "Reports" (
    "Id"                TEXT NOT NULL CONSTRAINT "PK_Reports" PRIMARY KEY,
    "ImportDate"        TEXT NOT NULL,
    "ImporterId"        TEXT NOT NULL,
    "Status"            INTEGER NOT NULL DEFAULT 0,
    "ReportName"        TEXT NOT NULL,
    "AttachmentContent" BLOB,
    "MailboxMessageId"  INTEGER,
    CONSTRAINT "FK_Reports_Importers_ImporterId"
        FOREIGN KEY ("ImporterId") REFERENCES "Importers" ("Id")
        ON DELETE CASCADE
);
CREATE UNIQUE INDEX "IX_Reports_ImporterId_ReportName"
    ON "Reports" ("ImporterId", "ReportName");
```
