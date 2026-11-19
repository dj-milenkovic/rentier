using FluentAssertions;
using Rentier.Domain.Enums;
using Rentier.Domain.Services;
using Xunit;

namespace Rentier.UnitTests;

public class FilingLifecycleTests
{
    // "Outstanding" drives the unpaid-tax totals: anything not yet Paid still needs action.
    [Theory]
    [InlineData(FilingStatus.Init, true)]
    [InlineData(FilingStatus.Filed, true)]
    [InlineData(FilingStatus.Paid, false)]
    public void IsOutstanding_ForStatus_ReturnsWhetherFilingIsUnpaid(FilingStatus status, bool expected)
    {
        FilingLifecycle.IsOutstanding(status).Should().Be(expected);
    }
}
