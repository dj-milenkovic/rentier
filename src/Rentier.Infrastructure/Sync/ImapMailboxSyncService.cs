using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.ValueObjects;

namespace Rentier.Infrastructure.Sync;

/// <summary>
/// Connects to an IMAP mailbox, finds matching email attachments for each importer,
/// and persists them as Report entities ready for processing.
/// </summary>
public class ImapMailboxSyncService : IMailboxSyncService
{
    private readonly IReportRepository _reportRepository;
    private readonly IMailboxRepository _mailboxRepository;
    private readonly ICredentialStore _credentialStore;

    public ImapMailboxSyncService(
        IReportRepository reportRepository,
        IMailboxRepository mailboxRepository,
        ICredentialStore credentialStore)
    {
        _reportRepository = reportRepository;
        _mailboxRepository = mailboxRepository;
        _credentialStore = credentialStore;
    }

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

        var credResult = await _credentialStore.GetCredentialAsync(
            CredentialKeys.MailboxPassword(mailbox.Id), ct);

        if (!credResult.IsSuccess)
            return Result<SyncResult, Error>.Failure(credResult.Error);

        var password = credResult.Value;

        var errors = new List<string>();
        var reportsCreated = 0;

        try
        {
            using var client = CreateClient();
            await client.ConnectAsync(mailbox.Host, mailbox.Port, SecureSocketOptions.SslOnConnect, ct);
            await client.AuthenticateAsync(mailbox.Username, password, ct);

            var inbox = client.Inbox;
            await inbox!.OpenAsync(FolderAccess.ReadOnly, ct);

            var cursor = mailbox.Cursor;
            long? maxUid = null;

            // Build base time/uid search query using SyncParameters
            var baseQuery = BuildBaseQuery(cursor, parameters);

            // Process each importer within this mailbox
            foreach (var importer in importers)
            {
                ct.ThrowIfCancellationRequested();
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

                    foreach (var uid in uids)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var message = await inbox.GetMessageAsync(uid, ct);
                            var subject = message.Subject ?? string.Empty;
                            processed++;
                            progress?.Report(new SyncProgressEntry(
                                DateTimeOffset.Now,
                                $"Downloading email {processed}/{total}: {subject}",
                                SyncProgressSeverity.Info));

                            foreach (var attachment in message.Attachments.OfType<MimePart>())
                            {
                                var filename = attachment.FileName
                                    ?? attachment.ContentType?.Name
                                    ?? string.Empty;

                                if (string.IsNullOrEmpty(filename))
                                    continue;

                                // attachmentRegex is null when AttachmentRegex is empty → skip all attachments
                                if (attachmentRegex is null || !attachmentRegex.IsMatch(filename))
                                    continue;

                                var rawDate = message.Date;
                                // Guard against missing/default Date header (MimeKit returns DateTimeOffset.MinValue).
                                // Use Unix epoch as a stable sentinel so the report name is deterministic
                                // across replays — DateTime.UtcNow would produce a different name each run.
                                var emailDate = rawDate == default // NOSONAR — requires live IMAP message with no Date header
                                    ? DateOnly.FromDateTime(DateTime.UnixEpoch) // NOSONAR
                                    : DateOnly.FromDateTime(rawDate.UtcDateTime);
                                var reportName = BuildReportName(emailDate, subject, filename);

                                var exists = await _reportRepository.ExistsByImporterAndNameAsync(
                                    importer.Id, reportName, ct);
                                if (exists)
                                    continue;

                                using var ms = new MemoryStream();
                                if (attachment.Content != null)
                                    attachment.Content.DecodeTo(ms);
                                var content = ms.ToArray();

                                var report = Report.Create(importer.Id, reportName, content, (long)uid.Id, emailDate);
                                await _reportRepository.AddAsync(report, ct);
                                reportsCreated++;
                            }

                            // Track the highest UID seen across all importers
                            if (maxUid == null || (long)uid.Id > maxUid.Value)
                                maxUid = (long)uid.Id;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Importer {importer.Id} UID {uid.Id}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Importer {importer.Id}: {ex.Message}");
                }
            }

            await inbox.CloseAsync(expunge: false, ct);
            await client.DisconnectAsync(quit: true, ct);

            // Update cursor after all importers processed successfully
            var newCursor = new MailboxCursor.SyncedTo(
                DateOnly.FromDateTime(DateTime.UtcNow),
                maxUid ?? (cursor is MailboxCursor.SyncedTo s ? s.Uid : null));
            mailbox.UpdateCursor(newCursor);
            await _mailboxRepository.UpdateAsync(mailbox, ct);

            return Result<SyncResult, Error>.Success(new SyncResult(reportsCreated, errors));
        }
        catch (Exception ex)
        {
            return Result<SyncResult, Error>.Failure(
                Error.Infrastructure($"IMAP sync failed for mailbox {mailbox.Id}: {ex.Message}"));
        }
    }

    private static SearchQuery BuildBaseQuery(MailboxCursor cursor, SyncParameters parameters)
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
                    new UniqueId((uint)(synced.Uid!.Value + 1)),
                    UniqueId.MaxValue));
        }

        // Otherwise: use date filter
        var sinceDate = effectiveDate?.ToDateTime(TimeOnly.MinValue)
            ?? DateTime.UtcNow.AddDays(-90);
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
