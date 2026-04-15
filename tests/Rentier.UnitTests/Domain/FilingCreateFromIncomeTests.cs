using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.UnitTests;

public class FilingCreateFromIncomeTests
{
    private readonly Guid ProfileId = Guid.NewGuid();
    private static readonly DateOnly TestDate = new(2024, 6, 15);
    private static readonly DateOnly Deadline = new(2024, 7, 15);

    [Fact]
    public void CreateFromIncome_ValidArgs_ReturnsFilingWithInitStatus()
    {
        var filing = Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, "ACME Corp", TestDate,
            1000m, 150m, 150m, 0m, Deadline);

        filing.Status.Should().Be(FilingStatus.Init);
        filing.TaxpayerProfileId.Should().Be(ProfileId);
        filing.IncomeType.Should().Be(IncomeType.Dividend);
        filing.PayingEntity.Should().Be("ACME Corp");
    }

    [Fact]
    public void CreateFromIncome_TrimsPayingEntity()
    {
        var filing = Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, "  ACME Corp  ", TestDate,
            1000m, 0m, 150m, 150m, Deadline);

        filing.PayingEntity.Should().Be("ACME Corp");
    }

    [Fact]
    public void CreateFromIncome_EmptyPayingEntity_ThrowsDomainException()
    {
        var act = () => Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, "  ", TestDate,
            1000m, 0m, 150m, 150m, Deadline);

        act.Should().Throw<DomainException>().WithMessage("*PayingEntity must not be empty*");
    }

    [Fact]
    public void CreateFromIncome_NullPayingEntity_ThrowsDomainException()
    {
        var act = () => Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, null!, TestDate,
            1000m, 0m, 150m, 150m, Deadline);

        act.Should().Throw<DomainException>().WithMessage("*PayingEntity must not be empty*");
    }

    [Fact]
    public void CreateFromIncome_NegativeGrossIncome_ThrowsDomainException()
    {
        var act = () => Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, "ACME", TestDate,
            -1m, 0m, 0m, 0m, Deadline);

        act.Should().Throw<DomainException>().WithMessage("*GrossIncomeRsd must not be negative*");
    }

    [Fact]
    public void CreateFromIncome_NegativeWht_ThrowsDomainException()
    {
        var act = () => Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, "ACME", TestDate,
            1000m, -1m, 0m, 0m, Deadline);

        act.Should().Throw<DomainException>().WithMessage("*WhtPaidRsd must not be negative*");
    }

    [Fact]
    public void CreateFromIncome_NegativeGrossTax_ThrowsDomainException()
    {
        var act = () => Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, "ACME", TestDate,
            1000m, 0m, -1m, 0m, Deadline);

        act.Should().Throw<DomainException>().WithMessage("*GrossTaxPayableRsd must not be negative*");
    }

    [Fact]
    public void CreateFromIncome_NegativeTaxPayable_ThrowsDomainException()
    {
        var act = () => Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, "ACME", TestDate,
            1000m, 0m, 150m, -1m, Deadline);

        act.Should().Throw<DomainException>().WithMessage("*TaxPayableRsd must not be negative*");
    }

    [Fact]
    public void CreateFromIncome_SetsIncomeDateAndTaxPeriod()
    {
        var filing = Filing.CreateFromIncome(ProfileId, IncomeType.Interest, "IB", TestDate,
            500m, 0m, 75m, 75m, Deadline);

        filing.IncomeDate.Should().Be(TestDate);
        filing.TaxPeriod.Should().Be(TestDate);
    }

    [Fact]
    public void CreateFromIncome_WithReportId_SetsReportId()
    {
        var reportId = Guid.NewGuid();

        var filing = Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, "ACME", TestDate,
            1000m, 0m, 150m, 150m, Deadline, reportId);

        filing.ReportId.Should().Be(reportId);
    }

    [Fact]
    public void CreateFromIncome_WithoutReportId_ReportIdIsNull()
    {
        var filing = Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, "ACME", TestDate,
            1000m, 0m, 150m, 150m, Deadline);

        filing.ReportId.Should().BeNull();
    }

    [Fact]
    public void CreateFromIncome_SetsFilingDeadline()
    {
        var filing = Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, "ACME", TestDate,
            1000m, 0m, 150m, 150m, Deadline);

        filing.FilingDeadline.Should().Be(Deadline);
    }

    [Fact]
    public void CreateFromIncome_WithoutProvenance_BothProvenancePropertiesNull()
    {
        var filing = Filing.CreateFromIncome(
            Guid.NewGuid(), IncomeType.Dividend, "Test Co", new DateOnly(2024, 1, 15),
            100m, 0m, 15m, 15m, new DateOnly(2024, 2, 15), null);

        filing.ExchangeRateSourceDate.Should().BeNull();
        filing.ExchangeRateSourceType.Should().BeNull();
    }

    [Fact]
    public void CreateFromIncome_WithExactProvenance_SetsBothFields()
    {
        var incomeDate = new DateOnly(2024, 1, 15);
        var filing = Filing.CreateFromIncome(
            Guid.NewGuid(), IncomeType.Dividend, "Test Co", incomeDate,
            100m, 0m, 15m, 15m, new DateOnly(2024, 2, 15), null,
            incomeDate, ExchangeRateSourceType.Exact);

        filing.ExchangeRateSourceDate.Should().Be(incomeDate);
        filing.ExchangeRateSourceType.Should().Be(ExchangeRateSourceType.Exact);
    }

    [Fact]
    public void CreateFromIncome_WithFallbackProvenance_SetsFallbackSourceDate()
    {
        var incomeDate = new DateOnly(2024, 1, 13); // Saturday
        var fallbackDate = new DateOnly(2024, 1, 12); // Friday
        var filing = Filing.CreateFromIncome(
            Guid.NewGuid(), IncomeType.Dividend, "Test Co", incomeDate,
            100m, 0m, 15m, 15m, new DateOnly(2024, 2, 15), null,
            fallbackDate, ExchangeRateSourceType.Fallback);

        filing.ExchangeRateSourceDate.Should().Be(fallbackDate);
        filing.ExchangeRateSourceType.Should().Be(ExchangeRateSourceType.Fallback);
    }
}