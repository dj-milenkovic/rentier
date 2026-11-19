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

    // Interactive Brokers account ids look like U1234567 (sometimes with a leading letter).
    [GeneratedRegex(@"\b[UuDdFf]\d{6,9}\b", RegexOptions.CultureInvariant, TIMEOUT_MS)]
    private static partial Regex BrokerAccountId();

    // 8+ digit runs. Neighbouring '-' or letters are excluded so GUID segments, dates such as
    // 2026-11-19 and file-name stamps are left alone.
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
