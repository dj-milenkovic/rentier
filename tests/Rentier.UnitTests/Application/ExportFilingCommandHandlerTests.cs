using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests.Application;

public class ExportFilingCommandHandlerTests
{
    private readonly IFilingRepository _filings = Substitute.For<IFilingRepository>();
    private readonly ITaxpayerProfileRepository _profiles = Substitute.For<ITaxpayerProfileRepository>();
    private readonly IReportRepository _reports = Substitute.For<IReportRepository>();
    private readonly IImporterRepository _importers = Substitute.For<IImporterRepository>();
    private readonly IXmlFilingSerializer _serializer = Substitute.For<IXmlFilingSerializer>();
    private readonly ExportFilingCommandHandler _sut;

    public ExportFilingCommandHandlerTests()
    {
        _serializer.Serialize(Arg.Any<Filing>(), Arg.Any<TaxpayerProfile>(), Arg.Any<string>())
            .Returns(Result<byte[], Error>.Success("<?"u8.ToArray())); // minimal non-empty byte array

        _sut = new ExportFilingCommandHandler(_filings, _profiles, _reports, _importers, _serializer);
    }

    private static Filing MakeFiling(
        IncomeType incomeType = IncomeType.Dividend,
        Guid? reportId = null,
        string? ticker = null,
        string payingEntity = "ACME Corp",
        string? paymentNotes = null)
    {
        var filing = Filing.CreateFromIncome(
            Guid.NewGuid(), incomeType, payingEntity,
            new DateOnly(2025, 3, 15), 10000m, 1500m, 1500m, 0m,
            new DateOnly(2025, 4, 30),
            reportId,
            ticker: ticker,
            paymentNotes: paymentNotes);
        return filing;
    }

    private static TaxpayerProfile MakeProfile()
        => new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "John Doe", "Beograd 1", "018");

    [Fact]
    public async Task HandleAsync_FilingNotFound_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _filings.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Filing?)null);

        var result = await _sut.HandleAsync(new ExportFilingCommand(id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task HandleAsync_ProfileNotFound_ReturnsFailure()
    {
        var filing = MakeFiling();
        _filings.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);
        _profiles.GetAsync(Arg.Any<CancellationToken>()).Returns((TaxpayerProfile?)null);

        var result = await _sut.HandleAsync(new ExportFilingCommand(filing.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        result.Error.Message.Should().Contain("profile");
    }

    [Fact]
    public async Task HandleAsync_NullReportId_UsesEmptyPaymentNotes()
    {
        var filing = MakeFiling(reportId: null);
        var profile = MakeProfile();
        _filings.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);
        _profiles.GetAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _sut.HandleAsync(new ExportFilingCommand(filing.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _serializer.Received(1).Serialize(filing, profile, string.Empty);
        await _reports.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NullReportIdWithFilingPaymentNotes_UsesFilingPaymentNotes()
    {
        var filing = MakeFiling(reportId: null, paymentNotes: "Isplata na brokerski racun");
        var profile = MakeProfile();
        _filings.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);
        _profiles.GetAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _sut.HandleAsync(new ExportFilingCommand(filing.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _serializer.Received(1).Serialize(filing, profile, "Isplata na brokerski racun");
        await _reports.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ReportBackedFiling_IgnoresFilingPaymentNotesAndUsesImporterPaymentNotes()
    {
        var report = Report.Create(Guid.NewGuid(), "report.csv", null, null);
        var filing = MakeFiling(reportId: report.Id, paymentNotes: "Should not be used");
        var profile = MakeProfile();
        var importer = Importer.Create("Test Importer");
        importer.UpdateDetails(
            "Test Importer", ReportType.IbkrCsv, null, null, "", "", "",
            paymentNotes: "Importer payment notes");
        _filings.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);
        _profiles.GetAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _reports.GetByIdAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);
        _importers.GetByIdAsync(report.ImporterId, Arg.Any<CancellationToken>()).Returns(importer);

        var result = await _sut.HandleAsync(new ExportFilingCommand(filing.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _serializer.Received(1).Serialize(filing, profile, "Importer payment notes");
    }

    // ── T013: New filename convention tests ───────────────────────────────────

    [Fact]
    public async Task HandleAsync_FilingWithTicker_UsesTickerInFilename()
    {
        var filing = MakeFiling(ticker: "BABA");
        var profile = MakeProfile();
        _filings.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);
        _profiles.GetAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _sut.HandleAsync(new ExportFilingCommand(filing.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.SuggestedFileName.Should().Be("2025-03-BABA.xml");
    }

    [Fact]
    public async Task HandleAsync_NullTickerWithPayingEntity_UsesPayingEntityInFilename()
    {
        var filing = MakeFiling(ticker: null, payingEntity: "ACME Corp");
        var profile = MakeProfile();
        _filings.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);
        _profiles.GetAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _sut.HandleAsync(new ExportFilingCommand(filing.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.SuggestedFileName.Should().Be("2025-03-ACME_Corp.xml");
    }

    [Fact]
    public async Task HandleAsync_TickerWithUnsafeChars_SanitizesFilename()
    {
        var filing = MakeFiling(ticker: "BAD:NAME*");
        var profile = MakeProfile();
        _filings.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);
        _profiles.GetAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _sut.HandleAsync(new ExportFilingCommand(filing.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.SuggestedFileName.Should().Be("2025-03-BAD_NAME_.xml");
    }

    [Fact]
    public async Task HandleAsync_DividendFiling_ReturnsDividendResult()
    {
        var filing = MakeFiling(IncomeType.Dividend, ticker: "ACME");
        var profile = MakeProfile();
        _filings.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);
        _profiles.GetAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _sut.HandleAsync(new ExportFilingCommand(filing.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.SuggestedFileName.Should().Be("2025-03-ACME.xml");
        result.Value.Bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_InterestFiling_ReturnsInterestResult()
    {
        var filing = MakeFiling(IncomeType.Interest, ticker: "AAPL");
        var profile = MakeProfile();
        _filings.GetByIdAsync(filing.Id, Arg.Any<CancellationToken>()).Returns(filing);
        _profiles.GetAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _sut.HandleAsync(new ExportFilingCommand(filing.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.SuggestedFileName.Should().Be("2025-03-AAPL.xml");
        result.Value.Bytes.Should().NotBeEmpty();
    }
}

