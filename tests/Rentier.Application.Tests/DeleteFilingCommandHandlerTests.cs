using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.Application.Tests;

public class DeleteFilingCommandHandlerTests
{
    private readonly IFilingRepository _repo = Substitute.For<IFilingRepository>();
    private readonly DeleteFilingCommandHandler _sut;

    public DeleteFilingCommandHandlerTests()
    {
        _sut = new DeleteFilingCommandHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_WithExistingFilingId_CallsDeleteAsyncAndReturnsSuccess()
    {
        var id = Guid.NewGuid();

        var result = await _sut.HandleAsync(new DeleteFilingCommand(id));

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenFilingNotFound_IsIdempotentAndReturnsSuccess()
    {
        // DeleteAsync is a no-op when the id doesn't exist (by IFilingRepository contract)
        var id = Guid.NewGuid();
        _repo.DeleteAsync(id, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await _sut.HandleAsync(new DeleteFilingCommand(id));

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }
}
