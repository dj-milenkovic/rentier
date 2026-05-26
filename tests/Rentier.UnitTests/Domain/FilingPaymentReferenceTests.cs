using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.UnitTests;

public class FilingPaymentReferenceTests
{
    private static Filing MakeFiling() =>
        Filing.CreateFromIncome(
            taxpayerProfileId: Guid.NewGuid(),
            incomeType: IncomeType.Dividend,
            payingEntity: "Test Corp",
            incomeDate: new DateOnly(2024, 1, 15),
            grossIncomeRsd: 1000m,
            whtPaidRsd: 150m,
            grossTaxPayableRsd: 150m,
            taxPayableRsd: 0m,
            filingDeadline: new DateOnly(2024, 2, 14));

    [Fact]
    public void SetPaymentReference_WithNull_StoresNull()
    {
        var filing = MakeFiling();
        filing.SetPaymentReference(null);
        filing.PaymentReference.Should().BeNull();
    }

    [Fact]
    public void SetPaymentReference_WithEmptyString_StoresNull()
    {
        var filing = MakeFiling();
        filing.SetPaymentReference(string.Empty);
        filing.PaymentReference.Should().BeNull();
    }

    [Fact]
    public void SetPaymentReference_WithWhitespaceOnly_StoresNull()
    {
        var filing = MakeFiling();
        filing.SetPaymentReference("   ");
        filing.PaymentReference.Should().BeNull();
    }

    [Fact]
    public void SetPaymentReference_WithValidString_TrimsAndStores()
    {
        var filing = MakeFiling();
        filing.SetPaymentReference("  REF-001  ");
        filing.PaymentReference.Should().Be("REF-001");
    }

    [Fact]
    public void SetPaymentReference_WithExactly200Characters_Succeeds()
    {
        var filing = MakeFiling();
        var value = new string('x', 200);
        filing.SetPaymentReference(value);
        filing.PaymentReference.Should().Be(value);
    }

    [Fact]
    public void SetPaymentReference_WithOver200Characters_ThrowsDomainException()
    {
        var filing = MakeFiling();
        var tooLong = new string('x', 201);

        var act = () => filing.SetPaymentReference(tooLong);
        act.Should().Throw<DomainException>()
            .WithMessage("*200*");
    }

    [Fact]
    public void SetPaymentReference_CalledTwice_SecondCallOverwrites()
    {
        var filing = MakeFiling();
        filing.SetPaymentReference("FIRST");
        filing.SetPaymentReference("SECOND");
        filing.PaymentReference.Should().Be("SECOND");
    }
}
