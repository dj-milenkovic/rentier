using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests.Application;

public class GetReportsQueryHandlerTests
{
    private readonly IReportRepository _reportRepo = Substitute.For<IReportRepository>();
    private readonly IImporterRepository _importerRepo = Substitute.For<IImporterRepository>();
    private readonly IFilingRepository _filingRepo = Substitute.For<IFilingRepository>();
    private readonly GetReportsQueryHandler _sut;

    public GetReportsQueryHandlerTests()
    {
        _sut = new GetReportsQueryHandler(_reportRepo, _importerRepo, _filingRepo);
    }

    private static Report MakeReport(Guid? importerId = null, string name = "rep.csv")
    {
        var id = importerId ?? Guid.NewGuid();
        return Report.Create(id, name, null, null);
    }

    private static Importer MakeImporter(string display = "Test Importer")
        => Importer.Create(display);

    private void SetupPagedReports(IReadOnlyList<Report> reports, int? totalCount = null)
    {
        var tc = totalCount ?? reports.Count;
        _reportRepo.GetPagedAsync(
            Arg.Any<ReportColumnFilter?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Returns((reports, tc));
    }

    private void SetupPagedReports(int count)
    {
        var importer = MakeImporter();
        var reports = Enumerable.Range(0, count).Select(_ => MakeReport(importer.Id)).ToArray();
        SetupPagedReports(reports, count);
        _importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([importer]);
        _filingRepo.GetAggregatesByReportIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (int, DateOnly?)>());
    }

    [Fact]
    public async Task HandleAsync_WithNoReports_ReturnsEmptyList()
    {
        SetupPagedReports(Array.Empty<Report>());
        _importerRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Importer>());

        var result = await _sut.HandleAsync(new GetReportsQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MapsAllDtoFieldsCorrectly()
    {
        var importer = MakeImporter("IBKR EU");
        var report = MakeReport(importer.Id, "stmt_2024.csv");
        var earliest = new DateOnly(2024, 3, 15);
        SetupPagedReports([report]);
        _importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([importer]);
        _filingRepo.GetAggregatesByReportIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (int, DateOnly?)> { [report.Id] = (7, earliest) });

        var result = await _sut.HandleAsync(new GetReportsQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.Rows[0];
        dto.Id.Should().Be(report.Id);
        dto.ReportName.Should().Be("stmt_2024.csv");
        dto.ImporterName.Should().Be("IBKR EU");
        dto.Status.Should().Be(ReportStatus.Init);
        dto.FilingCount.Should().Be(7);
        dto.EarliestIncomeDate.Should().Be(earliest);
        dto.DisplayName.Should().Be("IBKR EU \u2013 2024-03-15");
    }

    [Fact]
    public async Task HandleAsync_DisplayName_FallsBackToImportDateWhenNoFilings()
    {
        var importer = MakeImporter("My Broker");
        var report = MakeReport(importer.Id, "stmt.csv");
        SetupPagedReports([report]);
        _importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([importer]);
        _filingRepo.GetAggregatesByReportIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (int, DateOnly?)>());

        var result = await _sut.HandleAsync(new GetReportsQuery(), TestContext.Current.CancellationToken);

        var dto = result.Value.Rows[0];
        dto.EarliestIncomeDate.Should().BeNull();
        dto.DisplayName.Should().Be($"My Broker \u2013 {report.ImportDate:yyyy-MM-dd}");
    }

    [Fact]
    public async Task HandleAsync_ResolvesImporterNameFromDictionary()
    {
        var importer = MakeImporter("My Broker");
        var report = MakeReport(importer.Id);
        SetupPagedReports([report]);
        _importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([importer]);
        _filingRepo.GetAggregatesByReportIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (int, DateOnly?)>());

        var result = await _sut.HandleAsync(new GetReportsQuery(), TestContext.Current.CancellationToken);

        result.Value.Rows[0].ImporterName.Should().Be("My Broker");
    }

    [Fact]
    public async Task HandleAsync_WhenImporterNotFound_UsesUnknownFallback()
    {
        var report = MakeReport(Guid.NewGuid());
        SetupPagedReports([report]);
        _importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Importer>());
        _filingRepo.GetAggregatesByReportIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (int, DateOnly?)>());

        var result = await _sut.HandleAsync(new GetReportsQuery(), TestContext.Current.CancellationToken);

        result.Value.Rows[0].ImporterName.Should().Be("Unknown");
    }

    [Fact]
    public async Task HandleAsync_ReturnsCorrectFilingCountPerReport()
    {
        var importer = MakeImporter();
        var r1 = MakeReport(importer.Id, "r1.csv");
        var r2 = MakeReport(importer.Id, "r2.csv");
        SetupPagedReports([r1, r2]);
        _importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([importer]);
        _filingRepo.GetAggregatesByReportIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (int, DateOnly?)> { [r1.Id] = (5, null), [r2.Id] = (2, null) });

        var result = await _sut.HandleAsync(new GetReportsQuery(), TestContext.Current.CancellationToken);

        result.Value.Rows.Should().HaveCount(2);
        result.Value.Rows.First(d => d.Id == r1.Id).FilingCount.Should().Be(5);
        result.Value.Rows.First(d => d.Id == r2.Id).FilingCount.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryThrows_ReturnsFailure()
    {
        _reportRepo.GetPagedAsync(
            Arg.Any<ReportColumnFilter?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("DB failure"));
        _importerRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Importer>());

        var result = await _sut.HandleAsync(new GetReportsQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_QUERY_FAILED");
        result.Error.Message.Should().Contain("DB failure");
    }

    [Fact]
    public async Task HandleAsync_DefaultQuery_CallsGetPagedAsyncWithSortDescendingTrue()
    {
        SetupPagedReports(0);

        await _sut.HandleAsync(new GetReportsQuery(), TestContext.Current.CancellationToken);

        await _reportRepo.Received(1).GetPagedAsync(
            Arg.Any<ReportColumnFilter?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SortDescendingFalse_ForwardedToRepository()
    {
        SetupPagedReports(0);

        await _sut.HandleAsync(new GetReportsQuery(SortDescending: false), TestContext.Current.CancellationToken);

        await _reportRepo.Received(1).GetPagedAsync(
            Arg.Any<ReportColumnFilter?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Page1PageSize30_PassesSkip0Take30ToRepository()
    {
        SetupPagedReports(0);

        await _sut.HandleAsync(new GetReportsQuery(Page: 1, PageSize: 30), TestContext.Current.CancellationToken);

        await _reportRepo.Received(1).GetPagedAsync(
            Arg.Any<ReportColumnFilter?>(),
            0,
            30,
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Page2PageSize30_PassesSkip30Take30ToRepository()
    {
        SetupPagedReports(0);

        await _sut.HandleAsync(new GetReportsQuery(Page: 2, PageSize: 30), TestContext.Current.CancellationToken);

        await _reportRepo.Received(1).GetPagedAsync(
            Arg.Any<ReportColumnFilter?>(),
            30,
            30,
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_TotalCountFrom_Repository_DrivesTotalPages()
    {
        var importer = MakeImporter();
        var pageReports = Enumerable.Range(0, 30).Select(_ => MakeReport(importer.Id)).ToArray();
        _reportRepo.GetPagedAsync(
            Arg.Any<ReportColumnFilter?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Report>)pageReports, 75));
        _importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([importer]);
        _filingRepo.GetAggregatesByReportIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (int, DateOnly?)>());

        var result = await _sut.HandleAsync(new GetReportsQuery(Page: 1, PageSize: 30), TestContext.Current.CancellationToken);

        result.Value.TotalCount.Should().Be(75);
        result.Value.TotalPages.Should().Be(3);
        result.Value.Rows.Should().HaveCount(30);
    }

    [Fact]
    public async Task HandleAsync_EmptyCollection_ReturnsTotalPagesOne()
    {
        SetupPagedReports(0);

        var result = await _sut.HandleAsync(new GetReportsQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
        result.Value.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_PageZero_ReturnsValidationFailure()
    {
        var result = await _sut.HandleAsync(new GetReportsQuery(Page: 0), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PAGINATION_VALIDATION_FAILED");
        result.Error.Message.Should().Contain("1");
    }

    [Fact]
    public async Task HandleAsync_PageSizeZero_ReturnsValidationFailure()
    {
        var result = await _sut.HandleAsync(new GetReportsQuery(PageSize: 0), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PAGINATION_VALIDATION_FAILED");
    }

    [Fact]
    public async Task HandleAsync_PageSize101_ReturnsValidationFailure()
    {
        var result = await _sut.HandleAsync(new GetReportsQuery(PageSize: 101), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PAGINATION_VALIDATION_FAILED");
    }

    [Fact]
    public async Task HandleAsync_ImporterContains_PreResolvesImporterIds()
    {
        var importer = MakeImporter("IBKR Europe");
        var report = MakeReport(importer.Id);
        SetupPagedReports([report]);
        _importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([importer]);
        _filingRepo.GetAggregatesByReportIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (int, DateOnly?)>());

        var filter = new ReportColumnFilter(ImporterContains: "IBKR");
        var result = await _sut.HandleAsync(new GetReportsQuery(Filter: filter), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await _reportRepo.Received(1).GetPagedAsync(
            Arg.Is<ReportColumnFilter?>(f => f != null && f.ImporterIds != null && f.ImporterIds.Contains(importer.Id)),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_FilingCountFilter_PostFiltersPageResults()
    {
        var importer = MakeImporter();
        var r1 = MakeReport(importer.Id, "r1.csv");
        var r2 = MakeReport(importer.Id, "r2.csv");
        SetupPagedReports([r1, r2]);
        _importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns([importer]);
        _filingRepo.GetAggregatesByReportIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (int, DateOnly?)> { [r1.Id] = (3, null), [r2.Id] = (7, null) });

        var filter = new ReportColumnFilter(FilingCountValue: 7);
        var result = await _sut.HandleAsync(new GetReportsQuery(Filter: filter), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().HaveCount(1);
        result.Value.Rows[0].Id.Should().Be(r2.Id);
    }

    [Fact]
    public async Task HandleAsync_NullFilter_PassesNullFilterToRepository()
    {
        SetupPagedReports(0);

        await _sut.HandleAsync(new GetReportsQuery(), TestContext.Current.CancellationToken);

        await _reportRepo.Received(1).GetPagedAsync(
            null,
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }
}
