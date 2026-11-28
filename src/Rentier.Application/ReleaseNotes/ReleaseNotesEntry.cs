namespace Rentier.Application.ReleaseNotes;

/// <summary>
/// User-facing notes for one released version, in English and Serbian (Latin), as Markdown.
/// </summary>
/// <param name="Version">The released version (major.minor.patch).</param>
/// <param name="English">English notes; may be empty when only Serbian notes exist.</param>
/// <param name="Serbian">Serbian (Latin) notes; may be empty when only English notes exist.</param>
public sealed record ReleaseNotesEntry(Version Version, string English, string Serbian)
{
    /// <summary>The version as shown to users, e.g. <c>v1.4.0</c>.</summary>
    public string DisplayVersion => $"v{Version}";

    /// <summary>
    /// The notes for a UI culture: Serbian for any <c>sr</c> culture, English otherwise.
    /// Falls back to the other language when the preferred one is empty, so an entry is never
    /// shown blank while it has notes at all.
    /// </summary>
    public string For(string cultureCode)
    {
        var preferSerbian = cultureCode.StartsWith("sr", StringComparison.OrdinalIgnoreCase);
        var preferred = preferSerbian ? Serbian : English;
        var other = preferSerbian ? English : Serbian;
        return string.IsNullOrWhiteSpace(preferred) ? other : preferred;
    }

    /// <summary>Whether the entry carries notes in at least one language.</summary>
    public bool HasNotes => !string.IsNullOrWhiteSpace(English) || !string.IsNullOrWhiteSpace(Serbian);
}
