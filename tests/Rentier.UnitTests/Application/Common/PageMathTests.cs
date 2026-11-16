using FluentAssertions;
using Rentier.Application.Common;
using Xunit;

namespace Rentier.UnitTests.Application.Common;

public class PageMathTests
{
    [Theory]
    // zero / negative clamp to a single page
    [InlineData(0, 30, 1)]
    [InlineData(-5, 30, 1)]
    // below one page
    [InlineData(1, 30, 1)]
    [InlineData(29, 30, 1)]
    // exact multiples
    [InlineData(30, 30, 1)]
    [InlineData(60, 30, 2)]
    [InlineData(300, 100, 3)]
    // remainders
    [InlineData(31, 30, 2)]
    [InlineData(45, 30, 2)]
    [InlineData(75, 30, 3)]
    [InlineData(61, 30, 3)]
    // page size of one
    [InlineData(7, 1, 7)]
    // extremes: no overflow, and beyond the range where double is exact per item
    [InlineData(int.MaxValue, 1, int.MaxValue)]
    [InlineData(int.MaxValue, 100, 21474837)]
    [InlineData(int.MaxValue, int.MaxValue, 1)]
    public void TotalPages_ForCountAndPageSize_ReturnsCeilingClampedToOne(int totalCount, int pageSize, int expected)
        => PageMath.TotalPages(totalCount, pageSize).Should().Be(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TotalPages_PageSizeBelowOne_Throws(int pageSize)
    {
        var act = () => PageMath.TotalPages(10, pageSize);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1, 30, 0)]
    [InlineData(2, 30, 30)]
    [InlineData(5, 100, 400)]
    [InlineData(21_474_837, 100, 2_147_483_600)]
    public void Skip_ValidPage_ReturnsZeroBasedRowOffset(int page, int pageSize, int expected)
        => PageMath.Skip(page, pageSize).Should().Be(expected);

    [Fact]
    public void Skip_OffsetOverflowsInt_ThrowsInsteadOfWrappingNegative()
    {
        var act = () => PageMath.Skip(100_000_000, 30);

        act.Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData(1, 100, true)]
    [InlineData(21_474_837, 100, true)]
    [InlineData(21_474_838, 100, false)]
    [InlineData(int.MaxValue, 1, true)]
    [InlineData(int.MaxValue, 2, false)]
    [InlineData(0, 30, false)]
    public void IsOffsetInRange_ForPageAndSize_ReportsWhetherSkipFitsInInt(int page, int pageSize, bool expected)
        => PageMath.IsOffsetInRange(page, pageSize).Should().Be(expected);

    [Fact]
    public void TotalPages_MatchesPreviousFloatingPointFormula_ForAllValidPageSizes()
    {
        // Characterisation against the formula this replaced:
        // Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize)).
        for (var pageSize = 1; pageSize <= 100; pageSize++)
        {
            for (var totalCount = 0; totalCount <= 1000; totalCount++)
            {
                var previous = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));

                PageMath.TotalPages(totalCount, pageSize).Should().Be(
                    previous, "totalCount={0}, pageSize={1}", totalCount, pageSize);
            }
        }
    }
}
