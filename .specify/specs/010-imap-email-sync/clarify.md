# Clarify — 010 IMAP Email Sync

## Decisions

| # | Question | Decision |
|---|----------|----------|
| 1 | Where does MailKit live? | Infrastructure only. Application defines `IMailboxSyncService` interface; `ImapMailboxSyncService` implements it. |
| 2 | How does `SyncMailboxCommandHandler` work? | Loads all importers+mailboxes, groups by mailbox, calls `IMailboxSyncService.SyncAsync` per mailbox. |
| 3 | `IProgress<SyncProgress>` in command record? | Yes — `SyncMailboxCommand(IProgress<SyncProgress>? Progress = null)`. Passed through to service. |
| 4 | Cursor transition rule? | No cursor (null UID): search by `InitialSyncDate`. After first successful sync: store max UID seen as `LastUid`. Subsequent: search `uid > LastUid`. Cursor only updated on success. |
| 5 | Attachment content storage? | `byte[]` column in Reports table (`AttachmentContent`). Nullable — populated only when attachment found. |
| 6 | Duplicate report prevention? | `UNIQUE (ImporterId, ReportName)` index. Check via `ExistsByImporterAndNameAsync` before adding. |
| 7 | Error handling scope? | Per-mailbox isolation: error on one mailbox does not stop others. Aggregated error list in result. |
| 8 | ReportName format? | Email subject + `_` + attachment filename. Truncated to 500 chars. |
| 9 | `SyncProgress` fields? | `(int Total, int Processed, string? CurrentFile, bool IsComplete)` |
| 10 | `MailboxMessageId` type? | `long?` — MailKit UID is `UniqueId` (uint); store as long for SQLite INTEGER compatibility. |
| 11 | Report.Status mutable? | Via `SetStatus(ReportStatus)` instance method — not a property setter. |
| 12 | Credential key format? | `Rentier/Mailbox/{mailboxId}/password` — matches existing ICredentialStore key convention. |
| 13 | Search scope? | Always search INBOX only. |
