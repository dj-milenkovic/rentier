using FluentAssertions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Sync;
using Xunit;

namespace Rentier.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class ImapMailboxSyncServiceTests
{
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
