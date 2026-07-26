using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.UnitTests;

/// <summary>
/// Property-based tests for FilingStatus state-machine transition invariants.
/// Exhaustively covers all 9 combinations of {Init, Filed, Paid} × {Init, Filed, Paid}.
/// Exactly 2 transitions are valid; the remaining 7 (including all self-transitions) must throw.
/// </summary>
public class FilingStatusTransitionProperties
{
    // The exactly 2 valid transitions
    private static readonly HashSet<(FilingStatus, FilingStatus)> ValidSet = new()
    {
        (FilingStatus.Init, FilingStatus.Filed),
        (FilingStatus.Filed, FilingStatus.Paid),
    };

    private static readonly (FilingStatus Source, FilingStatus Target)[] ValidPairs =
        ValidSet.Select(p => (p.Item1, p.Item2)).ToArray();

    // All 9 - 2 = 7 invalid transitions
    private static readonly (FilingStatus Source, FilingStatus Target)[] InvalidPairs =
        (from src in Enum.GetValues<FilingStatus>()
         from tgt in Enum.GetValues<FilingStatus>()
         where !ValidSet.Contains((src, tgt))
         select (src, tgt)).ToArray();

    /// <summary>
    /// Every valid (source → target) transition succeeds and the filing ends up
    /// in the target status. Covers the 2 valid pairs.
    /// </summary>
    [Property]
    public Property AdvanceStatus_ValidTransition_FilingMovesToNewStatus()
        => Prop.ForAll(
            Gen.Elements(ValidPairs).ToArbitrary(),
            pair =>
            {
                var filing = CreateFilingInState(pair.Source);
                filing.AdvanceStatus(pair.Target);
                return filing.Status == pair.Target;
            });

    /// <summary>
    /// Every invalid (source → target) pair throws DomainException and leaves the
    /// filing status unchanged. Covers all 7 invalid pairs (SC-005).
    /// </summary>
    [Property]
    public Property AdvanceStatus_InvalidTransition_ThrowsDomainException()
        => Prop.ForAll(
            Gen.Elements(InvalidPairs).ToArbitrary(),
            pair =>
            {
                var filing = CreateFilingInState(pair.Source);
                var originalStatus = filing.Status;
                var threw = false;

                try { filing.AdvanceStatus(pair.Target); }
                catch (DomainException) { threw = true; }

                // Must throw AND must not mutate status
                return threw && filing.Status == originalStatus;
            });

    /// <summary>Creates a Filing already advanced to the given status.</summary>
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
