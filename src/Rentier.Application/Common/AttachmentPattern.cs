using System.Text.RegularExpressions;

namespace Rentier.Application.Common;

/// <summary>
/// The single definition of how an importer's user-entered <c>AttachmentRegex</c> is built.
/// Validation (when the importer is saved) and matching (during mailbox sync, against attachment
/// file names taken from incoming email) both go through <see cref="Create"/>, so they always
/// agree on options and on the match timeout.
/// </summary>
public static class AttachmentPattern
{
    /// <summary>
    /// Upper bound for one match against one file name. The pattern is user-entered and the
    /// input arrives by email, so a catastrophically backtracking pattern must fail fast
    /// (<see cref="RegexMatchTimeoutException"/>) instead of hanging the sync.
    /// </summary>
    public static readonly TimeSpan MATCH_TIMEOUT = TimeSpan.FromSeconds(1);

    private const RegexOptions OPTIONS = RegexOptions.Compiled | RegexOptions.IgnoreCase;

    /// <summary>Builds the matcher for <paramref name="pattern"/>.</summary>
    /// <exception cref="ArgumentException">The pattern is not a valid regular expression.</exception>
    public static Regex Create(string pattern) => new(pattern, OPTIONS, MATCH_TIMEOUT);

    /// <summary>
    /// Checks that <paramref name="pattern"/> is usable. Empty means "no attachment filter"
    /// and is valid. Returns <c>null</c> when valid, otherwise the validation error.
    /// </summary>
    public static Error? Validate(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return null;

        try
        {
            _ = Create(pattern);
            return null;
        }
        catch (ArgumentException ex)
        {
            return new Error(ErrorCodes.IMPORTER_VALIDATION_INVALID_REGEX, ex.Message);
        }
    }
}
