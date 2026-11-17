namespace Rentier.Application.Common;

/// <summary>
/// The currencies for which NBS publishes a middle rate — the single list used by the rate
/// adapters (to reject anything else as <see cref="ErrorCodes.UNSUPPORTED_CURRENCY"/>) and by the
/// manual-filing screen (to offer only currencies a rate can be found for).
/// </summary>
public static class NbsCurrencies
{
    /// <summary>Supported currency codes in display order (most common first).</summary>
    public static IReadOnlyList<string> All { get; } =
        ["USD", "EUR", "GBP", "CHF", "AUD", "CAD", "CZK", "DKK", "HUF", "JPY", "NOK", "PLN", "SEK", "TRY", "AED"];

    private static readonly HashSet<string> Lookup = new(All, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when NBS publishes a rate for <paramref name="currency"/> (case-insensitive).</summary>
    public static bool IsSupported(string currency) => Lookup.Contains(currency);
}
