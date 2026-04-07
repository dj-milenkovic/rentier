# Interface Contract — IMailboxSyncService

**Layer**: `Rentier.Application/Interfaces`  
**Implementor**: `Rentier.Infrastructure/Sync/ImapMailboxSyncService`  
**Consumer**: `Rentier.Application/Handlers/SyncMailboxCommandHandler`

---

## Contract Definition

```csharp
namespace Rentier.Application.Interfaces;

/// <summary>
/// Connects to a single IMAP mailbox, downloads email attachments matching each
/// importer's filters, and persists them as Report records in Init status.
/// MailKit is an implementation detail of Infrastructure — this interface must
/// remain I/O-agnostic.
/// </summary>
public interface IMailboxSyncService
{
    /// <param name="mailbox">The IMAP mailbox to connect to.</param>
    /// <param name="importers">
    ///     All importers whose MailboxId matches <paramref name="mailbox"/>.Id.
    ///     Must be non-empty.
    /// </param>
    /// <param name="progress">
    ///     Optional progress sink. Receives SyncProgress after each processed message.
    ///     May be null if the caller does not require progress feedback.
    /// </param>
    /// <param name="ct">Cancellation token. OperationCanceledException propagates to caller.</param>
    /// <returns>
    ///     Success: SyncResult(ReportsCreated, Errors) — Errors is empty on full success,
    ///     non-empty on per-importer partial failures.
    ///     Failure: Error.Infrastructure — connection, auth, or unhandled exception.
    ///     Cursor is NOT advanced on Failure.
    /// </returns>
    Task<Result<SyncResult, Error>> SyncAsync(
        Mailbox mailbox,
        IReadOnlyList<Importer> importers,
        IProgress<SyncProgress>? progress,
        CancellationToken ct);
}
```

---

## Behaviour Specification

### Pre-conditions

| # | Condition | Violation behaviour |
|---|---|---|
| 1 | `mailbox` is non-null | `ArgumentNullException` (caller responsibility) |
| 2 | `mailbox.Host`, `Port`, `Username` are valid | Enforced by `Mailbox` constructor; guaranteed if loaded from repo |
| 3 | `importers` is non-null and non-empty | Implementation silently returns `Success(0, [])` on empty list |
| 4 | All `importers[n].MailboxId == mailbox.Id` | Not validated; mismatched importers are silently included in search |
| 5 | Password exists in OS credential store for key `Rentier/Mailbox/{mailbox.Id}/password` | Returns `Result.Failure(Error.Infrastructure("No password..."))` immediately |

### Post-conditions (Success path)

| # | Guarantee |
|---|---|
| 1 | Each IMAP message matching importer filters has its attachments scanned |
| 2 | Attachments matching `importer.AttachmentRegex` are persisted as `Report` records via `IReportRepository.AddAsync` |
| 3 | Reports with duplicate `(ImporterId, ReportName)` are skipped (`ExistsByImporterAndNameAsync` check) |
| 4 | All new reports have `Status = ReportStatus.Init` |
| 5 | `mailbox.Cursor.LastUid` is set to the maximum UID seen across all processed messages |
| 6 | `IMailboxRepository.UpdateAsync(mailbox)` is called exactly once with the updated cursor |
| 7 | `progress` receives a `SyncProgress` update after each message (per-importer, not per-mailbox) |
| 8 | `SyncProgress.IsComplete = true` on the final message of each importer |

### Post-conditions (Failure path)

| # | Guarantee |
|---|---|
| 1 | `IMailboxRepository.UpdateAsync` is NOT called — cursor is not advanced |
| 2 | Connection-level failures (auth, TCP) return `Result.Failure(Error.Infrastructure(...))` |
| 3 | Per-importer exceptions (search/download failures) are caught; added to `SyncResult.Errors` |
| 4 | `OperationCanceledException` propagates uncaught to the caller |

---

## IMAP Search Strategy

```
if (mailbox.Cursor.LastUid == null)
    query = SearchQuery.DeliveredAfter(mailbox.Cursor.LastSyncDate ?? mailbox.InitialSyncDate)
else
    minUid = new UniqueId((uint)(mailbox.Cursor.LastUid.Value + 1))
    query = SearchQuery.Uids(new UniqueIdRange(minUid, UniqueId.MaxValue))

if (importer.FromFilter != "")
    query = SearchQuery.And(query, SearchQuery.FromContains(importer.FromFilter))

if (importer.SubjectFilter != "")
    query = SearchQuery.And(query, SearchQuery.SubjectContains(importer.SubjectFilter))
```

Search folder: **INBOX** only (`client.Inbox`), opened with `FolderAccess.ReadOnly`.

---

## Progress Reporting

```csharp
// Reported after each message is processed for a given importer:
new SyncProgress(
    Total:       uids.Count,           // total UIDs matching this importer
    Processed:   ++processed,          // 1-based index
    CurrentFile: message.Subject,
    IsComplete:  processed == uids.Count
)
```

Progress is reported on the calling thread. Callers must marshal to UI thread if needed.

---

## Credential Key Convention

```
Rentier/Mailbox/{mailboxId}/password
```

Retrieved via `ICredentialStore.GetCredentialAsync(key, ct)`.  
`null` or empty string → immediate `Result.Failure` — no connection attempt is made.

---

## Error Codes Used

| Code | When |
|---|---|
| `INFRASTRUCTURE_ERROR` | Missing credential, IMAP connect/auth failure, unhandled exception |

Partial failures (per-importer exceptions) are surfaced in `SyncResult.Errors` as strings,
not `Error` values, to allow aggregation across multiple importers.

---

## DI Registration

```csharp
// In InfrastructureServiceExtensions.AddInfrastructureServices():
services.AddTransient<IMailboxSyncService, ImapMailboxSyncService>();
services.AddTransient<
    ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>,
    SyncMailboxCommandHandler>();
```

Lifetime: `Transient` — each sync operation gets a fresh `ImapClient` instance.
