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
        importer.UpdateDetails("Importer", ReportType.IbkrCsv, null, mailbox.Id, string.Empty, string.Empty, @".*\.csv", string.Empty);

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
