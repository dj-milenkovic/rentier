# Data Model: 018 Sync Replay Controls

**Feature Branch**: `feature/018-sync-replay-controls`
**Date**: 2025-07-15

## New Domain Types

### SyncMode (Enum — `Rentier.Domain/Enums/SyncMode.cs`)

```csharp
public enum SyncMode
{
    Incremental = 0,     // Default: sync from cursor position
    ReplayFromDate = 1,  // Replay from user-specified DateOnly
    FullReplay = 2       // Replay from beginning of time
}
```

| Value | IMAP Query Behavior | Duplicate Strategy Required |
|-------|--------------------|-----------------------------|
| Incremental | From cursor (LastUid > X or DeliveredAfter(LastSyncDate)) | No (cursor prevents duplicates) |
| ReplayFromDate | DeliveredAfter(replayDate) | Yes |
| FullReplay | No date filter (all messages) | Yes |

---

### DuplicateStrategy (Enum — `Rentier.Domain/Enums/DuplicateStrategy.cs`)

```csharp
public enum DuplicateStrategy
{
    SkipExisting = 0,       // Default: skip already-imported reports (idempotent)
    CreateNewRevision = 1,  // Create a new report linked to original
    ReprocessInPlace = 2    // Update existing report's filings (with safety check)
}
```

| Value | Behavior on Duplicate | Safety Constraint |
|-------|----------------------|-------------------|
| SkipExisting | Log "skipped — already exists", move to next | None |
| CreateNewRevision | Create new Report with `OriginalReportId` pointing to existing | None |
| ReprocessInPlace | Delete filings for existing report, re-parse | BLOCKED if any filing has Status ≠ Init (fall back to CreateNewRevision) |

---

### SyncParameters (Value Object — `Rentier.Domain/ValueObjects/SyncParameters.cs`)

```csharp
public sealed record SyncParameters
{
    public SyncMode Mode { get; init; } = SyncMode.Incremental;
    public DuplicateStrategy Strategy { get; init; } = DuplicateStrategy.SkipExisting;
    public DateOnly? ReplayFromDate { get; init; }
    public Guid? ScopeImporterId { get; init; }

    /// <summary>
    /// Returns the effective start date for IMAP query construction.
    /// For Incremental: uses cursor's LastSyncDate.
    /// For ReplayFromDate: uses the user-specified date.
    /// For FullReplay: returns null (no date filter).
    /// </summary>
    public DateOnly? GetEffectiveStartDate(MailboxCursor cursor) => Mode switch
    {
        SyncMode.Incremental => cursor.LastSyncDate,
        SyncMode.ReplayFromDate => ReplayFromDate,
        SyncMode.FullReplay => null,
        _ => cursor.LastSyncDate
    };
}
```

**Validation Rules**:
- `ReplayFromDate` MUST be non-null when `Mode == ReplayFromDate`
- `ReplayFromDate` MUST be ≤ today (no future dates)
- `ReplayFromDate` MUST be null when `Mode != ReplayFromDate`
- `ScopeImporterId` is optional; when set with FullReplay, limits replay to one importer
- `Strategy` is ignored when `Mode == Incremental` (cursor prevents duplicates)

---

## Modified Entities

### Mailbox Entity — Remove InitialSyncDate

**File**: `src/Rentier.Domain/Entities/Mailbox.cs`

#### Current Schema

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | Guid | No | PK |
| Host | string(253) | No | IMAP server hostname |
| Port | int | No | IMAP port (1-65535) |
| Username | string(320) | No | Email username |
| **InitialSyncDate** | **DateOnly** | **No** | **Starting date for first sync — TO BE REMOVED** |
| Cursor_LastSyncDate | DateOnly | Yes | Last sync date (owned VO) |
| Cursor_LastUid | long | Yes | Last processed IMAP UID (owned VO) |

#### New Schema (after migration)

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | Guid | No | PK |
| Host | string(253) | No | IMAP server hostname |
| Port | int | No | IMAP port (1-65535) |
| Username | string(320) | No | Email username |
| Cursor_LastSyncDate | DateOnly | Yes | Last sync date (owned VO) |
| Cursor_LastUid | long | Yes | Last processed IMAP UID (owned VO) |

#### Entity Code Changes

```csharp
// BEFORE:
public static Mailbox Create(string host, int port, string username, DateOnly initialSyncDate)
{
    // ... validation ...
    return new Mailbox
    {
        Id = Guid.NewGuid(),
        Host = host, Port = port, Username = username,
        InitialSyncDate = initialSyncDate,
        Cursor = new MailboxCursor(initialSyncDate, null)
    };
}

// AFTER:
public static Mailbox Create(string host, int port, string username)
{
    // ... validation ...
    var defaultStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-90);
    return new Mailbox
    {
        Id = Guid.NewGuid(),
        Host = host, Port = port, Username = username,
        Cursor = new MailboxCursor(defaultStart, null)
    };
}

// UpdateDetails also changes:
// BEFORE: public void UpdateDetails(string host, int port, string username, DateOnly initialSyncDate)
// AFTER:  public void UpdateDetails(string host, int port, string username)
```

---

### Report Entity — Add Revision Tracking

**File**: `src/Rentier.Domain/Entities/Report.cs`

#### Current Schema

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | Guid | No | PK |
| ImportDate | DateOnly | No | Date imported |
| ImporterId | Guid | No | FK to Importer |
| Status | int (ReportStatus) | No | Init/Processed/Error |
| ReportName | string(500) | No | Subject + attachment name |
| AttachmentContent | byte[] | Yes | Raw file content |
| MailboxMessageId | long | Yes | IMAP UID |

#### New Schema (after migration)

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | Guid | No | PK |
| ImportDate | DateOnly | No | Date imported |
| ImporterId | Guid | No | FK to Importer |
| Status | int (ReportStatus) | No | Init/Processed/Error |
| ReportName | string(500) | No | Subject + attachment name |
| AttachmentContent | byte[] | Yes | Raw file content |
| MailboxMessageId | long | Yes | IMAP UID |
| **OriginalReportId** | **Guid** | **Yes** | **FK to Report (self-referencing) — null for originals** |

#### Entity Code Changes

```csharp
// Add to Report entity:
public Guid? OriginalReportId { get; private set; }

// Factory for revision:
public static Report CreateRevision(Report original, byte[]? newContent)
{
    return new Report
    {
        Id = Guid.NewGuid(),
        ImportDate = DateOnly.FromDateTime(DateTime.UtcNow),
        ImporterId = original.ImporterId,
        Status = ReportStatus.Init,
        ReportName = $"{original.ReportName}_rev{DateTime.UtcNow:yyyyMMddHHmmss}",
        AttachmentContent = newContent,
        MailboxMessageId = original.MailboxMessageId,
        OriginalReportId = original.Id
    };
}
```

---

## EF Core Migration: 0010_SyncReplayControls

**File**: `src/Rentier.Infrastructure/Persistence/Migrations/0010_SyncReplayControls.cs`

### Migration Steps

```sql
-- Step 1: Preserve InitialSyncDate for un-synced mailboxes
-- (Copy to cursor so first incremental sync starts from correct date)
UPDATE Mailboxes
SET Cursor_LastSyncDate = InitialSyncDate
WHERE Cursor_LastSyncDate IS NULL;

-- Step 2: Remove InitialSyncDate column
-- (EF Core handles SQLite table rebuild for column removal)
ALTER TABLE Mailboxes DROP COLUMN InitialSyncDate;

-- Step 3: Add OriginalReportId to Reports
ALTER TABLE Reports ADD COLUMN OriginalReportId TEXT NULL;

-- Step 4: Create index for revision lookups
CREATE INDEX IX_Reports_OriginalReportId ON Reports (OriginalReportId);

-- Step 5: Add FK constraint (via table rebuild in SQLite)
-- FK: Reports.OriginalReportId → Reports.Id (ON DELETE SET NULL)
```

### EF Core Configuration Changes

#### MailboxConfiguration — Remove InitialSyncDate

```csharp
// REMOVE:
// builder.Property(m => m.InitialSyncDate).IsRequired();
```

#### ReportConfiguration — Add OriginalReportId

```csharp
// ADD:
builder.Property(r => r.OriginalReportId);
builder.HasIndex(r => r.OriginalReportId);
builder.HasOne<Report>()
    .WithMany()
    .HasForeignKey(r => r.OriginalReportId)
    .OnDelete(DeleteBehavior.SetNull);
```

---

## MailboxCursor — No Schema Change

The `MailboxCursor` value object is **not modified**. It remains:

```csharp
public record MailboxCursor(DateOnly? LastSyncDate, long? LastUid);
```

Override for replay is handled externally via `SyncParameters.GetEffectiveStartDate()`.

---

## Relationships Diagram

```text
┌──────────────┐       ┌──────────────┐
│   Mailbox    │ 1───* │   Importer   │
│──────────────│       │──────────────│
│ Id           │       │ Id           │
│ Host         │       │ DisplayName  │
│ Port         │       │ ReportType   │
│ Username     │       │ MailboxId FK │
│ Cursor (VO)  │       │ FromFilter   │
│              │       │ SubjectFilter│
└──────────────┘       │ AttachRegex  │
                       └──────┬───────┘
                              │ 1
                              │
                              │ *
                       ┌──────┴───────┐
                       │    Report    │
                       │──────────────│
                       │ Id           │
                       │ ImportDate   │
                       │ ImporterId FK│
                       │ Status       │
                       │ ReportName   │
                       │ Content      │
                       │ MsgId        │
                       │ OrigReportId │◄── self-ref (nullable)
                       └──────┬───────┘
                              │ 1
                              │ *
                       ┌──────┴───────┐
                       │    Filing    │
                       │──────────────│
                       │ Id           │
                       │ ReportId FK  │
                       │ Status       │
                       │ ...amounts   │
                       └──────────────┘

New domain types (not persisted as entities):
┌─────────────────────┐
│   SyncParameters    │ (Value Object — transient, not stored)
│─────────────────────│
│ Mode: SyncMode      │
│ Strategy: DupStrat  │
│ ReplayFromDate?     │
│ ScopeImporterId?    │
└─────────────────────┘
```

---

## Validation Rules Summary

| Entity/Type | Rule | Enforcement |
|-------------|------|-------------|
| SyncParameters | ReplayFromDate required when Mode = ReplayFromDate | Domain validation in constructor/factory |
| SyncParameters | ReplayFromDate ≤ today | Domain validation |
| SyncParameters | ReplayFromDate null when Mode ≠ ReplayFromDate | Domain validation |
| Mailbox.Create | Default cursor = 90 days ago | Factory method |
| Report.CreateRevision | OriginalReportId must reference existing report | FK constraint |
| Report.CreateRevision | ReportName must be unique per importer | Unique index (existing) |
| Cursor update | new.LastSyncDate ≥ old.LastSyncDate | Application logic in sync service |
| Cursor update | new.LastUid ≥ old.LastUid | Application logic in sync service |
| ReprocessInPlace | Blocked if filing.Status ≠ Init | Application logic in sync handler |
