using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.Application.Tests;

public class AddMailboxCommandHandlerTests
{
    private readonly IMailboxRepository _repo = Substitute.For<IMailboxRepository>();
    private readonly ICredentialStore _credentials = Substitute.For<ICredentialStore>();
    private readonly AddMailboxCommandHandler _handler;

    private static readonly DateOnly TestDate = new(2024, 1, 1);

    public AddMailboxCommandHandlerTests()
    {
        _handler = new AddMailboxCommandHandler(_repo, _credentials);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ReturnsSuccessWithGuid()
    {
        var cmd = new AddMailboxCommand("imap.example.com", 993, "user@example.com", null, TestDate);

        var result = await _handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        await _repo.Received(1).AddAsync(Arg.Any<Domain.Entities.Mailbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithPassword_SavesCredentialBeforeAdd()
    {
        var cmd = new AddMailboxCommand("imap.example.com", 993, "user@example.com", "secret123", TestDate);

        var result = await _handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        await _credentials.Received(1).SaveCredentialAsync(
            Arg.Is<string>(k => k.StartsWith("Rentier/Mailbox/")),
            "secret123",
            Arg.Any<CancellationToken>());
        await _repo.Received(1).AddAsync(Arg.Any<Domain.Entities.Mailbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NullPassword_SkipsCredentialSave()
    {
        var cmd = new AddMailboxCommand("imap.example.com", 993, "user@example.com", null, TestDate);

        await _handler.HandleAsync(cmd);

        await _credentials.DidNotReceive().SaveCredentialAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).AddAsync(Arg.Any<Domain.Entities.Mailbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmptyPassword_SkipsCredentialSave()
    {
        var cmd = new AddMailboxCommand("imap.example.com", 993, "user@example.com", "", TestDate);

        await _handler.HandleAsync(cmd);

        await _credentials.DidNotReceive().SaveCredentialAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).AddAsync(Arg.Any<Domain.Entities.Mailbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidHost_ReturnsDomainError()
    {
        var cmd = new AddMailboxCommand("", 993, "user@example.com", null, TestDate);

        var result = await _handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DOMAIN_VALIDATION");
        await _repo.DidNotReceive().AddAsync(Arg.Any<Domain.Entities.Mailbox>(), Arg.Any<CancellationToken>());
    }
}
