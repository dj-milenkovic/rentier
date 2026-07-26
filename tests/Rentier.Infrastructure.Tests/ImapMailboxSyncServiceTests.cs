using System.Text;
using FluentAssertions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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

[Trait("Category", "Integration")]
public class ImapMailboxSyncServiceTests
{
    private sealed class TestableImapMailboxSyncService : ImapMailboxSyncService
    {
        private readonly ImapClient _client;

        public TestableImapMailboxSyncService(
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

    private static Importer MakeImporter(string attachmentRegex = @".*\.csv")
    {
        var importer = Importer.Create("Importer");
        importer.UpdateDetails("Importer", ReportType.IbkrCsv, null, null, string.Empty, string.Empty, attachmentRegex, string.Empty);
        return importer;
    }

    private static MimeMessage MakeMessageWithAttachment(string subject, string filename, string content = "a,b\n1,2")
    {
        var message = new MimeMessage
        {
            Subject = subject,
            Date = new DateTimeOffset(2024, 3, 15, 10, 0, 0, TimeSpan.Zero)
        };
        var bodyBuilder = new BodyBuilder { TextBody = "body" };
        bodyBuilder.Attachments.Add(filename, Encoding.UTF8.GetBytes(content));
        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private static MimeMessage LoadRawMessage(string rawMime)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rawMime));
        return MimeMessage.Load(stream);
    }

    private static ICredentialStore MakeCredentialStore(string password = "secret")
    {
        var credentialStore = Substitute.For<ICredentialStore>();
        credentialStore.GetCredentialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, Error>.Success(password));
        return credentialStore;
    }

    private static (ImapClient Client, IMailFolder Inbox) MakeConnectedClient(Mailbox mailbox, string password = "secret")
    {
        var client = Substitute.For<ImapClient>();
        var inbox = Substitute.For<IMailFolder>();
        client.Inbox.Returns(inbox);

        client.ConnectAsync(mailbox.Host, mailbox.Port, SecureSocketOptions.SslOnConnect, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        client.AuthenticateAsync(mailbox.Username, password, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        inbox.OpenAsync(FolderAccess.ReadOnly, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(FolderAccess.ReadOnly));
        inbox.CloseAsync(false, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        client.DisconnectAsync(true, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return (client, inbox);
    }

    [Fact]
    public async Task SyncAsync_CredentialNotFound_ReturnsFailure()
    {
        var credStore = Substitute.For<ICredentialStore>();
        credStore.GetCredentialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, Error>.Failure(Error.CredentialNotFound("Rentier/Mailbox/test/password")));

        var svc = new ImapMailboxSyncService(
            Substitute.For<IReportRepository>(),
            Substitute.For<IMailboxRepository>(),
            credStore);

        var mailbox = MakeMailbox();
        var result = await svc.SyncAsync(mailbox, Array.Empty<Importer>(), SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact]
    public async Task SyncAsync_ProviderUnavailable_ReturnsFailure()
    {
        var credStore = Substitute.For<ICredentialStore>();
        credStore.GetCredentialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, Error>.Failure(Error.ProviderUnavailable("Daemon not running")));

        var svc = new ImapMailboxSyncService(
            Substitute.For<IReportRepository>(),
            Substitute.For<IMailboxRepository>(),
            credStore);

        var mailbox = MakeMailbox();
        var result = await svc.SyncAsync(mailbox, Array.Empty<Importer>(), SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PROVIDER_UNAVAILABLE");
    }

    [Fact]
    public async Task SyncAsync_NullMailbox_ThrowsArgumentNullException()
    {
        var svc = new ImapMailboxSyncService(
            Substitute.For<IReportRepository>(),
            Substitute.For<IMailboxRepository>(),
            Substitute.For<ICredentialStore>());

        var act = async () => await svc.SyncAsync(null!, Array.Empty<Importer>(), SyncParameters.Default, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SyncAsync_NullImporters_ThrowsArgumentNullException()
    {
        var svc = new ImapMailboxSyncService(
            Substitute.For<IReportRepository>(),
            Substitute.For<IMailboxRepository>(),
            Substitute.For<ICredentialStore>());

        var mailbox = MakeMailbox();
        var act = async () => await svc.SyncAsync(mailbox, null!, SyncParameters.Default, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SyncAsync_EmailProcessed_ReportsPerEmailProgressEntry()
    {
        var mailbox = MakeMailbox();
        var importer = Importer.Create("Importer");
        importer.UpdateDetails(new ImporterDetails("Importer", ReportType.IbkrCsv, null, mailbox.Id, string.Empty, string.Empty, @".*\.csv", string.Empty));

        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.ExistsByImporterAndNameAsync(importer.Id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = Substitute.For<ICredentialStore>();
        credentialStore.GetCredentialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, Error>.Success("secret"));

        var client = Substitute.For<ImapClient>();
        var inbox = Substitute.For<IMailFolder>();
        client.Inbox.Returns(inbox);

        var uid = new UniqueId(42);
        var message = new MimeMessage
        {
            Subject = "Dividend statement",
            Date = new DateTimeOffset(2024, 3, 15, 10, 0, 0, TimeSpan.Zero)
        };
        var bodyBuilder = new BodyBuilder { TextBody = "body" };
        bodyBuilder.Attachments.Add("statement.csv", Encoding.UTF8.GetBytes("a,b\n1,2"));
        message.Body = bodyBuilder.ToMessageBody();

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

        var progress = Substitute.For<IProgress<SyncProgressEntry>>();
        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, progress, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        progress.Received(1).Report(Arg.Is<SyncProgressEntry>(entry =>
            entry!.Message == "Downloading email 1/1: Dividend statement"
            && entry.Severity == SyncProgressSeverity.Info));
    }

    [Fact]
    public async Task SyncAsync_OneImporterFailsSearch_OtherImporterStillProcessed()
    {
        var mailbox = MakeMailbox();
        var failingImporter = MakeImporter(attachmentRegex: "(");
        var workingImporter = MakeImporter();

        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.ExistsByImporterAndNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = MakeCredentialStore();
        var (client, inbox) = MakeConnectedClient(mailbox);

        var uid = new UniqueId(1);
        var message = MakeMessageWithAttachment("Dividend statement", "statement.csv");

        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { uid }));
        inbox.GetMessageAsync(uid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(message));

        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(
            mailbox, new[] { failingImporter, workingImporter }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(1);
        result.Value.Errors.Should().ContainSingle(e => e.StartsWith($"Importer {failingImporter.Id}:"));
    }

    [Fact]
    public async Task SyncAsync_MessageThrowsForOneUid_OtherUidStillProcessedAndMaxUidReflectsSuccessOnly()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter();

        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.ExistsByImporterAndNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = MakeCredentialStore();
        var (client, inbox) = MakeConnectedClient(mailbox);

        var failingUid = new UniqueId(10);
        var succeedingUid = new UniqueId(20);
        var message = MakeMessageWithAttachment("Dividend statement", "statement.csv");

        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { failingUid, succeedingUid }));
        inbox.GetMessageAsync(failingUid, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("mailbox connection dropped"));
        inbox.GetMessageAsync(succeedingUid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(message));

        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(1);
        result.Value.Errors.Should().ContainSingle(e => e.StartsWith($"Importer {importer.Id} UID {failingUid.Id}:"));
        mailbox.Cursor.Should().BeOfType<MailboxCursor.SyncedTo>()
            .Which.Uid.Should().Be(succeedingUid.Id);
    }

    [Fact]
    public async Task SyncAsync_SecondAttachmentThrows_FirstAttachmentsReportCountIsPreserved()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter();

        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.ExistsByImporterAndNameAsync(importer.Id, Arg.Is<string>(n => n != null && n.Contains("a.csv")), Arg.Any<CancellationToken>())
            .Returns(false);
        reportRepository.ExistsByImporterAndNameAsync(importer.Id, Arg.Is<string>(n => n != null && n.Contains("b.csv")), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("database unavailable"));

        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = MakeCredentialStore();
        var (client, inbox) = MakeConnectedClient(mailbox);

        var uid = new UniqueId(1);
        var message = new MimeMessage
        {
            Subject = "Two attachments",
            Date = new DateTimeOffset(2024, 3, 15, 10, 0, 0, TimeSpan.Zero)
        };
        var bodyBuilder = new BodyBuilder { TextBody = "body" };
        bodyBuilder.Attachments.Add("a.csv", Encoding.UTF8.GetBytes("a,b\n1,2"));
        bodyBuilder.Attachments.Add("b.csv", Encoding.UTF8.GetBytes("c,d\n3,4"));
        message.Body = bodyBuilder.ToMessageBody();

        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { uid }));
        inbox.GetMessageAsync(uid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(message));

        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(1);
        result.Value.Errors.Should().ContainSingle(e => e.StartsWith($"Importer {importer.Id} UID {uid.Id}:"));
        await reportRepository.Received(1).AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_EmptyAttachmentRegex_SkipsAllAttachments()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter(attachmentRegex: string.Empty);

        var reportRepository = Substitute.For<IReportRepository>();
        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = MakeCredentialStore();
        var (client, inbox) = MakeConnectedClient(mailbox);

        var uid = new UniqueId(1);
        var message = MakeMessageWithAttachment("Dividend statement", "statement.csv");

        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { uid }));
        inbox.GetMessageAsync(uid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(message));

        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(0);
        await reportRepository.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_AttachmentRegexDoesNotMatchFilename_SkipsAttachment()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter(attachmentRegex: @".*\.pdf");

        var reportRepository = Substitute.For<IReportRepository>();
        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = MakeCredentialStore();
        var (client, inbox) = MakeConnectedClient(mailbox);

        var uid = new UniqueId(1);
        var message = MakeMessageWithAttachment("Dividend statement", "statement.csv");

        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { uid }));
        inbox.GetMessageAsync(uid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(message));

        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(0);
        await reportRepository.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_AttachmentWithNoFilename_SkipsAttachment()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter();

        var reportRepository = Substitute.For<IReportRepository>();
        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = MakeCredentialStore();
        var (client, inbox) = MakeConnectedClient(mailbox);

        var uid = new UniqueId(1);
        var message = new MimeMessage
        {
            Subject = "No filename attachment",
            Date = new DateTimeOffset(2024, 3, 15, 10, 0, 0, TimeSpan.Zero)
        };
        // Nameless MimePart: no Content-Disposition filename and no Content-Type name parameter,
        // so FileName resolves to null and exercises the empty-filename skip branch.
        var namelessPart = new MimePart("application", "octet-stream")
        {
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("data")))
        };
        var multipart = new Multipart("mixed") { namelessPart };
        message.Body = multipart;

        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { uid }));
        inbox.GetMessageAsync(uid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(message));

        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(0);
        await reportRepository.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ReportAlreadyExists_SkipsPersisting()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter();

        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.ExistsByImporterAndNameAsync(importer.Id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = MakeCredentialStore();
        var (client, inbox) = MakeConnectedClient(mailbox);

        var uid = new UniqueId(1);
        var message = MakeMessageWithAttachment("Dividend statement", "statement.csv");

        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { uid }));
        inbox.GetMessageAsync(uid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(message));

        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(0);
        await reportRepository.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_MultipleImporters_TracksMaxUidAcrossAllImporters()
    {
        var mailbox = MakeMailbox();
        var importer1 = MakeImporter();
        var importer2 = MakeImporter();

        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.ExistsByImporterAndNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = MakeCredentialStore();
        var (client, inbox) = MakeConnectedClient(mailbox);

        var lowUid = new UniqueId(5);
        var highUid = new UniqueId(10);
        var lowMessage = MakeMessageWithAttachment("Low uid", "low.csv");
        var highMessage = MakeMessageWithAttachment("High uid", "high.csv");

        // Both importers search against the same mailbox; distinguish by which importer's
        // query object is passed rather than by importer, since both use the same base query.
        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { lowUid }), Task.FromResult<IList<UniqueId>>(new[] { highUid }));
        inbox.GetMessageAsync(lowUid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(lowMessage));
        inbox.GetMessageAsync(highUid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(highMessage));

        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(
            mailbox, new[] { importer1, importer2 }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(2);
        mailbox.Cursor.Should().BeOfType<MailboxCursor.SyncedTo>()
            .Which.Uid.Should().Be(highUid.Id);
    }

    [Fact]
    public async Task SyncAsync_MessageWithNoDateHeader_UsesUnixEpochSentinelForEmailDate()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter();

        Report? capturedReport = null;
        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.ExistsByImporterAndNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        reportRepository.AddAsync(Arg.Do<Report>(r => capturedReport = r), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = MakeCredentialStore();
        var (client, inbox) = MakeConnectedClient(mailbox);

        var uid = new UniqueId(1);
        // A real IMAP message missing its Date header — MimeKit's parser leaves
        // MimeMessage.Date at DateTimeOffset.MinValue in that case, which is the
        // scenario the Unix-epoch sentinel fallback guards against.
        var message = LoadRawMessage(
            "From: sender@example.com\r\n" +
            "To: user@example.com\r\n" +
            "Subject: No date header\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Content-Type: multipart/mixed; boundary=\"BOUNDARY\"\r\n" +
            "\r\n" +
            "--BOUNDARY\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "body\r\n" +
            "--BOUNDARY\r\n" +
            "Content-Type: text/csv; name=\"statement.csv\"\r\n" +
            "Content-Disposition: attachment; filename=\"statement.csv\"\r\n" +
            "Content-Transfer-Encoding: 8bit\r\n" +
            "\r\n" +
            "a,b\r\n1,2\r\n" +
            "--BOUNDARY--\r\n");

        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { uid }));
        inbox.GetMessageAsync(uid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(message));

        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedReport.Should().NotBeNull();
        capturedReport!.EmailDate.Should().Be(DateOnly.FromDateTime(DateTime.UnixEpoch));
    }

    [Fact]
    public async Task SyncAsync_MessageWithNoSubjectHeader_BuildsReportNameWithEmptySubject()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter();

        Report? capturedReport = null;
        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.ExistsByImporterAndNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        reportRepository.AddAsync(Arg.Do<Report>(r => capturedReport = r), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = MakeCredentialStore();
        var (client, inbox) = MakeConnectedClient(mailbox);

        var uid = new UniqueId(1);
        // A real IMAP message missing its Subject header — MimeMessage.Subject is null
        // in that case, exercising "message.Subject ?? string.Empty".
        var message = LoadRawMessage(
            "From: sender@example.com\r\n" +
            "To: user@example.com\r\n" +
            "Date: Fri, 15 Mar 2024 10:00:00 +0000\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Content-Type: multipart/mixed; boundary=\"BOUNDARY\"\r\n" +
            "\r\n" +
            "--BOUNDARY\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "body\r\n" +
            "--BOUNDARY\r\n" +
            "Content-Type: text/csv; name=\"statement.csv\"\r\n" +
            "Content-Disposition: attachment; filename=\"statement.csv\"\r\n" +
            "Content-Transfer-Encoding: 8bit\r\n" +
            "\r\n" +
            "a,b\r\n1,2\r\n" +
            "--BOUNDARY--\r\n");

        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { uid }));
        inbox.GetMessageAsync(uid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(message));

        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedReport.Should().NotBeNull();
        capturedReport!.ReportName.Should().Be("2024-03-15__statement.csv");
    }

    [Fact]
    public async Task SyncAsync_UidsProcessedOutOfOrder_MaxUidTracksHighestNotLastProcessed()
    {
        var mailbox = MakeMailbox();
        var importer = MakeImporter();

        var reportRepository = Substitute.For<IReportRepository>();
        reportRepository.ExistsByImporterAndNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var mailboxRepository = Substitute.For<IMailboxRepository>();
        var credentialStore = MakeCredentialStore();
        var (client, inbox) = MakeConnectedClient(mailbox);

        var highUid = new UniqueId(20);
        var lowUid = new UniqueId(10);
        var highMessage = MakeMessageWithAttachment("High uid first", "high.csv");
        var lowMessage = MakeMessageWithAttachment("Low uid second", "low.csv");

        // Highest UID is searched/processed first so the low UID processed afterwards
        // exercises the branch where the running max is retained rather than overwritten.
        inbox.SearchAsync(Arg.Any<SearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<UniqueId>>(new[] { highUid, lowUid }));
        inbox.GetMessageAsync(highUid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(highMessage));
        inbox.GetMessageAsync(lowUid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(lowMessage));

        var svc = new TestableImapMailboxSyncService(reportRepository, mailboxRepository, credentialStore, client);

        var result = await svc.SyncAsync(mailbox, new[] { importer }, SyncParameters.Default, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(2);
        mailbox.Cursor.Should().BeOfType<MailboxCursor.SyncedTo>()
            .Which.Uid.Should().Be(highUid.Id);
    }

    [Fact]
    public void BuildReportName_ShortName_ReturnsCombinedName()
    {
        var date = new DateOnly(2024, 3, 15);

        var result = ImapMailboxSyncService.BuildReportName(date, "Subject", "file.csv");

        result.Should().Be("2024-03-15_Subject_file.csv");
    }

    [Fact]
    public void BuildReportName_LongName_TruncatesTo500()
    {
        var date = new DateOnly(2024, 3, 15);
        var longSubject = new string('S', 300);
        var longFile = new string('F', 300);

        var result = ImapMailboxSyncService.BuildReportName(date, longSubject, longFile);

        result.Should().HaveLength(500);
        result.Should().StartWith("2024-03-15_");
    }
}
