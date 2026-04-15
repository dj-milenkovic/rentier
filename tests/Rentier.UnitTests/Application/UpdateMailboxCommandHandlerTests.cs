using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.UnitTests;

public class UpdateMailboxCommandHandlerTests
{
    private readonly IMailboxRepository _repo = Substitute.For<IMailboxRepository>();
    private readonly FakeCredentialStore _fakeCredentials = new();
    private readonly UpdateMailboxCommandHandler _handler;

    private static readonly DateOnly TestDate = new(2024, 1, 1);

    public UpdateMailboxCommandHandlerTests()
    {
        _handler = new UpdateMailboxCommandHandler(_repo, _fakeCredentials);
    }

    [Fact]
    public async Task HandleAsync_ValidUpdate_UpdatesRepoAndReturnsSuccess()
    {
        var existing = Mailbox.Create("imap.old.com", 993, "old@example.com");
        _repo.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var cmd = new UpdateMailboxCommand(existing.Id, "imap.new.com", 143, "new@example.com", null);

        var result = await _handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NotFound_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Mailbox?)null);

        var cmd = new UpdateMailboxCommand(id, "imap.example.com", 993, "user@example.com", null);

        var result = await _handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NOT_FOUND");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Mailbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNewPassword_UpdatesCredentialUsingCorrectKey()
    {
        var existing = Mailbox.Create("imap.example.com", 993, "user@example.com");
        _repo.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var cmd = new UpdateMailboxCommand(existing.Id, "imap.example.com", 993, "user@example.com", "newpass");

        await _handler.HandleAsync(cmd);

        var savedKey = _fakeCredentials.StoredKeys.Single();
        savedKey.Should().Be(CredentialKeys.MailboxPassword(existing.Id));
    }

    [Fact]
    public async Task HandleAsync_EmptyPassword_PreservesExistingCredential()
    {
        var existing = Mailbox.Create("imap.example.com", 993, "user@example.com");
        _repo.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var cmd = new UpdateMailboxCommand(existing.Id, "imap.example.com", 993, "user@example.com", "");

        await _handler.HandleAsync(cmd);

        _fakeCredentials.StoredKeys.Should().BeEmpty();
        await _repo.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SaveCredentialFails_PropagatesCredentialWriteFailed()
    {
        var existing = Mailbox.Create("imap.example.com", 993, "user@example.com");
        _repo.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var failingCredentials = Substitute.For<ICredentialStore>();
        failingCredentials.SaveCredentialAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Failure(Error.CredentialWriteFailed("OS store locked")));

        var handler = new UpdateMailboxCommandHandler(_repo, failingCredentials);
        var cmd = new UpdateMailboxCommand(existing.Id, "imap.example.com", 993, "user@example.com", "pass");

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_WRITE_FAILED");
    }
}
