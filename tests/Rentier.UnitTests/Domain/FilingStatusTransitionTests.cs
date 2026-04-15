using Xunit;
using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;

namespace Rentier.UnitTests;

public class FilingStatusTransitionTests
{
    [Theory]
    [InlineData(FilingStatus.Init,  FilingStatus.Filed, false)]  // Init → Filed: valid
    [InlineData(FilingStatus.Filed, FilingStatus.Paid,  false)]  // Filed → Paid: valid
    [InlineData(FilingStatus.Paid,  FilingStatus.Init,  true)]   // Paid → Init: invalid
    [InlineData(FilingStatus.Init,  FilingStatus.Paid,  true)]   // Init → Paid: invalid (skip Filed)
    [InlineData(FilingStatus.Filed, FilingStatus.Init,  true)]   // Filed → Init: invalid
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

    /// <summary>Creates a Filing already advanced to the given status via the public factory.</summary>
    private static Filing CreateFilingInState(FilingStatus status)
    {
        var filing = Filing.CreateFromIncome(
            Guid.NewGuid(), IncomeType.Dividend, "ACME Corp",
            new DateOnly(2025, 1, 1), 1000m, 150m, 150m, 0m,
            new DateOnly(2025, 2, 1));

        if (status is FilingStatus.Filed or FilingStatus.Paid)
            filing.AdvanceStatus(FilingStatus.Filed);
        if (status is FilingStatus.Paid)
            filing.AdvanceStatus(FilingStatus.Paid);

        return filing;
    }
}
