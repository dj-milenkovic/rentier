# Spec — 010 IMAP Email Sync

## Goal
Synchronise IMAP mailboxes configured in Rentier, downloading email attachments
that match each importer's filters, and persisting them as `Report` records for
downstream pipeline processing.

## Domain Changes

### `ReportStatus` enum (new, Domain/Enums)
```csharp
public enum ReportStatus { Init = 0, Processed = 1, Error = 2 }
```

### `Report` entity enrichment
Add to existing entity:
- `Status` (ReportStatus) — mutable via `SetStatus(ReportStatus)`
- `ReportName` (string, max 500, not-null) — "{subject}_{filename}"
- `AttachmentContent` (byte[]?) — raw bytes of matched attachment
- `MailboxMessageId` (long?) — UID from IMAP server

Factory method: `Report.Create(importerId, reportName, attachmentContent, mailboxMessageId)`

### EF Migration 0007
Creates `Reports` table with UNIQUE index on `(ImporterId, ReportName)`.

## Application Layer

### `SyncProgress` record
```
record SyncProgress(int Total, int Processed, string? CurrentFile, bool IsComplete)
```

### `SyncMailboxCommand`
```csharp
public sealed record SyncMailboxCommand(IProgress<SyncProgress>? Progress = null);
```

### `SyncMailboxCommandHandler`
Implements `ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>`.
1. Load all importers via `IImporterRepository.GetAllAsync()`
2. For each unique mailbox in importers, call `IMailboxSyncService.SyncAsync(mailbox, importers, progress, ct)`
3. Aggregate results

### `IMailboxSyncService` interface (Application/Interfaces)
```csharp
Task<Result<SyncResult, Error>> SyncAsync(
    Mailbox mailbox,
    IReadOnlyList<Importer> importers,
    IProgress<SyncProgress>? progress,
    CancellationToken ct);
```

### `IReportRepository` additions
- `Task<IReadOnlyList<Report>> GetByStatusAsync(ReportStatus status, CT)`
- `Task<bool> ExistsByImporterAndNameAsync(Guid importerId, string reportName, CT)`
- `Task UpdateAsync(Report report, CT)`

## Infrastructure Layer

### `ImapMailboxSyncService`
- Injects: `IReportRepository`, `IMailboxRepository`, `ICredentialStore`
- Connects via MailKit: `ImapClient.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect)`
- Authenticates: `ICredentialStore.GetCredentialAsync($"Rentier/Mailbox/{id}/password")`
- Search:
  - If `cursor.LastUid == null`: `SearchQuery.DeliveredAfter(mailbox.InitialSyncDate.ToDateTime(TimeOnly.MinValue))`
  - Else: `SearchQuery.Uids(new UniqueIdRange(new UniqueId((uint)cursor.LastUid.Value + 1), UniqueId.MaxValue))`
  - AND-combine with FromFilter / SubjectFilter if non-empty
- For each matching message: download attachments matching `Importer.AttachmentRegex`
- For each attachment: check `ExistsByImporterAndNameAsync` → skip if duplicate
- Create `Report.Create(importerId, reportName, content, uid)` → `AddAsync`
- Report progress via `IProgress<SyncProgress>`
- After all messages processed successfully: update cursor to UID-based using max UID seen
- On any exception: log, return Failure (cursor NOT updated)

### `ReportRepository`
Full CRUD + `GetByStatusAsync` + `ExistsByImporterAndNameAsync` + `UpdateAsync`.

### EF Config `ReportConfiguration`
- `HasKey(r => r.Id)`
- `Property(r => r.ReportName).HasMaxLength(500).IsRequired()`
- `Property(r => r.AttachmentContent)` nullable
- `HasIndex(r => new { r.ImporterId, r.ReportName }).IsUnique()`
- `HasOne<Importer>().WithMany().HasForeignKey(r => r.ImporterId).OnDelete(DeleteBehavior.Cascade)`

## Tests
- Unit: `SyncMailboxCommandHandlerTests` — mocked `IMailboxSyncService`, verify grouping
- Unit: `ImapMailboxSyncServiceTests` — mock MailKit + repos, test cursor transitions, dedup, progress
- Integration test structure (no real IMAP): placeholder class with `[Trait("Category","Integration")]`
