using Xunit;
using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Tests;

public class FilingStatusTransitionTests
{
    [Fact]
    public void AdvanceStatus_FromInitToFiled_StatusBecomesFiled()
    {
        var filing = new Filing(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        filing.AdvanceStatus(FilingStatus.Filed);
        filing.Status.Should().Be(FilingStatus.Filed);
    }

    [Fact]
    public void AdvanceStatus_FromFiledToPaid_StatusBecomesPaid()
    {
        var filing = new Filing(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        filing.AdvanceStatus(FilingStatus.Filed);
        filing.AdvanceStatus(FilingStatus.Paid);
        filing.Status.Should().Be(FilingStatus.Paid);
    }

    [Fact]
    public void AdvanceStatus_FromPaidToInit_ThrowsDomainException()
    {
        var filing = new Filing(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        filing.AdvanceStatus(FilingStatus.Filed);
        filing.AdvanceStatus(FilingStatus.Paid);

        var act = () => filing.AdvanceStatus(FilingStatus.Init);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdvanceStatus_FromInitToPaid_ThrowsDomainException()
    {
        var filing = new Filing(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        var act = () => filing.AdvanceStatus(FilingStatus.Paid);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdvanceStatus_FromFiledToInit_ThrowsDomainException()
    {
        var filing = new Filing(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        filing.AdvanceStatus(FilingStatus.Filed);

        var act = () => filing.AdvanceStatus(FilingStatus.Init);
        act.Should().Throw<DomainException>();
    }
}
