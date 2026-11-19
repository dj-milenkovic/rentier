using System.Text.RegularExpressions;

namespace Rentier.Desktop.Logging;

/// <summary>
/// Last line of defence for the log file: scrubs text that is about to be written. The primary
/// defence is not logging sensitive values at all (see the Logging rules); this catches the
/// accidents — usernames inside file paths, emails, broker account ids and long digit runs
/// (JMBG, IBAN, account numbers). It cannot recognise amounts or tickers, so those must never
/// reach a log call.
/// </summary>
internal static partial class PiiRedactor
{
    private const int TIMEOUT_MS = 250;
    private const string REDACTION_FAILED = "[redaction-failed]";

    // C:\Users\<name>\...  (any drive, either slash)
    [GeneratedRegex(@"(?<prefix>[A-Za-z]:[\\/]Users[\\/])[^\\/\s:*?""<>|]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TIMEOUT_MS)]
    private static partial Regex WindowsProfilePath();

    // /home/<name>/...  and  /Users/<name>/...
    [GeneratedRegex(@"(?<prefix>(?<![A-Za-z]:)/(?:home|Users)/)[^/\s]+", RegexOptions.CultureInvariant, TIMEOUT_MS)]
    private static partial Regex UnixProfilePath();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant, TIMEOUT_MS)]
    private static partial Regex EmailAddress();

    // Interactive Brokers account ids: U1234567, F1234567, paper accounts DU1234567 / DF1234567.
    // Lookarounds instead of \b so ids inside statement file names (U1234567_20250101.csv) match.
    [GeneratedRegex(@"(?<![A-Za-z0-9])(?:[Dd]?[UuFf]|[Dd])\d{6,9}(?!\d)", RegexOptions.CultureInvariant, TIMEOUT_MS)]
    private static partial Regex BrokerAccountId();

    // IBAN: country code, check digits, 11–30 alphanumerics, compact or in groups of four
    // (RS35260005601001611379, RS35 2600 0560 1001 6113 79). Minimum length excludes ISINs.
    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?:[A-Z0-9]{11,30}|(?: [A-Z0-9]{4}){2,7}(?: [A-Z0-9]{1,4})?)\b", RegexOptions.CultureInvariant, TIMEOUT_MS)]
    private static partial Regex Iban();

    // Serbian bank account numbers in dashed form: bank-account-control (160-0000000123456-78).
    [GeneratedRegex(@"(?<![\w-])\d{3}-\d{1,13}-\d{2}(?![\w-])", RegexOptions.CultureInvariant, TIMEOUT_MS)]
    private static partial Regex SerbianBankAccount();

    // 8+ digit runs. Neighbouring '-' or letters are excluded so GUID segments, dates such as
    // 2026-11-19 and file-name stamps are left alone; IBANs and dashed account numbers are
    // caught by the dedicated patterns above.
    [GeneratedRegex(@"(?<![\w-])\d{8,}(?![\w-])", RegexOptions.CultureInvariant, TIMEOUT_MS)]
    private static partial Regex LongDigitRun();

    /// <summary>Scrubs <paramref name="input"/>. Never throws.</summary>
    /// <param name="input">The text about to be written to the log.</param>
    /// <param name="homeFolder">
    /// The current user's profile folder; defaults to <see cref="Environment.SpecialFolder.UserProfile"/>.
    /// Replaced with <c>~</c> wherever it appears.
    /// </param>
    public static string Redact(string? input, string? homeFolder = null)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;

        try
        {
            var text = input;

            homeFolder ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(homeFolder) && homeFolder.Length > 3)
            {
                text = text.Replace(homeFolder, "~", StringComparison.OrdinalIgnoreCase);
                // Exception messages sometimes carry the forward-slash form of the same path.
                text = text.Replace(homeFolder.Replace('\\', '/'), "~", StringComparison.OrdinalIgnoreCase);
            }

            text = WindowsProfilePath().Replace(text, "${prefix}<user>");
            text = UnixProfilePath().Replace(text, "${prefix}<user>");
            text = EmailAddress().Replace(text, "[email]");
            text = BrokerAccountId().Replace(text, "[account]");
            text = Iban().Replace(text, "[number]");
            text = SerbianBankAccount().Replace(text, "[number]");
            text = LongDigitRun().Replace(text, "[number]");
            return text;
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail closed: writing nothing is safer than writing unscrubbed text.
            return REDACTION_FAILED;
        }
    }
}
