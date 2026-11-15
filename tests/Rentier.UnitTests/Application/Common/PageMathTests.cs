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
