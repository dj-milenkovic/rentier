using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Tests.Common.Fakes;
using Rentier.Domain.Entities;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests.Application;

public class AddMailboxCommandHandlerTests
{
    private readonly IMailboxRepository _repo = Substitute.For<IMailboxRepository>();
    private readonly FakeCredentialStore _fakeCredentials = new();
    private readonly AddMailboxCommandHandler _handler;

    private static readonly DateOnly TestDate = new(2024, 1, 1);

    public AddMailboxCommandHandlerTests()
    {
        _handler = new AddMailboxCommandHandler(_repo, _fakeCredentials, TestClock.Provider());
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ReturnsSuccessWithGuid()
    {
        var cmd = new AddMailboxCommand("imap.example.com", 993, "user@example.com", null);

        var result = await _handler.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        await _repo.Received(1).AddAsync(Arg.Any<Mailbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NewMailbox_CursorStartsNinetyDaysBeforeUtcToday()
    {
        // 2026-01-01 00:30 local (UTC+1) is still 2025-12-31 in UTC; cursor dates are UTC dates.
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2025, 12, 31, 23, 30, 0, TimeSpan.Zero),
            FixedTimeProvider.FixedOffsetZone(TimeSpan.FromHours(1)));
        var handler = new AddMailboxCommandHandler(_repo, _fakeCredentials, clock);
        Mailbox? saved = null;
        await _repo.AddAsync(Arg.Do<Mailbox>(m => saved = m), Arg.Any<CancellationToken>());

        await handler.HandleAsync(
            new AddMailboxCommand("imap.example.com", 993, "user@example.com", null),
            TestContext.Current.CancellationToken);

        saved!.Cursor.Should().Be(new MailboxCursor.SyncedTo(new DateOnly(2025, 12, 31).AddDays(-90), null));
    }

    [Fact]
    public async Task HandleAsync_WithPassword_SavesCredentialWithCorrectKey()
    {
        var cmd = new AddMailboxCommand("imap.example.com", 993, "user@example.com", "secret123");

        var result = await _handler.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var savedKey = _fakeCredentials.StoredKeys.Single();
        savedKey.Should().EndWith("/password");
        savedKey.Should().StartWith("Rentier/Mailbox/");
        await _repo.Received(1).AddAsync(Arg.Any<Mailbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithPassword_UsesCredentialKeysFormat()
    {
        var cmd = new AddMailboxCommand("imap.example.com", 993, "user@example.com", "secret123");

        var result = await _handler.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var savedKey = _fakeCredentials.StoredKeys.Single();
        // Key format: Rentier/Mailbox/{guid}/password
        savedKey.Should().MatchRegex(@"^Rentier/Mailbox/[0-9a-f\-]+/password$");
    }

    [Fact]
    public async Task HandleAsync_NullPassword_SkipsCredentialSave()
    {
        var cmd = new AddMailboxCommand("imap.example.com", 993, "user@example.com", null);

        await _handler.HandleAsync(cmd, TestContext.Current.CancellationToken);

        _fakeCredentials.StoredKeys.Should().BeEmpty();
        await _repo.Received(1).AddAsync(Arg.Any<Mailbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmptyPassword_SkipsCredentialSave()
    {
        var cmd = new AddMailboxCommand("imap.example.com", 993, "user@example.com", "");

        await _handler.HandleAsync(cmd, TestContext.Current.CancellationToken);

        _fakeCredentials.StoredKeys.Should().BeEmpty();
        await _repo.Received(1).AddAsync(Arg.Any<Mailbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidHost_ReturnsDomainError()
    {
        var cmd = new AddMailboxCommand("", 993, "user@example.com", null);

        var result = await _handler.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("MAILBOX_VALIDATION_FAILED");
        await _repo.DidNotReceive().AddAsync(Arg.Any<Mailbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SaveCredentialFails_ReturnsFailureWithoutSavingToRepo()
    {
        var failingCredentials = Substitute.For<ICredentialStore>();
        failingCredentials.SaveCredentialAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Failure(Error.CredentialWriteFailed("OS store failure")));

        var handler = new AddMailboxCommandHandler(_repo, failingCredentials, TestClock.Provider());
        var cmd = new AddMailboxCommand("imap.example.com", 993, "user@example.com", "pass");

        var result = await handler.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_WRITE_FAILED");
        await _repo.DidNotReceive().AddAsync(Arg.Any<Mailbox>(), Arg.Any<CancellationToken>());
    }
}
