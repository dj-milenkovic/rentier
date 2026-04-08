using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Xunit;

namespace Rentier.Application.Tests;

public class UpdateMailboxCommandHandlerTests
{
    private readonly IMailboxRepository _repo = Substitute.For<IMailboxRepository>();
    private readonly ICredentialStore _credentials = Substitute.For<ICredentialStore>();
    private readonly UpdateMailboxCommandHandler _handler;

    private static readonly DateOnly TestDate = new(2024, 1, 1);

    public UpdateMailboxCommandHandlerTests()
    {
        _handler = new UpdateMailboxCommandHandler(_repo, _credentials);
    }

    [Fact]
    public async Task HandleAsync_ValidUpdate_UpdatesRepoAndReturnsSuccess()
    {
        var existing = Mailbox.Create("imap.old.com", 993, "old@example.com", TestDate);
        _repo.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var cmd = new UpdateMailboxCommand(existing.Id, "imap.new.com", 143, "new@example.com", null, TestDate);

        var result = await _handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NotFound_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Mailbox?)null);

        var cmd = new UpdateMailboxCommand(id, "imap.example.com", 993, "user@example.com", null, TestDate);

        var result = await _handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NOT_FOUND");
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Mailbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNewPassword_UpdatesCredential()
    {
        var existing = Mailbox.Create("imap.example.com", 993, "user@example.com", TestDate);
        _repo.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var cmd = new UpdateMailboxCommand(existing.Id, "imap.example.com", 993, "user@example.com", "newpass", TestDate);

        await _handler.HandleAsync(cmd);

        await _credentials.Received(1).SaveCredentialAsync(
            Arg.Is<string>(k => k.Contains(existing.Id.ToString())),
            "newpass",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmptyPassword_PreservesExistingCredential()
    {
        var existing = Mailbox.Create("imap.example.com", 993, "user@example.com", TestDate);
        _repo.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var cmd = new UpdateMailboxCommand(existing.Id, "imap.example.com", 993, "user@example.com", "", TestDate);

        await _handler.HandleAsync(cmd);

        await _credentials.DidNotReceive().SaveCredentialAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }
}
