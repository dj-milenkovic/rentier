using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.Application.Tests;

public class DeleteMailboxCommandHandlerTests
{
    private readonly IMailboxRepository _repo = Substitute.For<IMailboxRepository>();
    private readonly ICredentialStore _credentials = Substitute.For<ICredentialStore>();
    private readonly DeleteMailboxCommandHandler _handler;

    public DeleteMailboxCommandHandlerTests()
    {
        _handler = new DeleteMailboxCommandHandler(_repo, _credentials);
    }

    [Fact]
    public async Task HandleAsync_ExistingMailbox_DeletesCredentialThenRepo()
    {
        var id = Guid.NewGuid();
        var cmd = new DeleteMailboxCommand(id);

        var result = await _handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        await _credentials.Received(1).DeleteCredentialAsync(
            $"Rentier/Mailbox/{id}",
            Arg.Any<CancellationToken>());
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CredentialThrows_StillDeletesRepo()
    {
        var id = Guid.NewGuid();
        _credentials.DeleteCredentialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("credential not found"));

        var cmd = new DeleteMailboxCommand(id);

        var result = await _handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }
}
