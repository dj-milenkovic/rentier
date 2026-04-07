using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.Application.Tests;

public sealed class DeleteImporterCommandHandlerTests
{
    private readonly IImporterRepository _repo = Substitute.For<IImporterRepository>();
    private readonly DeleteImporterCommandHandler _sut;

    public DeleteImporterCommandHandlerTests()
    {
        _sut = new DeleteImporterCommandHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_ExistingImporter_DeletesAndReturnsSuccess()
    {
        var id = Guid.NewGuid();
        var cmd = new DeleteImporterCommand(id);

        var result = await _sut.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonExistentId_StillReturnsSuccess()
    {
        var id = Guid.NewGuid();
        _repo.DeleteAsync(id, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await _sut.HandleAsync(new DeleteImporterCommand(id));

        result.IsSuccess.Should().BeTrue();
    }
}
