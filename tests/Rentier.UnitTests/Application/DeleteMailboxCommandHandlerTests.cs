using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.UnitTests;

public class DeleteMailboxCommandHandlerTests
{
    private readonly IMailboxRepository _repo = Substitute.For<IMailboxRepository>();
    private readonly FakeCredentialStore _fakeCredentials = new();
    private readonly DeleteMailboxCommandHandler _handler;

    public DeleteMailboxCommandHandlerTests()
    {
        _handler = new DeleteMailboxCommandHandler(_repo, _fakeCredentials, NullLogger<DeleteMailboxCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ExistingMailbox_DeletesCredentialAndRepo()
    {
        var id = Guid.NewGuid();
        var expectedKey = CredentialKeys.MailboxPassword(id);
        _fakeCredentials.StoredKeys.Should().BeEmpty(); // ensure clean state

        var cmd = new DeleteMailboxCommand(id);

        var result = await _handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DeletesCredentialWithCorrectKey()
    {
        var id = Guid.NewGuid();
        // Pre-seed a credential so we can verify it was deleted
        await _fakeCredentials.SaveCredentialAsync(CredentialKeys.MailboxPassword(id), "secret");

        var cmd = new DeleteMailboxCommand(id);
        await _handler.HandleAsync(cmd);

        _fakeCredentials.StoredKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_CredentialAlreadyAbsent_SucceedsIdempotently()
    {
        // No credential seeded — delete should still succeed
        var id = Guid.NewGuid();
        var cmd = new DeleteMailboxCommand(id);

        var result = await _handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_FullLifecycle_AddThenDeleteRemovesCredential()
    {
        var id = Guid.NewGuid();
        var key = CredentialKeys.MailboxPassword(id);

        // Simulate Add
        await _fakeCredentials.SaveCredentialAsync(key, "my-password");
        _fakeCredentials.StoredKeys.Should().Contain(key);

        // Simulate Delete
        var handler = new DeleteMailboxCommandHandler(_repo, _fakeCredentials, NullLogger<DeleteMailboxCommandHandler>.Instance);
        await handler.HandleAsync(new DeleteMailboxCommand(id));

        _fakeCredentials.StoredKeys.Should().NotContain(key);
    }

    [Fact]
    public async Task HandleAsync_CredentialDeleteFailsWithUnexpectedError_StillDeletesFromDbAndReturnsSuccess()
    {
        var id = Guid.NewGuid();
        var failingCredentials = Substitute.For<ICredentialStore>();
        failingCredentials.DeleteCredentialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Failure(Error.CredentialDeleteFailed("OS store locked")));

        var handler = new DeleteMailboxCommandHandler(_repo, failingCredentials, NullLogger<DeleteMailboxCommandHandler>.Instance);
        var cmd = new DeleteMailboxCommand(id);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }
}
