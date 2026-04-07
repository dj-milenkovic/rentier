using FluentAssertions;
using NSubstitute;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Sync;
using Xunit;

namespace Rentier.Infrastructure.Tests;

public class ImapMailboxSyncServiceTests
{
    private static Mailbox MakeMailbox()
        => Mailbox.Create("imap.example.com", 993, "user@example.com", new DateOnly(2024, 1, 1));

    [Fact]
    public async Task SyncAsync_NoPassword_ReturnsFailure()
    {
        var credStore = Substitute.For<ICredentialStore>();
        credStore.GetCredentialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var svc = new ImapMailboxSyncService(
            Substitute.For<IReportRepository>(),
            Substitute.For<IMailboxRepository>(),
            credStore);

        var mailbox = MakeMailbox();
        var result = await svc.SyncAsync(mailbox, Array.Empty<Importer>(), null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("No password found");
    }

    [Fact]
    public async Task SyncAsync_EmptyPassword_ReturnsFailure()
    {
        var credStore = Substitute.For<ICredentialStore>();
        credStore.GetCredentialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(string.Empty);

        var svc = new ImapMailboxSyncService(
            Substitute.For<IReportRepository>(),
            Substitute.For<IMailboxRepository>(),
            credStore);

        var mailbox = MakeMailbox();
        var result = await svc.SyncAsync(mailbox, Array.Empty<Importer>(), null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("INFRASTRUCTURE_ERROR");
    }

    [Fact]
    public async Task SyncAsync_NullMailbox_ThrowsArgumentNullException()
    {
        var svc = new ImapMailboxSyncService(
            Substitute.For<IReportRepository>(),
            Substitute.For<IMailboxRepository>(),
            Substitute.For<ICredentialStore>());

        var act = async () => await svc.SyncAsync(null!, Array.Empty<Importer>(), null, CancellationToken.None);

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
        var act = async () => await svc.SyncAsync(mailbox, null!, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void BuildReportName_ShortName_ReturnsCombinedName()
    {
        var result = ImapMailboxSyncService.BuildReportName("Subject", "file.csv");

        result.Should().Be("Subject_file.csv");
    }

    [Fact]
    public void BuildReportName_LongName_TruncatesTo500()
    {
        var longSubject = new string('S', 300);
        var longFile = new string('F', 300);

        var result = ImapMailboxSyncService.BuildReportName(longSubject, longFile);

        result.Should().HaveLength(500);
        result.Should().StartWith(longSubject[..300]);
    }
}
