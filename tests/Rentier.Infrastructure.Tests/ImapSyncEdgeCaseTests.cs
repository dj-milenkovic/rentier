using System.Text;
using FluentAssertions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Sync;
using Xunit;

namespace Rentier.Infrastructure.Tests;

/// <summary>
/// Edge-case tests for ImapMailboxSyncService: missing Date header, non-ASCII
/// subjects, and attachment encoding variants.
/// </summary>
[Trait("Category", "Integration")]
public class ImapSyncEdgeCaseTests
{
    private sealed class TestableService : ImapMailboxSyncService
    {
        private readonly ImapClient _client;

        public TestableService(
            IReportRepository reportRepository,
            IMailboxRepository mailboxRepository,
            ICredentialStore credentialStore,
            ImapClient client)
            : base(reportRepository, mailboxRepository, credentialStore)
        {
            _client = client;
        }

        protected override ImapClient CreateClient() => _client;
    }

    private static Mailbox MakeMailbox()
        => Mailbox.Create("imap.example.com", 993, "user@example.com");

    private static Importer MakeImporter(Guid mailboxId)
    {
        var importer = Importer.Create("Importer");
        importer.UpdateDetails("Importer", ReportType.IbkrCsv, null, mailboxId,
            string.Empty, string.Empty, @".*\.csv", string.Empty);
        return importer;
    }

    private static (IReportRepository, IMailboxRepository, ICredentialStore) MakeDefaultDependencies()
    {
        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.ExistsByImporterAndNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var mailboxRepo = Substitute.For<IMailboxRepository>();

        var credStore = Substitute.For<ICredentialStore>();
        credStore.GetCredentialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, Error>.Success("secret"));

        return (reportRepo, mailboxRepo, credStore);
    }

    private static ImapClient MakeImapClient(Mailbox mailbox, MimeMessage message)
    {
        var client = Substitute.For<ImapClient>();
        var inbox = Substitute.For<IMailFolder>();
        client.Inbox.Returns(inbox);

        var uid = new UniqueId(1);

        client.ConnectAsync(mailbox.Host, mailbox.Port, SecureSocketOptions.SslOnConnect, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        client.AuthenticateAsync(mailbox.Username, "secret", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        inbox.OpenAsync(FolderAccess.ReadOnly, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(FolderAccess.ReadOnly));
        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { uid }));
        inbox.GetMessageAsync(uid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(message));
        inbox.CloseAsync(false, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        client.DisconnectAsync(true, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return client;
    }

    // ── BuildReportName static helpers ────────────────────────────────────────

    /// <summary>S16: Non-ASCII (Serbian Cyrillic) subject is preserved in the report name.</summary>
    [Fact]
    public void BuildReportName_CyrillicSubject_PreservedInReportName()
    {
        var date = new DateOnly(2024, 3, 15);
        var cyrillicSubject = "Дивиденда ACME";

        var result = ImapMailboxSyncService.BuildReportName(date, cyrillicSubject, "statement.csv");

        result.Should().StartWith("2024-03-15_");
        result.Should().Contain("Дивиденда ACME");
        result.Should().EndWith("_statement.csv");
    }

    [Fact]
    public void BuildReportName_EmptySubject_FallsBackToDate()
    {
        var date = new DateOnly(2024, 3, 15);

        var result = ImapMailboxSyncService.BuildReportName(date, string.Empty, "file.csv");

        result.Should().StartWith("2024-03-15_");
        result.Should().Contain("file.csv");
    }

    [Fact]
    public void BuildReportName_ExactlyAt500Chars_NotTruncated()
    {
        var date = new DateOnly(2024, 3, 15);
        // "2024-03-15_" = 11 chars, "_" + "x" = 2 chars → subject fills the rest
        // Total = 11 + subjectLen + 1 + fileLen = 500
        var fileName = "a.csv"; // 5
        // subjectLen = 500 - 11 - 1 - 5 = 483
        var subject = new string('S', 483);

        var result = ImapMailboxSyncService.BuildReportName(date, subject, fileName);

        result.Should().HaveLength(500);
    }

    [Fact]
    public void BuildReportName_Over500Chars_TruncatesTo500()
    {
        var date = new DateOnly(2024, 3, 15);
        var subject = new string('S', 400);
        var file = new string('F', 400);

        var result = ImapMailboxSyncService.BuildReportName(date, subject, file);

        result.Should().HaveLength(500);
    }

    // ── Missing Date header → fall back to today ───────────────────────────────

    /// <summary>
    /// S16: When a message has no Date header (MimeKit returns DateTimeOffset.MinValue),
    /// the sync service falls back to today's UTC date and creates the report.
    /// </summary>
    [Fact]
    public async Task SyncAsync_MessageWithMissingDateHeader_FallsBackToTodayAndCreatesReport()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter(mailbox.Id);
        var (reportRepo, mailboxRepo, credStore) = MakeDefaultDependencies();

        // Message with NO Date header — MimeKit sets Date to DateTimeOffset.MinValue
        var message = new MimeMessage();
        message.Subject = "Dividend statement";
        // Intentionally do NOT set message.Date → it stays at DateTimeOffset.MinValue
        var bodyBuilder = new BodyBuilder { TextBody = "body" };
        bodyBuilder.Attachments.Add("statement.csv", Encoding.UTF8.GetBytes("col1,col2\n1,2"));
        message.Body = bodyBuilder.ToMessageBody();

        var client = MakeImapClient(mailbox, message);
        var svc = new TestableService(reportRepo, mailboxRepo, credStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(1);

        // The report name should use today's date (not 0001-01-01)
        await reportRepo.Received(1).AddAsync(
            Arg.Is<Report>(r => r.ReportName.StartsWith(DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"))),
            Arg.Any<CancellationToken>());
    }

    // ── Base64 attachment decoding ─────────────────────────────────────────────

    /// <summary>
    /// S16: An attachment with explicit base64 content-transfer-encoding must be
    /// decoded correctly so the stored content matches the original bytes.
    /// </summary>
    [Fact]
    public async Task SyncAsync_Base64EncodedAttachment_DecodesAndStoresCorrectContent()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter(mailbox.Id);
        var (reportRepo, mailboxRepo, credStore) = MakeDefaultDependencies();

        var originalContent = Encoding.UTF8.GetBytes("col1,col2\nval1,val2");

        // Build message with base64-encoded attachment via MimeKit BodyBuilder
        var message = new MimeMessage
        {
            Subject = "Dividend statement",
            Date = new DateTimeOffset(2024, 3, 15, 10, 0, 0, TimeSpan.Zero)
        };
        var bodyBuilder = new BodyBuilder { TextBody = "body" };
        // Add attachment — BodyBuilder/MimePart will use base64 encoding by default for binary
        var part = (MimePart)bodyBuilder.Attachments.Add("statement.csv", originalContent);
        part.ContentTransferEncoding = ContentEncoding.Base64;
        message.Body = bodyBuilder.ToMessageBody();

        var client = MakeImapClient(mailbox, message);
        var svc = new TestableService(reportRepo, mailboxRepo, credStore, client);

        byte[]? savedContent = null;
        await reportRepo.AddAsync(
            Arg.Do<Report>(r => savedContent = r.AttachmentContent),
            Arg.Any<CancellationToken>());

        await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        savedContent.Should().NotBeNull();
        savedContent.Should().Equal(originalContent);
    }

    // ── Duplicate name skip ───────────────────────────────────────────────────

    [Fact]
    public async Task SyncAsync_AttachmentAlreadyExists_SkipsWithoutCreatingReport()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter(mailbox.Id);
        var (reportRepo, mailboxRepo, credStore) = MakeDefaultDependencies();

        // Return "already exists" for any report name check
        reportRepo.ExistsByImporterAndNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var message = new MimeMessage
        {
            Subject = "Dividend statement",
            Date = new DateTimeOffset(2024, 3, 15, 10, 0, 0, TimeSpan.Zero)
        };
        var bodyBuilder = new BodyBuilder { TextBody = "body" };
        bodyBuilder.Attachments.Add("statement.csv", Encoding.UTF8.GetBytes("data"));
        message.Body = bodyBuilder.ToMessageBody();

        var client = MakeImapClient(mailbox, message);
        var svc = new TestableService(reportRepo, mailboxRepo, credStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(0);
        await reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
    }
}
