using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.UnitTests.Application;

public class DeleteReportCommandHandlerTests
{
    private readonly IReportRepository _reportRepo = Substitute.For<IReportRepository>();
    private readonly IFilingRepository _filingRepo = Substitute.For<IFilingRepository>();
    private readonly FakeUnitOfWorkFactory _unitOfWorkFactory;
    private readonly DeleteReportCommandHandler _sut;

    public DeleteReportCommandHandlerTests()
    {
        _unitOfWorkFactory = new FakeUnitOfWorkFactory(_reportRepo, _filingRepo);
        _sut = new DeleteReportCommandHandler(_unitOfWorkFactory, NullLogger<DeleteReportCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenReportHasFilings_DeletesFilingsThenReport()
    {
        var reportId = Guid.NewGuid();
        var cmd = new DeleteReportCommand(reportId);

        var result = await _sut.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(VoidResult.Value);
        await _filingRepo.Received(1).DeleteByReportIdAsync(reportId, Arg.Any<CancellationToken>());
        await _reportRepo.Received(1).DeleteAsync(reportId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenReportHasNoFilings_DeletesReportWithoutError()
    {
        // DeleteByReportIdAsync is idempotent — returns immediately when no filings
        var cmd = new DeleteReportCommand(Guid.NewGuid());

        var result = await _sut.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_DeletesFilingsBeforeReport()
    {
        // Verify call order: filings first, report second
        var callOrder = new List<string>();
        _filingRepo.DeleteByReportIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("filings"); return Task.CompletedTask; });
        _reportRepo.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("report"); return Task.CompletedTask; });

        await _sut.HandleAsync(new DeleteReportCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        callOrder.Should().Equal("filings", "report");
    }

    [Fact]
    public async Task HandleAsync_WhenDeleteByReportIdThrows_ReturnsFailureAndDoesNotCallDeleteReport()
    {
        _filingRepo.DeleteByReportIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("FK constraint"));

        var result = await _sut.HandleAsync(new DeleteReportCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_DELETE_FAILED");
        await _reportRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenDeleteReportThrows_ReturnsFailure()
    {
        _reportRepo.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Report not deletable"));

        var result = await _sut.HandleAsync(new DeleteReportCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_DELETE_FAILED");
        result.Error.Message.Should().Contain("Report not deletable");
    }

    [Fact]
    public async Task HandleAsync_WhenBothDeletesStaged_CommitsExactlyOnce()
    {
        await _sut.HandleAsync(new DeleteReportCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        _unitOfWorkFactory.CommittedCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_WhenDeleteReportThrows_DoesNotCommitStagedFilingDeletes()
    {
        _reportRepo.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Report not deletable"));

        await _sut.HandleAsync(new DeleteReportCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        _unitOfWorkFactory.CommittedCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_WhenCommitFails_ReturnsFailure()
    {
        _unitOfWorkFactory.SaveFailure = _ => new InvalidOperationException("disk full");

        var result = await _sut.HandleAsync(new DeleteReportCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Code.Should().Be("REPORT_DELETE_FAILED");
    }

    [Fact]
    public async Task HandleAsync_Always_DisposesUnitOfWork()
    {
        await _sut.HandleAsync(new DeleteReportCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        _unitOfWorkFactory.DisposedCount.Should().Be(1);
    }
}
