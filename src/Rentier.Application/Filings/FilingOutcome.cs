using Rentier.Application.Common;
using Rentier.Domain.Entities;

namespace Rentier.Application.Filings;

/// <summary>What happened when an income event was filed.</summary>
public abstract record FilingOutcome
{
    private FilingOutcome()
    {
    }

    /// <summary>A new filing was added (staged, or saved by a standalone repository).</summary>
    public sealed record Created(Filing Filing) : FilingOutcome;

    /// <summary>
    /// A filing for the same taxpayer, paying entity and income date with the same gross RSD
    /// already exists: the event was filed before. Nothing was added.
    /// </summary>
    public sealed record AlreadyFiled(Filing Existing) : FilingOutcome;

    /// <summary>
    /// A filing for the same taxpayer, paying entity and income date exists with a different
    /// gross RSD — e.g. a broker correction. A second filing is never created silently; nothing
    /// was added and <see cref="Error"/> (<see cref="ErrorCodes.FILING_CORRECTION_DETECTED"/>)
    /// explains what needs manual review.
    /// </summary>
    public sealed record CorrectionDetected(Filing Existing, decimal NewGrossIncomeRsd, Error Error) : FilingOutcome;

    /// <summary>
    /// The filing could not be computed: no exchange rate (<see cref="ErrorCodes.RATE_SOURCE_UNAVAILABLE"/>
    /// means temporarily — retry later) or a business-rule violation.
    /// </summary>
    public sealed record Failed(Error Error) : FilingOutcome;
}
