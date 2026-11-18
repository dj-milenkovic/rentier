using Rentier.Domain.Enums;

namespace Rentier.Domain.Services;

/// <summary>
/// The PP-OPO filing lifecycle rules (Init → Filed → Paid) in one place: <see cref="Entities.Filing"/>
/// enforces them, and every layer that needs to show or query them asks here instead of
/// re-deriving them.
/// </summary>
public static class FilingLifecycle
{
    /// <summary>The only status a filing in <paramref name="status"/> may advance to; null when final.</summary>
    public static FilingStatus? NextStatus(FilingStatus status) => status switch
    {
        FilingStatus.Init => FilingStatus.Filed,
        FilingStatus.Filed => FilingStatus.Paid,
        _ => null,
    };

    /// <summary>
    /// A payment reference identifies the tax payment for a submitted return, so it can only be
    /// set once the filing has been filed (issue #65).
    /// </summary>
    public static bool AcceptsPaymentReference(FilingStatus status)
        => status is FilingStatus.Filed or FilingStatus.Paid;

    /// <summary>Not yet paid: still needs filing and/or payment (counts toward outstanding tax).</summary>
    public static bool IsOutstanding(FilingStatus status) => status != FilingStatus.Paid;
}
