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
using Xunit;

namespace Rentier.UnitTests.Application.Handlers;

public class ProcessReportsCommandHandlerPartialSuccessTests
{
    private readonly Guid ProfileId = Guid.NewGuid();
    private static readonly DateOnly TestDate = new(2024, 6, 17); // Monday

    private static HolidayConfDto MakeHolidayDto()
        => new HolidayConfDto([], 2024, 2024);

    private Importer MakeImporter()
    {
        var importer = Importer.Create("Test Importer");
        importer.UpdateDetails("Test Importer", ReportType.IbkrCsv, ProfileId, null, "", "", "", "");
        return importer;
    }

    private static Report MakeReportWithContent(Guid importerId)
        => Report.Create(importerId, "statement.csv", [1, 2, 3], null);

    private static ExchangeRateResolver MakeResolver(IExchangeRateFetcher fetcher)
        => new ExchangeRateResolver(fetcher, NullLogger<ExchangeRateResolver>.Instance);

    private ProcessReportsCommandHandler MakeHandler(
        IReportRepository reportRepo,
        IImporterRepository importerRepo,
        IFilingRepository filingRepo,
        ExchangeRateResolver resolver,
        IStatementParser parser)
    {
        var holidayRepo = Substitute.For<IHolidayRepository>();
        holidayRepo.GetHolidayConfAsync(Arg.Any<CancellationToken>()).Returns(MakeHolidayDto());

        return new ProcessReportsCommandHandler(
            reportRepo, importerRepo, filingRepo, resolver, holidayRepo, parser,
            NullLogger<ProcessReportsCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_AllEventsSucceed_ReturnsProcessed()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>()).Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var dividends = new[]
        {
            new DividendRecord(TestDate, "USD", "ACME Corp", 100m),
            new DividendRecord(TestDate, "USD", "BIG Inc", 200m),
        };
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(
                new StatementParseResult(dividends, [], [], [], [])));

        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 108m)));

        var filingRepo = Substitute.For<IFilingRepository>();
        filingRepo.GetByIncomeEventAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>()).Returns(Array.Empty<Filing>());

        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, MakeResolver(fetcher), parser);
        var result = await handler.HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilingsCreated.Should().Be(2);
        result.Value.ReportsProcessed.Should().Be(1);
        result.Value.ReportsPartialError.Should().Be(0);
        result.Value.EventErrors.Should().BeEmpty();
        await reportRepo.Received(1).UpdateAsync(
            Arg.Is<Report>(r => r!.Status == ReportStatus.Processed), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_TwoSucceedOneFails_ReturnsPartialError()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>()).Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var dividends = new[]
        {
            new DividendRecord(TestDate, "USD", "ACME Corp", 100m),
            new DividendRecord(TestDate, "USD", "BIG Inc", 200m),
            new DividendRecord(TestDate, "CHF", "Swiss Co", 50m),
        };
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(
                new StatementParseResult(dividends, [], [], [], [])));

        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 108m)));
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "CHF", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("UNSUPPORTED_CURRENCY", "CHF not supported")));

        var filingRepo = Substitute.For<IFilingRepository>();
        filingRepo.GetByIncomeEventAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>()).Returns(Array.Empty<Filing>());

        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, MakeResolver(fetcher), parser);
        var result = await handler.HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilingsCreated.Should().Be(2);
        result.Value.ReportsPartialError.Should().Be(1);
        result.Value.EventErrors.Should().HaveCount(1);
        result.Value.EventErrors[0].EntityName.Should().Be("Swiss Co");
        result.Value.EventErrors[0].Currency.Should().Be("CHF");
        result.Value.EventErrors[0].ErrorCode.Should().Be("UNSUPPORTED_CURRENCY");
        await reportRepo.Received(1).UpdateAsync(
            Arg.Is<Report>(r => r!.Status == ReportStatus.PartialError), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_AllEventsFail_ReturnsError()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>()).Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var dividends = new[]
        {
            new DividendRecord(TestDate, "CHF", "Swiss Co", 50m),
            new DividendRecord(TestDate, "CHF", "Alps AG", 75m),
        };
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(
                new StatementParseResult(dividends, [], [], [], [])));

        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "CHF", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("UNSUPPORTED_CURRENCY", "CHF not supported")));

        var filingRepo = Substitute.For<IFilingRepository>();
        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, MakeResolver(fetcher), parser);
        var result = await handler.HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilingsCreated.Should().Be(0);
        result.Value.ReportsErrored.Should().Be(1);
        result.Value.EventErrors.Should().HaveCount(2);
        await reportRepo.Received(1).UpdateAsync(
            Arg.Is<Report>(r => r!.Status == ReportStatus.Error), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmptyReport_ReturnsProcessed()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>()).Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(
                new StatementParseResult([], [], [], [], [])));

        var fetcher = Substitute.For<IExchangeRateFetcher>();
        var filingRepo = Substitute.For<IFilingRepository>();
        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, MakeResolver(fetcher), parser);
        var result = await handler.HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsProcessed.Should().Be(1);
        result.Value.FilingsCreated.Should().Be(0);
        result.Value.EventErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MixedBatch_TwoSucceedOneFails_PartialError()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>()).Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var dividends = new[]
        {
            new DividendRecord(TestDate, "USD", "Alpha Corp", 100m),
            new DividendRecord(TestDate, "USD", "Beta Inc", 200m),
            new DividendRecord(TestDate, "XYZ", "Gamma Co", 50m),
        };
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(
                new StatementParseResult(dividends, [], [], [], [])));

        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 108m)));
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "XYZ", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("UNSUPPORTED_CURRENCY", "XYZ not supported")));

        var filingRepo = Substitute.For<IFilingRepository>();
        filingRepo.GetByIncomeEventAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>()).Returns(Array.Empty<Filing>());

        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, MakeResolver(fetcher), parser);
        var result = await handler.HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilingsCreated.Should().Be(2);
        result.Value.ReportsPartialError.Should().Be(1);
        result.Value.EventErrors.Should().HaveCount(1);
        result.Value.EventErrors[0].EntityName.Should().Be("Gamma Co");
    }
}
