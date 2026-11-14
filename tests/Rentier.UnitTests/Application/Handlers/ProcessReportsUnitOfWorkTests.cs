using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Application.Repositories;
using Rentier.Application.Services;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.UnitTests.Application.Handlers;

/// <summary>
/// Transaction-boundary behaviour of <see cref="ProcessReportsCommandHandler"/>: a report's
/// filings and its status change are staged in one unit of work and committed exactly once.
/// </summary>
public class ProcessReportsUnitOfWorkTests
{
    private static readonly DateOnly TEST_DATE = new(2024, 6, 17);
    private const decimal USD_RATE = 117m;

    private readonly Guid _profileId = Guid.NewGuid();
    private readonly IReportRepository _reportRepo = Substitute.For<IReportRepository>();
    private readonly IFilingRepository _filingRepo = Substitute.For<IFilingRepository>();
    private readonly IImporterRepository _importerRepo = Substitute.For<IImporterRepository>();
    private readonly IExchangeRateFetcher _rateFetcher = Substitute.For<IExchangeRateFetcher>();
    private readonly IStatementParser _parser = Substitute.For<IStatementParser>();
    private readonly FakeUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ProcessReportsCommandHandler _sut;

    public ProcessReportsUnitOfWorkTests()
    {
        _unitOfWorkFactory = new FakeUnitOfWorkFactory(_reportRepo, _filingRepo);

        _filingRepo.GetByIncomeEventAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Filing>());
        _rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TEST_DATE, "USD", USD_RATE)));

        var holidayRepo = Substitute.For<IHolidayRepository>();
        holidayRepo.GetHolidayConfAsync(Arg.Any<CancellationToken>())
            .Returns(new HolidayConfDto([], 2024, 2024));

        _sut = new ProcessReportsCommandHandler(
            _reportRepo,
            _importerRepo,
            _unitOfWorkFactory,
            new ExchangeRateResolver(_rateFetcher, NullLogger<ExchangeRateResolver>.Instance),
            holidayRepo,
            _parser,
            NullLogger<ProcessReportsCommandHandler>.Instance);
    }

    /// <summary>Arranges <paramref name="reportCount"/> Init reports that all parse to <paramref name="dividends"/>.</summary>
    private IReadOnlyList<Report> ArrangeReports(int reportCount, params DividendRecord[] dividends)
    {
        var importer = Importer.Create("Test Importer");
        importer.UpdateDetails(new ImporterDetails("Test Importer", ReportType.IbkrCsv, _profileId, null, "", "", "", ""));
        _importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var reports = Enumerable.Range(1, reportCount)
            .Select(i => Report.Create(importer.Id, $"statement{i}.csv", [1, 2, 3], null))
            .ToList();
        _reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>()).Returns(reports);

        _parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(
                new StatementParseResult(dividends, [], [], [], [])));
        return reports;
    }

    private static DividendRecord Dividend(string entity, decimal amount = 100m)
        => new(TEST_DATE, "USD", entity, amount);

    private Task<Result<ProcessReportsResult, Error>> ActAsync()
        => _sut.HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task HandleAsync_ReportWithSeveralFilings_CommitsExactlyOnce()
    {
        ArrangeReports(1, Dividend("ACME Corp"), Dividend("Globex"));

        await ActAsync();

        _unitOfWorkFactory.CommittedCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_SeveralReports_UsesOneUnitOfWorkPerReport()
    {
        ArrangeReports(3, Dividend("ACME Corp"));

        await ActAsync();

        _unitOfWorkFactory.CommittedCount.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_SeveralReports_DisposesEveryUnitOfWork()
    {
        ArrangeReports(3, Dividend("ACME Corp"));

        await ActAsync();

        _unitOfWorkFactory.DisposedCount.Should().Be(_unitOfWorkFactory.CreatedCount);
    }

    [Fact]
    public async Task HandleAsync_CommitFails_ReportsNoFilingsCreated()
    {
        ArrangeReports(1, Dividend("ACME Corp"), Dividend("Globex"));
        _unitOfWorkFactory.SaveFailure = ordinal => ordinal == 1 ? new InvalidOperationException("disk full") : null;

        var result = await ActAsync();

        result.Value.FilingsCreated.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_CommitFails_CountsReportAsErroredOnly()
    {
        ArrangeReports(1, Dividend("ACME Corp"));
        _unitOfWorkFactory.SaveFailure = ordinal => ordinal == 1 ? new InvalidOperationException("disk full") : null;

        var result = await ActAsync();

        (result.Value.ReportsProcessed, result.Value.ReportsErrored).Should().Be((0, 1));
    }

    [Fact]
    public async Task HandleAsync_CommitFails_MarksPersistedReportErroredInSeparateUnitOfWork()
    {
        var report = ArrangeReports(1, Dividend("ACME Corp"))[0];
        var persisted = Report.Create(report.ImporterId, report.ReportName, [1, 2, 3], null);
        _reportRepo.GetByIdAsync(report.Id, Arg.Any<CancellationToken>()).Returns(persisted);
        _unitOfWorkFactory.SaveFailure = ordinal => ordinal == 1 ? new InvalidOperationException("disk full") : null;

        await ActAsync();

        await _reportRepo.Received(1).UpdateAsync(
            Arg.Is<Report>(r => ReferenceEquals(r, persisted) && r.Status == ReportStatus.Error),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CommitFailsForFirstReport_StillCommitsSecondReport()
    {
        ArrangeReports(2, Dividend("ACME Corp"));
        _unitOfWorkFactory.SaveFailure = ordinal => ordinal == 1 ? new InvalidOperationException("disk full") : null;

        var result = await ActAsync();

        result.Value.ReportsProcessed.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_CancelledMidReport_CommitsNothing()
    {
        ArrangeReports(1, Dividend("ACME Corp"), Dividend("Globex"));
        var calls = 0;
        _rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1
                ? Result<ExchangeRate, Error>.Success(new ExchangeRate(TEST_DATE, "USD", USD_RATE))
                : throw new OperationCanceledException());

        var act = ActAsync;

        await act.Should().ThrowAsync<OperationCanceledException>();
        _unitOfWorkFactory.CommittedCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_SameIncomeEventTwiceInOneReport_StagesSingleFiling()
    {
        // The first filing is only staged, so the repository query cannot see it yet;
        // the handler must still treat the repeat as an idempotent skip.
        ArrangeReports(1, Dividend("ACME Corp"), Dividend("ACME Corp"));

        await ActAsync();

        await _filingRepo.Received(1).AddAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CorrectedIncomeEventInSameReport_FlagsCorrection()
    {
        ArrangeReports(1, Dividend("ACME Corp", 100m), Dividend("ACME Corp", 120m));

        var result = await ActAsync();

        result.Value.EventErrors.Should().ContainSingle(e => e.ErrorCode == ErrorCodes.FILING_CORRECTION_DETECTED);
    }
}
