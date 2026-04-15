using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.Application.Tests;

public class BulkDeleteFilingsCommandHandlerTests
{
    private readonly IFilingRepository _repo = Substitute.For<IFilingRepository>();
    private readonly BulkDeleteFilingsCommandHandler _sut;

    public BulkDeleteFilingsCommandHandlerTests()
        => _sut = new BulkDeleteFilingsCommandHandler(_repo);

    [Fact]
    public async Task HandleAsync_NullFilingIds_ReturnsDomainError()
    {
        var cmd = new BulkDeleteFilingsCommand(null!);
        var result = await _sut.HandleAsync(cmd);
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("BULK_DELETE_FILINGS_INVALID");
    }

    [Fact]
    public async Task HandleAsync_EmptyFilingIds_ReturnsDomainError()
    {
        var cmd = new BulkDeleteFilingsCommand(Array.Empty<Guid>());
        var result = await _sut.HandleAsync(cmd);
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("BULK_DELETE_FILINGS_INVALID");
    }

    [Fact]
    public async Task HandleAsync_ValidIds_CallsDeleteManyAndReturnsSuccess()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var cmd = new BulkDeleteFilingsCommand(ids);

        var result = await _sut.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(VoidResult.Value);
        await _repo.Received(1).DeleteManyAsync(ids, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_ReturnsFailure()
    {
        var ids = new[] { Guid.NewGuid() };
        _repo.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("DB error"));

        var result = await _sut.HandleAsync(new BulkDeleteFilingsCommand(ids));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("BULK_DELETE_FILINGS_FAILED");
        result.Error.Message.Should().Contain("DB error");
    }

    [Fact]
    public async Task HandleAsync_ForwardsCancellationToken()
    {
        var ids = new[] { Guid.NewGuid() };
        var cts = new CancellationTokenSource();
        await _sut.HandleAsync(new BulkDeleteFilingsCommand(ids), cts.Token);
        await _repo.Received(1).DeleteManyAsync(ids, cts.Token);
    }
}
