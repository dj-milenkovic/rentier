using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Logging;

namespace Rentier.Infrastructure.Sync;

/// <summary>
/// Connects to an IMAP mailbox, finds matching email attachments for each importer,
/// and persists them as Report entities ready for processing.
/// </summary>
public class ImapMailboxSyncService(
    IReportRepository reportRepository,
    IMailboxRepository mailboxRepository,
    ICredentialStore credentialStore,
    TimeProvider timeProvider,
    ILogger<ImapMailboxSyncService> logger)
    : IMailboxSyncService
{
    // Logging rule for this class: ids, counts and UIDs only. Never the host, username, subject,
    // attachment name or an exception message built from them.
    private readonly ILogger<ImapMailboxSyncService> _logger = logger;

    /// <summary>Seam for unit tests — override to return a pre-configured or mocked client.</summary>
    protected virtual ImapClient CreateClient() => new ImapClient();

    public async Task<Result<SyncResult, Error>> SyncAsync(
        Mailbox mailbox,
        IReadOnlyList<Importer> importers,
        SyncParameters parameters,
        IProgress<SyncProgressEntry>? progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mailbox);
        ArgumentNullException.ThrowIfNull(importers);

        _logger.MailboxSyncStarted(mailbox.Id, importers.Count);

        var credResult = await credentialStore.GetCredentialAsync(
            CredentialKeys.MailboxPassword(mailbox.Id), ct);

        if (!credResult.IsSuccess)
        {
            _logger.MailboxPasswordUnavailable(mailbox.Id, credResult.Error.Code);
            return Result<SyncResult, Error>.Failure(credResult.Error);
        }

        var password = credResult.Value;

        var errors = new List<string>();
        var reportsCreated = 0;

        try
        {
            using var client = CreateClient();
            await client.ConnectAsync(mailbox.Host, mailbox.Port, SecureSocketOptions.SslOnConnect, ct);
            await client.AuthenticateAsync(mailbox.Username, password, ct);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

            var cursor = mailbox.Cursor;
            long? maxUid = null;
            long? lowestFailedUid = null;
            var anyImporterIncomplete = false;

            // Build base time/uid search query using SyncParameters
            var utcNow = timeProvider.GetUtcNow();
            var baseQuery = BuildBaseQuery(cursor, parameters, utcNow);

            // Process each importer within this mailbox. A single importer's failure is
            // recorded in `errors` and does not abort the remaining importers.
            foreach (var importer in importers)
            {
                ct.ThrowIfCancellationRequested();

                var importerResult = await ProcessImporterAsync(inbox, importer, baseQuery, progress, ct);

                reportsCreated += importerResult.ReportsCreated;
                errors.AddRange(importerResult.Errors);

                // Track the highest UID seen across all importers
                if (importerResult.MaxUid is { } importerMaxUid && (maxUid == null || importerMaxUid > maxUid.Value))
                    maxUid = importerMaxUid;

                // Track the lowest UID that failed in any importer (UIDs are per folder, shared)
                if (importerResult.LowestFailedUid is { } failedUid && (lowestFailedUid == null || failedUid < lowestFailedUid.Value))
                    lowestFailedUid = failedUid;

                anyImporterIncomplete |= importerResult.Incomplete;
            }

            await inbox.CloseAsync(expunge: false, ct);
            await client.DisconnectAsync(quit: true, ct);

            // Cursor dates are UTC calendar dates, consistent with email dates (normalised to UTC below).
            // The cursor must never move past a message that failed: an incremental sync only
            // searches UIDs above it, so a skipped message (and its income) would be lost for good.
            var newCursor = NextCursor(
                cursor, DateOnly.FromDateTime(utcNow.UtcDateTime), maxUid, lowestFailedUid, anyImporterIncomplete);
            if (newCursor is not null)
            {
                mailbox.UpdateCursor(newCursor);
                await mailboxRepository.UpdateAsync(mailbox, ct);
            }

            _logger.MailboxSyncFinished(mailbox.Id, reportsCreated, errors.Count);
            return Result<SyncResult, Error>.Success(new SyncResult(reportsCreated, errors));
        }
        // Boundary translation: MailKit surfaces connection, protocol, authentication and
        // socket failures through unrelated exception hierarchies, so everything except caller
        // cancellation (an internal timeout is not one) becomes an infrastructure Result failure.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.MailboxSyncFailed(ex, mailbox.Id);
            return Result<SyncResult, Error>.Failure(
                Error.Infrastructure($"IMAP sync failed for mailbox {mailbox.Id}: {ex.Message}"));
        }
    }

    /// <summary>
    /// The cursor to save after a sync, or null to keep the current one.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>An importer that failed as a whole (invalid regex, failed search) has an unknown set
    /// of unprocessed messages, so the cursor is left unchanged and the next run re-scans them.</item>
    /// <item>A failed message caps the UID cursor just below it, so the next incremental run
    /// retries it. Messages above it that did succeed are re-scanned but skipped as already
    /// imported (reports are de-duplicated by importer and name).</item>
    /// </list>
    /// A message that fails permanently therefore holds the cursor back (and is reported as an
    /// error on every run) rather than being skipped silently.
    /// </remarks>
    internal static MailboxCursor? NextCursor(
        MailboxCursor current, DateOnly today, long? maxUid, long? lowestFailedUid, bool anyImporterIncomplete)
    {
        if (anyImporterIncomplete)
            return null;

        var previousUid = current is MailboxCursor.SyncedTo s ? s.Uid : null;

        if (lowestFailedUid is { } failed)
        {
            var cappedUid = failed - 1;
            var uid = maxUid is { } max && max < cappedUid ? max : cappedUid;
            if (previousUid is { } prev && uid < prev)
                uid = prev;
            return new MailboxCursor.SyncedTo(today, uid);
        }

        return new MailboxCursor.SyncedTo(today, maxUid ?? previousUid);
    }

    /// <summary>
    /// Searches the inbox for messages matching a single importer's filters and processes
    /// their attachments. Any failure (regex compile, search, or a specific message) is
    /// captured in <see cref="ImporterSyncResult.Errors"/> rather than thrown, so one bad
    /// importer or message never aborts the rest of the sync.
    /// </summary>
    private async Task<ImporterSyncResult> ProcessImporterAsync(
        IMailFolder inbox,
        Importer importer,
        SearchQuery baseQuery,
        IProgress<SyncProgressEntry>? progress,
        CancellationToken ct)
    {
        var errors = new List<string>();
        var reportsCreated = 0;
        long? maxUid = null;
        long? lowestFailedUid = null;
        var incomplete = false;

        try
        {
            // Compile the attachment regex once per importer rather than on every filename check
            Regex? attachmentRegex = string.IsNullOrEmpty(importer.AttachmentRegex)
                ? null
                : new Regex(importer.AttachmentRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var importerQuery = ComposeImporterQuery(baseQuery, importer);
            var uids = await inbox.SearchAsync(importerQuery, ct);
            var total = uids.Count;
            var processed = 0;
            _logger.ImporterMessagesMatched(importer.Id, total);

            foreach (var uid in uids)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    _logger.ImporterProcessingMessage(importer.Id, uid.Id);
                    var message = await inbox.GetMessageAsync(uid, ct);
                    var subject = message.Subject ?? string.Empty;
                    processed++;
                    progress?.Report(new SyncProgressEntry(
                        timeProvider.GetLocalNow(),
                        $"Downloading email {processed}/{total}: {subject}",
                        SyncProgressSeverity.Info));

                    await ProcessAttachmentsAsync(
                        message, importer, uid, subject, attachmentRegex, () => reportsCreated++, ct);

                    // Track the highest UID seen for this importer
                    if (maxUid == null || uid.Id > maxUid.Value)
                        maxUid = uid.Id;
                }
                // Per-message isolation: one bad message is recorded and the rest continue.
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    _logger.ImporterMessageFailed(ex, importer.Id, uid.Id);
                    errors.Add($"Importer {importer.Id} UID {uid.Id}: {ex.Message}");
                    if (lowestFailedUid == null || uid.Id < lowestFailedUid.Value)
                        lowestFailedUid = uid.Id;
                }
            }
        }
        // Per-importer isolation (invalid regex, failed search): recorded, other importers continue.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.ImporterFailed(ex, importer.Id);
            errors.Add($"Importer {importer.Id}: {ex.Message}");
            incomplete = true;
        }

        return new ImporterSyncResult(reportsCreated, maxUid, errors, lowestFailedUid, incomplete);
    }

    /// <summary>
    /// Finds attachments on a single message that match the importer's attachment regex
    /// and persists any not already recorded for this importer. Invokes
    /// <paramref name="onReportCreated"/> immediately after each report is persisted so
    /// the caller's running count reflects reports already saved even if a later
    /// attachment on the same message throws.
    /// </summary>
    private async Task ProcessAttachmentsAsync(
        MimeMessage message,
        Importer importer,
        UniqueId uid,
        string subject,
        Regex? attachmentRegex,
        Action onReportCreated,
        CancellationToken ct)
    {
        foreach (var attachment in message.Attachments.OfType<MimePart>())
        {
            var filename = attachment.FileName
                ?? attachment.ContentType.Name
                ?? string.Empty;

            if (string.IsNullOrEmpty(filename))
                continue;

            // attachmentRegex is null when AttachmentRegex is empty → skip all attachments
            if (attachmentRegex is null || !attachmentRegex.IsMatch(filename))
                continue;

            var rawDate = message.Date;
            // Guard against missing/default Date header (MimeKit returns DateTimeOffset.MinValue).
            // Use Unix epoch as a stable sentinel so the report name is deterministic
            // across replays — the current time would produce a different name each run.
            var emailDate = rawDate == default
                ? DateOnly.FromDateTime(DateTime.UnixEpoch)
                : DateOnly.FromDateTime(rawDate.UtcDateTime);
            var reportName = BuildReportName(emailDate, subject, filename);

            var exists = await reportRepository.ExistsByImporterAndNameAsync(
                importer.Id, reportName, ct);
            if (exists)
                continue;

            using var ms = new MemoryStream();
            if (attachment.Content != null)
                await attachment.Content.DecodeToAsync(ms, ct);
            var content = ms.ToArray();

            var report = Report.Create(
                importer.Id, reportName, content, uid.Id,
                importDate: timeProvider.GetLocalToday(), emailDate: emailDate);
            await reportRepository.AddAsync(report, ct);
            onReportCreated();
        }
    }

    /// <summary>
    /// Per-importer outcome of <see cref="ProcessImporterAsync"/>: reports created, the highest UID
    /// processed, any errors, the lowest UID that failed, and whether the importer failed as a
    /// whole (<see cref="Incomplete"/>: its unprocessed messages are unknown).
    /// </summary>
    private readonly record struct ImporterSyncResult(
        int ReportsCreated, long? MaxUid, IReadOnlyList<string> Errors, long? LowestFailedUid, bool Incomplete);

    private static SearchQuery BuildBaseQuery(MailboxCursor cursor, SyncParameters parameters, DateTimeOffset utcNow)
    {
        var effectiveDate = parameters.GetEffectiveStartDate(cursor);

        // For FullReplay mode with no date, fetch all
        if (effectiveDate == null && parameters.Mode == Domain.Enums.SyncMode.FullReplay)
            return SearchQuery.All;

        // For Incremental mode: prefer UID filter if available
        if (parameters.Mode == Domain.Enums.SyncMode.Incremental && cursor is MailboxCursor.SyncedTo { Uid: not null } synced)
        {
            return SearchQuery.Uids(
                new UniqueIdRange(
                    new UniqueId((uint)(synced.Uid.Value + 1)),
                    UniqueId.MaxValue));
        }

        // Otherwise: use date filter
        var sinceDate = effectiveDate?.ToDateTime(TimeOnly.MinValue)
            ?? utcNow.UtcDateTime.AddDays(-Mailbox.DEFAULT_LOOKBACK_DAYS);
        return SearchQuery.DeliveredAfter(sinceDate);
    }

    private static SearchQuery ComposeImporterQuery(SearchQuery baseQuery, Importer importer)
    {
        var query = baseQuery;

        if (!string.IsNullOrEmpty(importer.FromFilter))
            query = SearchQuery.And(query, SearchQuery.FromContains(importer.FromFilter));

        if (!string.IsNullOrEmpty(importer.SubjectFilter))
            query = SearchQuery.And(query, SearchQuery.SubjectContains(importer.SubjectFilter));

        return query;
    }

    /// <summary>
    /// Builds a report name from the email date, subject and attachment filename,
    /// truncated to 500 characters for the database constraint.
    /// </summary>
    internal static string BuildReportName(DateOnly emailDate, string subject, string filename)
    {
        var raw = $"{emailDate:yyyy-MM-dd}_{subject}_{filename}";
        return raw.Length > 500 ? raw[..500] : raw;
    }
}
