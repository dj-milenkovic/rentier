using Xunit;
using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.UnitTests;

public class FilingStatusTransitionTests
{
    [Theory]
    [InlineData(FilingStatus.Init, FilingStatus.Filed, false)]  // Init → Filed: valid
    [InlineData(FilingStatus.Filed, FilingStatus.Paid, false)]  // Filed → Paid: valid
    [InlineData(FilingStatus.Paid, FilingStatus.Init, true)]   // Paid → Init: invalid
    [InlineData(FilingStatus.Init, FilingStatus.Paid, true)]   // Init → Paid: invalid (skip Filed)
    [InlineData(FilingStatus.Filed, FilingStatus.Init, true)]   // Filed → Init: invalid
    public void AdvanceStatus_Transition_BehavesCorrectly(
        FilingStatus fromStatus, FilingStatus toStatus, bool shouldThrow)
    {
        var filing = CreateFilingInState(fromStatus);

        var act = () => filing.AdvanceStatus(toStatus);

        if (shouldThrow)
            act.Should().Throw<DomainException>();
        else
            act.Should().NotThrow();
    }

    [Fact]
    public void AdvanceStatus_InitToFiled_SetsStatusToFiled()
    {
        var filing = CreateFilingInState(FilingStatus.Init);

        filing.AdvanceStatus(FilingStatus.Filed);

        filing.Status.Should().Be(FilingStatus.Filed);
    }

    [Fact]
    public void AdvanceStatus_FiledToPaid_SetsStatusToPaid()
    {
        var filing = CreateFilingInState(FilingStatus.Filed);

        filing.AdvanceStatus(FilingStatus.Paid);

        filing.Status.Should().Be(FilingStatus.Paid);
    }

    [Fact]
    public void AdvanceStatus_FiledToFiled_ThrowsDomainException()
    {
        var filing = CreateFilingInState(FilingStatus.Filed);

        var act = () => filing.AdvanceStatus(FilingStatus.Filed);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdvanceStatus_PaidToPaid_ThrowsDomainException()
    {
        var filing = CreateFilingInState(FilingStatus.Paid);

        var act = () => filing.AdvanceStatus(FilingStatus.Paid);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdvanceStatus_PaidToFiled_ThrowsDomainException()
    {
        var filing = CreateFilingInState(FilingStatus.Paid);

        var act = () => filing.AdvanceStatus(FilingStatus.Filed);

        act.Should().Throw<DomainException>();
    }

    /// <summary>Creates a Filing already advanced to the given status via the public factory.</summary>
    private static Filing CreateFilingInState(FilingStatus status)
    {
        var filing = Filing.CreateFromIncome(
            new FilingInfo(IncomeType.Dividend, "ACME Corp", new DateOnly(2025, 1, 1), 1000m, 150m, 150m, 0m),
            Guid.NewGuid(), new DateOnly(2025, 2, 1), new FilingProvenance());

        if (status is FilingStatus.Filed or FilingStatus.Paid)
            filing.AdvanceStatus(FilingStatus.Filed);
        if (status is FilingStatus.Paid)
            filing.AdvanceStatus(FilingStatus.Paid);

        return filing;
    }
}
