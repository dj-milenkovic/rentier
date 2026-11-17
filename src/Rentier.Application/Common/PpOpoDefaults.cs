namespace Rentier.Application.Common;

/// <summary>Default values used when building a PP-OPO declaration.</summary>
public static class PpOpoDefaults
{
    /// <summary>
    /// Default text for PP-OPO field 3.3 "Ostalo" (<c>ns1:Ostalo</c>): income paid out to a
    /// brokerage account. Pre-filled on manual filings and used on export whenever a
    /// filing's (or its importer's) payment notes are blank.
    /// </summary>
    public const string DEFAULT_PAYMENT_NOTES = "Isplata na brokerski račun";
}
