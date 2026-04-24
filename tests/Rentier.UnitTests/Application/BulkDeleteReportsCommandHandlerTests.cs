using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.UnitTests;

public class BulkDeleteReportsCommandHandlerTests
{
    private readonly IReportRepository _reportRepo = Substitute.For<IReportRepository>();
    private readonly IFilingRepository _filingRepo = Substitute.For<IFilingRepository>();
    private readonly BulkDeleteReportsCommandHandler _sut;

    public BulkDeleteReportsCommandHandlerTests()
        => _sut = new BulkDeleteReportsCommandHandler(_reportRepo, _filingRepo);

    [Fact]
    public async Task HandleAsync_NullReportIds_ReturnsDomainError()
    {
        var result = await _sut.HandleAsync(new BulkDeleteReportsCommand(null!));
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_BULK_DELETE_INVALID");
    }

    [Fact]
    public async Task HandleAsync_EmptyReportIds_ReturnsDomainError()
    {
        var result = await _sut.HandleAsync(new BulkDeleteReportsCommand(Array.Empty<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_BULK_DELETE_INVALID");
    }

    [Fact]
    public async Task HandleAsync_ValidIds_CallsDeleteByReportIdForEachThenDeleteMany()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var ids = new[] { id1, id2 };

        var result = await _sut.HandleAsync(new BulkDeleteReportsCommand(ids));

        result.IsSuccess.Should().BeTrue();
        await _filingRepo.Received(1).DeleteByReportIdAsync(id1, Arg.Any<CancellationToken>());
        await _filingRepo.Received(1).DeleteByReportIdAsync(id2, Arg.Any<CancellationToken>());
        await _reportRepo.Received(1).DeleteManyAsync(ids, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DeleteByReportIdCalledBeforeDeleteMany()
    {
        var callOrder = new List<string>();
        var id = Guid.NewGuid();
        _filingRepo.DeleteByReportIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("filings"); return Task.CompletedTask; });
        _reportRepo.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("reports"); return Task.CompletedTask; });

        await _sut.HandleAsync(new BulkDeleteReportsCommand(new[] { id }));

        callOrder.Should().Equal("filings", "reports");
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_ReturnsFailure()
    {
        _filingRepo.DeleteByReportIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("DB error"));

        var result = await _sut.HandleAsync(
            new BulkDeleteReportsCommand(new[] { Guid.NewGuid() }));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_BULK_DELETE_FAILED");
    }

    [Fact]
    public async Task HandleAsync_ForwardsCancellationToken()
    {
        var ids = new[] { Guid.NewGuid() };
        var cts = new CancellationTokenSource();
        await _sut.HandleAsync(new BulkDeleteReportsCommand(ids), cts.Token);
        await _filingRepo.Received(1).DeleteByReportIdAsync(ids[0], cts.Token);
        await _reportRepo.Received(1).DeleteManyAsync(ids, cts.Token);
    }
}
