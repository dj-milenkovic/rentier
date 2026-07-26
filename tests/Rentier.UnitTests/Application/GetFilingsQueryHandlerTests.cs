using FluentAssertions;
using NSubstitute;
using Rentier.Application.Enums;
using Rentier.Application.Handlers;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests.Application;

public class GetFilingsQueryHandlerTests
{
    private readonly IFilingRepository _repo = Substitute.For<IFilingRepository>();
    private readonly GetFilingsQueryHandler _sut;

    public GetFilingsQueryHandlerTests()
    {
        _sut = new GetFilingsQueryHandler(_repo);
    }

    private static Filing MakeFiling(DateOnly? deadline = null, FilingStatus status = FilingStatus.Init)
    {
        var f = Filing.CreateFromIncome(
            new FilingInfo(IncomeType.Dividend, "ACME Corp", new DateOnly(2024, 3, 1), 1000m, 150m, 150m, 0m),
            Guid.NewGuid(), deadline ?? new DateOnly(2024, 4, 30), new FilingProvenance());
        if (status == FilingStatus.Filed) f.AdvanceStatus(FilingStatus.Filed);
        if (status == FilingStatus.Paid) { f.AdvanceStatus(FilingStatus.Filed); f.AdvanceStatus(FilingStatus.Paid); }
        return f;
    }

    private void SetupPagedReturns(IReadOnlyList<Filing>? items = null, int total = 0)
    {
        _repo.GetPagedAsync(
                Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<FilingSortColumn>(), Arg.Any<bool>(),
                Arg.Any<FilingColumnFilter?>(), Arg.Any<CancellationToken>())
            .Returns((items ?? Array.Empty<Filing>().ToList().AsReadOnly(), total));
    }

    [Fact]
    public async Task HandleAsync_WithUnpaidFilter_PassesUnpaidToRepository()
    {
        SetupPagedReturns();
        await _sut.HandleAsync(new GetFilingsQuery(), TestContext.Current.CancellationToken);
        await _repo.Received(1).GetPagedAsync(
            FilingFilterMode.Unpaid, Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<FilingSortColumn>(), Arg.Any<bool>(),
            Arg.Any<FilingColumnFilter?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAllFilter_PassesAllToRepository()
    {
        SetupPagedReturns();
        await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 1, 30), TestContext.Current.CancellationToken);
        await _repo.Received(1).GetPagedAsync(
            FilingFilterMode.All, Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<FilingSortColumn>(), Arg.Any<bool>(),
            Arg.Any<FilingColumnFilter?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_MapsFilingTaxPayableRsdToTaxPayable()
    {
        var filing = MakeFiling();
        SetupPagedReturns(new List<Filing> { filing }.AsReadOnly(), 1);
        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 1, 30), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.Rows[0].TaxPayable.Should().Be(filing.TaxPayableRsd);
    }

    [Fact]
    public async Task HandleAsync_ComputesCorrectTotalPages()
    {
        SetupPagedReturns(total: 45);
        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 1, 30), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPages.Should().Be(2); // ceil(45/30) = 2
    }

    [Fact]
    public async Task HandleAsync_WhenNoResults_ReturnsTotalPagesOfOne()
    {
        SetupPagedReturns();
        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 1, 30), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_WhenPageLessThan1_ReturnsFailure()
    {
        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 0, 30), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("1");
    }

    [Fact]
    public async Task HandleAsync_WhenPageSizeOutOfRange_ReturnsFailure()
    {
        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 1, 0), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectSkipToRepository()
    {
        SetupPagedReturns();
        await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.All, 3, 30), TestContext.Current.CancellationToken);
        await _repo.Received(1).GetPagedAsync(
            Arg.Any<FilingFilterMode>(), 60, 30,
            Arg.Any<FilingSortColumn>(), Arg.Any<bool>(),
            Arg.Any<FilingColumnFilter?>(), Arg.Any<CancellationToken>());
    }

    // -- Sort parameter tests (feature 027) -----------------------------------

    [Fact]
    public async Task HandleAsync_DefaultQuery_PassesFilingDeadlineSortColumnDescendingToRepository()
    {
        SetupPagedReturns();
        await _sut.HandleAsync(new GetFilingsQuery(), TestContext.Current.CancellationToken);
        await _repo.Received(1).GetPagedAsync(
            Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(),
            FilingSortColumn.FilingDeadline, true,
            Arg.Any<FilingColumnFilter?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ExplicitSortParams_ForwardsThemUnchangedToRepository()
    {
        SetupPagedReturns();
        await _sut.HandleAsync(new GetFilingsQuery(
            SortColumn: FilingSortColumn.TaxPayable,
            SortDescending: false), TestContext.Current.CancellationToken);
        await _repo.Received(1).GetPagedAsync(
            Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(),
            FilingSortColumn.TaxPayable, false,
            Arg.Any<FilingColumnFilter?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidSortColumn_ReturnsValidationFailure()
    {
        var result = await _sut.HandleAsync(new GetFilingsQuery(SortColumn: (FilingSortColumn)999), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PAGINATION_VALIDATION_FAILED");
        result.Error.Message.Should().Contain("sort column");
    }

    // -- ReportIdFilter branch (feature 014) ----------------------------------

    [Fact]
    public async Task HandleAsync_WhenReportIdFilterSet_CallsGetByReportIdAsyncInsteadOfGetPagedAsync()
    {
        var reportId = Guid.NewGuid();
        _repo.GetByReportIdAsync(reportId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Filing>().ToList().AsReadOnly());
        await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.Unpaid, 1, 30, reportId), TestContext.Current.CancellationToken);
        await _repo.Received(1).GetByReportIdAsync(reportId, Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().GetPagedAsync(
            Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<FilingSortColumn>(), Arg.Any<bool>(),
            Arg.Any<FilingColumnFilter?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenReportIdFilterSet_ReturnsAllFilingsAsSinglePage()
    {
        var reportId = Guid.NewGuid();
        _repo.GetByReportIdAsync(reportId, Arg.Any<CancellationToken>())
            .Returns(new List<Filing> { MakeFiling(), MakeFiling() }.AsReadOnly());
        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.Unpaid, 1, 30, reportId), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPages.Should().Be(1);
        result.Value.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_WhenReportIdFilterSet_ReturnsFilingCountAsTotal()
    {
        var reportId = Guid.NewGuid();
        _repo.GetByReportIdAsync(reportId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(0, 5).Select(_ => MakeFiling()).ToList().AsReadOnly());
        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.Unpaid, 1, 30, reportId), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_WhenReportIdFilterSetAndNoFilings_ReturnsEmptyPageResult()
    {
        var reportId = Guid.NewGuid();
        _repo.GetByReportIdAsync(reportId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Filing>().ToList().AsReadOnly());
        var result = await _sut.HandleAsync(new GetFilingsQuery(FilingFilterMode.Unpaid, 1, 30, reportId), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
        result.Value.TotalPages.Should().Be(1);
    }

    // -- ColumnFilter tests (feature 045) -------------------------------------

    [Fact]
    public async Task HandleAsync_WhenColumnFilterSet_PassesColumnFilterToRepository()
    {
        SetupPagedReturns();
        var filter = new FilingColumnFilter(Status: FilingStatus.Init);
        await _sut.HandleAsync(new GetFilingsQuery(ColumnFilter: filter), TestContext.Current.CancellationToken);
        await _repo.Received(1).GetPagedAsync(
            Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<FilingSortColumn>(), Arg.Any<bool>(),
            filter, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenReportIdFilterSet_PassesNullColumnFilterToRepository()
    {
        var reportId = Guid.NewGuid();
        _repo.GetByReportIdAsync(reportId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Filing>().ToList().AsReadOnly());
        var filter = new FilingColumnFilter(Status: FilingStatus.Init);
        await _sut.HandleAsync(new GetFilingsQuery(ReportIdFilter: reportId, ColumnFilter: filter), TestContext.Current.CancellationToken);
        // Should use GetByReportIdAsync, NOT GetPagedAsync
        await _repo.Received(1).GetByReportIdAsync(reportId, Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().GetPagedAsync(
            Arg.Any<FilingFilterMode>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<FilingSortColumn>(), Arg.Any<bool>(),
            Arg.Any<FilingColumnFilter?>(), Arg.Any<CancellationToken>());
    }
}

