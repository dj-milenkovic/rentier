using FluentAssertions;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class FilingInfoTests
{
    private static readonly DateOnly TestDate = new(2024, 6, 17);

    private static FilingInfo MakeValid(
        IncomeType incomeType = IncomeType.Dividend,
        string payingEntity = "ACME Corp",
        decimal grossIncomeRsd = 10000m,
        decimal whtPaidRsd = 1500m,
        decimal grossTaxPayableRsd = 1000m,
        decimal taxPayableRsd = 850m)
        => new(incomeType, payingEntity, TestDate, grossIncomeRsd, whtPaidRsd, grossTaxPayableRsd, taxPayableRsd);

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArguments_SetsAllProperties()
    {
        var info = MakeValid();

        info.IncomeType.Should().Be(IncomeType.Dividend);
        info.PayingEntity.Should().Be("ACME Corp");
        info.IncomeDate.Should().Be(TestDate);
        info.GrossIncomeRsd.Should().Be(10000m);
        info.WhtPaidRsd.Should().Be(1500m);
        info.GrossTaxPayableRsd.Should().Be(1000m);
        info.TaxPayableRsd.Should().Be(850m);
    }

    [Fact]
    public void Constructor_InterestIncomeType_SetsIncomeType()
    {
        var info = MakeValid(incomeType: IncomeType.Interest);

        info.IncomeType.Should().Be(IncomeType.Interest);
    }

    [Fact]
    public void Constructor_AllZeroAmounts_DoesNotThrow()
    {
        var act = () => new FilingInfo(IncomeType.Dividend, "Entity", TestDate, 0m, 0m, 0m, 0m);

        act.Should().NotThrow();
    }

    // ── PayingEntity validation ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespacePayingEntity_ThrowsDomainException(string? entity)
    {
        var act = () => new FilingInfo(IncomeType.Dividend, entity!, TestDate, 100m, 0m, 15m, 15m);

        act.Should().Throw<DomainException>().WithMessage("*PayingEntity*");
    }

    // ── Amount validations ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NegativeGrossIncomeRsd_ThrowsDomainException()
    {
        var act = () => new FilingInfo(IncomeType.Dividend, "Entity", TestDate, -0.01m, 0m, 0m, 0m);

        act.Should().Throw<DomainException>().WithMessage("*GrossIncomeRsd*");
    }

    [Fact]
    public void Constructor_NegativeWhtPaidRsd_ThrowsDomainException()
    {
        var act = () => new FilingInfo(IncomeType.Dividend, "Entity", TestDate, 100m, -0.01m, 15m, 15m);

        act.Should().Throw<DomainException>().WithMessage("*WhtPaidRsd*");
    }

    [Fact]
    public void Constructor_NegativeGrossTaxPayableRsd_ThrowsDomainException()
    {
        var act = () => new FilingInfo(IncomeType.Dividend, "Entity", TestDate, 100m, 0m, -0.01m, 0m);

        act.Should().Throw<DomainException>().WithMessage("*GrossTaxPayableRsd*");
    }

    [Fact]
    public void Constructor_NegativeTaxPayableRsd_ThrowsDomainException()
    {
        var act = () => new FilingInfo(IncomeType.Dividend, "Entity", TestDate, 100m, 0m, 15m, -0.01m);

        act.Should().Throw<DomainException>().WithMessage("*TaxPayableRsd*");
    }

    // ── Boundary: exactly zero is allowed ────────────────────────────────────

    [Fact]
    public void Constructor_ZeroGrossIncomeRsd_DoesNotThrow()
    {
        var act = () => new FilingInfo(IncomeType.Dividend, "Entity", TestDate, 0m, 0m, 0m, 0m);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ZeroWhtPaidRsd_DoesNotThrow()
    {
        var act = () => new FilingInfo(IncomeType.Dividend, "Entity", TestDate, 100m, 0m, 15m, 15m);

        act.Should().NotThrow();
    }

    // ── Record equality ───────────────────────────────────────────────────────

    [Fact]
    public void TwoInstancesWithSameValues_AreEqual()
    {
        var a = MakeValid();
        var b = MakeValid();

        a.Should().Be(b);
    }

    [Fact]
    public void TwoInstancesWithDifferentPayingEntity_AreNotEqual()
    {
        var a = MakeValid(payingEntity: "ACME Corp");
        var b = MakeValid(payingEntity: "Different Corp");

        a.Should().NotBe(b);
    }
}
