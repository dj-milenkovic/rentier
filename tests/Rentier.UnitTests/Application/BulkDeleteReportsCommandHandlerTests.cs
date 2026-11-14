using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.Commands;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.UnitTests.Application;

public class BulkDeleteReportsCommandHandlerTests
{
    private readonly IReportRepository _reportRepo = Substitute.For<IReportRepository>();
    private readonly IFilingRepository _filingRepo = Substitute.For<IFilingRepository>();
    private readonly FakeUnitOfWorkFactory _unitOfWorkFactory;
    private readonly BulkDeleteReportsCommandHandler _sut;

    public BulkDeleteReportsCommandHandlerTests()
    {
        _unitOfWorkFactory = new FakeUnitOfWorkFactory(_reportRepo, _filingRepo);
        _sut = new BulkDeleteReportsCommandHandler(_unitOfWorkFactory);
    }

    [Fact]
    public async Task HandleAsync_NullReportIds_ReturnsDomainError()
    {
        var result = await _sut.HandleAsync(new BulkDeleteReportsCommand(null!), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_BULK_DELETE_INVALID");
    }

    [Fact]
    public async Task HandleAsync_EmptyReportIds_ReturnsDomainError()
    {
        var result = await _sut.HandleAsync(new BulkDeleteReportsCommand(Array.Empty<Guid>()), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_BULK_DELETE_INVALID");
    }

    [Fact]
    public async Task HandleAsync_ValidIds_CallsDeleteByReportIdForEachThenDeleteMany()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var ids = new[] { id1, id2 };

        var result = await _sut.HandleAsync(new BulkDeleteReportsCommand(ids), TestContext.Current.CancellationToken);

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

        await _sut.HandleAsync(new BulkDeleteReportsCommand(new[] { id }), TestContext.Current.CancellationToken);

        callOrder.Should().Equal("filings", "reports");
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_ReturnsFailure()
    {
        _filingRepo.DeleteByReportIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("DB error"));

        var result = await _sut.HandleAsync(new BulkDeleteReportsCommand(new[] { Guid.NewGuid() }), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_BULK_DELETE_FAILED");
    }

    [Fact]
    public async Task HandleAsync_MultipleReports_CommitsExactlyOnce()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        await _sut.HandleAsync(new BulkDeleteReportsCommand(ids), TestContext.Current.CancellationToken);

        _unitOfWorkFactory.CommittedCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_WhenDeleteManyThrows_DoesNotCommitStagedFilingDeletes()
    {
        _reportRepo.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("DB error"));

        await _sut.HandleAsync(new BulkDeleteReportsCommand(new[] { Guid.NewGuid() }), TestContext.Current.CancellationToken);

        _unitOfWorkFactory.CommittedCount.Should().Be(0);
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
