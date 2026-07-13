using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.UnitTests;

public class FilingPaymentNotesTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly DateOnly TestDate = new(2025, 3, 15);
    private static readonly DateOnly Deadline = new(2025, 4, 30);

    private static Filing Make(string? paymentNotes) =>
        Filing.CreateFromIncome(ProfileId, IncomeType.Dividend, "ACME Corp",
            TestDate, 1000m, 150m, 150m, 0m, Deadline, paymentNotes: paymentNotes);

    [Fact]
    public void CreateFromIncome_WithValidPaymentNotes_StoresTrimmedValue()
    {
        var filing = Make("Isplata na brokerski racun");
        filing.PaymentNotes.Should().Be("Isplata na brokerski racun");
    }

    [Fact]
    public void CreateFromIncome_WithNullPaymentNotes_StoresNull()
    {
        var filing = Make(null);
        filing.PaymentNotes.Should().BeNull();
    }

    [Fact]
    public void CreateFromIncome_WithWhitespaceOnlyPaymentNotes_StoresNull()
    {
        var filing = Make("   ");
        filing.PaymentNotes.Should().BeNull();
    }

    [Fact]
    public void CreateFromIncome_WithEmptyStringPaymentNotes_StoresNull()
    {
        var filing = Make(string.Empty);
        filing.PaymentNotes.Should().BeNull();
    }

    [Fact]
    public void CreateFromIncome_WithLeadingTrailingWhitespacePaymentNotes_TrimmedAndStored()
    {
        var filing = Make("  Isplata na brokerski racun  ");
        filing.PaymentNotes.Should().Be("Isplata na brokerski racun");
    }

    [Fact]
    public void CreateFromIncome_WithPaymentNotesLongerThan4000Chars_ThrowsDomainException()
    {
        var tooLong = new string('A', 4001);
        var act = () => Make(tooLong);
        act.Should().Throw<DomainException>()
            .WithMessage("PaymentNotes must not exceed 4000 characters.");
    }

    [Fact]
    public void CreateFromIncome_WithExactly4000CharPaymentNotes_Succeeds()
    {
        var exactly4000 = new string('A', 4000);
        var filing = Make(exactly4000);
        filing.PaymentNotes.Should().Be(exactly4000);
    }
}
