using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class FilingTickerTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly DateOnly TestDate = new(2025, 3, 15);
    private static readonly DateOnly Deadline = new(2025, 4, 30);

    private static Filing Make(string? ticker) =>
        Filing.CreateFromIncome(
            new FilingInfo(IncomeType.Dividend, "ACME Corp", TestDate, 1000m, 150m, 150m, 0m),
            ProfileId, Deadline, new FilingProvenance(Ticker: ticker));

    [Fact]
    public void CreateFromIncome_WithValidTicker_StoresTrimmedValue()
    {
        var filing = Make("BABA");
        filing.Ticker.Should().Be("BABA");
    }

    [Fact]
    public void CreateFromIncome_WithNullTicker_StoresNull()
    {
        var filing = Make(null);
        filing.Ticker.Should().BeNull();
    }

    [Fact]
    public void CreateFromIncome_WithWhitespaceOnlyTicker_StoresNull()
    {
        var filing = Make("   ");
        filing.Ticker.Should().BeNull();
    }

    [Fact]
    public void CreateFromIncome_WithTickerLongerThan20Chars_ThrowsDomainException()
    {
        var tooLong = new string('A', 21);
        var act = () => Make(tooLong);
        act.Should().Throw<DomainException>()
            .WithMessage("Ticker must not exceed 20 characters.");
    }

    [Fact]
    public void CreateFromIncome_WithExactly20CharTicker_Succeeds()
    {
        var exactly20 = new string('X', 20);
        var filing = Make(exactly20);
        filing.Ticker.Should().Be(exactly20);
    }

    [Fact]
    public void CreateFromIncome_WithLeadingTrailingWhitespaceTicker_TrimmedAndStored()
    {
        var filing = Make("  AAPL  ");
        filing.Ticker.Should().Be("AAPL");
    }

    [Fact]
    public void CreateFromIncome_WithEmptyStringTicker_StoresNull()
    {
        var filing = Make(string.Empty);
        filing.Ticker.Should().BeNull();
    }
}
