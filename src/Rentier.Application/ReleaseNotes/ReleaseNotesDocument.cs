using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Rentier.Application.ReleaseNotes;

/// <summary>
/// Reads and writes the release-notes document that ships with every release: inside the app
/// build (Settings → What's new) and inside the update package (the update bar's What's new).
/// The release workflow writes it from <c>.github/release-notes/history.md</c> plus the
/// "## Release note" sections of merged PR descriptions.
/// </summary>
/// <remarks>
/// Format, one block per version, newest first:
/// <code>
/// ## v1.4.0
///
/// ### EN
/// - English notes (Markdown)
///
/// ### SR
/// - Serbian (Latin) notes (Markdown)
/// </code>
/// HTML comments are ignored. Unknown headings inside a version are kept as part of the
/// current language's Markdown, so the notes themselves may use level-4+ headings.
/// </remarks>
public static partial class ReleaseNotesDocument
{
    /// <summary>How many released versions the document covers (the newest one included).</summary>
    public const int MAX_VERSIONS = 10;

    private const string ENGLISH = "EN";
    private const string SERBIAN = "SR";

    /// <summary>Parses a document. Never throws: unreadable input yields no entries.</summary>
    /// <returns>Entries with notes, newest version first; a repeated version keeps its first block.</returns>
    public static IReadOnlyList<ReleaseNotesEntry> Parse(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var text = HtmlComment().Replace(markdown.Replace("\r\n", "\n", StringComparison.Ordinal), string.Empty);
        var entries = new List<ReleaseNotesEntry>();

        Version? version = null;
        string? language = null;
        var english = new StringBuilder();
        var serbian = new StringBuilder();

        void Flush()
        {
            if (version is not null)
            {
                var entry = new ReleaseNotesEntry(version, english.ToString().Trim(), serbian.ToString().Trim());
                if (entry.HasNotes && entries.TrueForAll(e => e.Version != entry.Version))
                    entries.Add(entry);
            }

            english.Clear();
            serbian.Clear();
            language = null;
        }

        foreach (var line in text.Split('\n'))
        {
            var versionHeading = VersionHeading().Match(line);
            if (versionHeading.Success)
            {
                Flush();
                version = TryParseVersion(versionHeading.Groups["version"].Value, out var parsed) ? parsed : null;
                continue;
            }

            var languageHeading = LanguageHeading().Match(line);
            if (languageHeading.Success)
            {
                language = languageHeading.Groups["lang"].Value.ToUpperInvariant();
                continue;
            }

            // Append '\n' explicitly: AppendLine writes Environment.NewLine ("\r\n" on Windows),
            // which would make the parsed notes differ by platform.
            switch (language)
            {
                case ENGLISH: english.Append(line.TrimEnd()).Append('\n'); break;
                case SERBIAN: serbian.Append(line.TrimEnd()).Append('\n'); break;
            }
        }

        Flush();
        entries.Sort((a, b) => b.Version.CompareTo(a.Version));
        return entries;
    }

    /// <summary>Writes entries in the document format, newest first. Entries without notes are left out.</summary>
    public static string Format(IEnumerable<ReleaseNotesEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (var entry in entries.Where(e => e.HasNotes).OrderByDescending(e => e.Version))
        {
            if (sb.Length > 0)
                sb.Append('\n');

            sb.Append(CultureInfo.InvariantCulture, $"## {entry.DisplayVersion}\n");
            AppendSection(sb, ENGLISH, entry.English);
            AppendSection(sb, SERBIAN, entry.Serbian);
        }

        return sb.ToString();
    }

    /// <summary>The entries for versions newer than <paramref name="installed"/>, newest first.</summary>
    public static IReadOnlyList<ReleaseNotesEntry> NewerThan(IEnumerable<ReleaseNotesEntry> entries, Version installed) =>
        [.. entries.Where(e => e.Version > Normalize(installed)).OrderByDescending(e => e.Version)];

    /// <summary>
    /// Parses <c>1.4.0</c>, <c>v1.4.0</c>, <c>1.4.0-beta.1</c> or <c>1.4.0+sha</c> to
    /// major.minor.patch. Pre-release and build suffixes are ignored.
    /// </summary>
    public static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var core = text.Trim().TrimStart('v', 'V');
        var suffix = core.IndexOfAny(['-', '+']);
        if (suffix >= 0)
            core = core[..suffix];

        if (!Version.TryParse(core, out var parsed))
            return false;

        version = Normalize(parsed);
        return true;
    }

    // "1.4" and "1.4.0" must compare equal; Version treats a missing build part as -1.
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    private static void AppendSection(StringBuilder sb, string language, string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return;

        sb.Append(CultureInfo.InvariantCulture, $"\n### {language}\n");
        sb.Append(notes.Replace("\r\n", "\n", StringComparison.Ordinal).Trim());
        sb.Append('\n');
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"^##\s+(?<version>v?\d+(\.\d+){1,2}\S*)\s*$", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionHeading();

    [GeneratedRegex(@"^###\s+(?<lang>EN|SR)\s*$", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LanguageHeading();
}
