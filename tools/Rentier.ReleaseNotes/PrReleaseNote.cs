using System.Text;
using System.Text.RegularExpressions;

namespace Rentier.ReleaseNotes;

/// <summary>
/// The user-facing release note written in a PR description:
/// <code>
/// ## Release note
/// EN: What changed, in words a user understands.
/// SR: Isto to, na srpskom.
/// </code>
/// Each language may run over several lines and may be a Markdown list. HTML comments (the PR
/// template's hints) are ignored, so an untouched template counts as "no note".
/// </summary>
public sealed partial record PrReleaseNote(bool HasSection, string English, string Serbian)
{
    /// <summary>No section, or a section with nothing written in it: the PR is not shown to users.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(English) && string.IsNullOrWhiteSpace(Serbian);

    /// <summary>A note must come in both languages; one without the other fails the PR check.</summary>
    public IReadOnlyList<string> Problems
    {
        get
        {
            if (IsEmpty)
                return [];

            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(English))
                problems.Add("The release note has an SR: line but no EN: line. Add the English text.");
            if (string.IsNullOrWhiteSpace(Serbian))
                problems.Add("The release note has an EN: line but no SR: line. Add the Serbian (Latin) text.");
            return problems;
        }
    }

    /// <summary>Reads the note from a PR description. Never throws.</summary>
    public static PrReleaseNote Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return new PrReleaseNote(false, string.Empty, string.Empty);

        var lines = HtmlComment().Replace(body.ReplaceLineEndings("\n"), string.Empty).Split('\n');

        var start = Array.FindIndex(lines, l => SectionHeading().IsMatch(l));
        if (start < 0)
            return new PrReleaseNote(false, string.Empty, string.Empty);

        var level = SectionHeading().Match(lines[start]).Groups["marks"].Length;
        var english = new StringBuilder();
        var serbian = new StringBuilder();
        StringBuilder? current = null;

        foreach (var line in lines.Skip(start + 1))
        {
            var heading = AnyHeading().Match(line);
            if (heading.Success && heading.Groups["marks"].Length <= level)
                break;

            var marker = LanguageMarker().Match(line);
            if (marker.Success)
            {
                current = marker.Groups["lang"].Value.Equals("EN", StringComparison.OrdinalIgnoreCase) ? english : serbian;
                current.AppendLine(marker.Groups["text"].Value);
                continue;
            }

            current?.AppendLine(line.TrimEnd());
        }

        return new PrReleaseNote(true, ToBullets(english.ToString()), ToBullets(serbian.ToString()));
    }

    /// <summary>
    /// Makes a note one Markdown bullet (or keeps the list it already is), so the notes of every
    /// PR in a release read as one list.
    /// </summary>
    internal static string ToBullets(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Trim('\n', ' ').Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        if (lines.Count == 0 || string.IsNullOrWhiteSpace(lines[0]))
            return string.Empty;

        if (ListMarker().IsMatch(lines[0]))
            return string.Join('\n', lines);

        return "- " + lines[0].Trim() + string.Concat(lines.Skip(1).Select(l => "\n" + (l.Length == 0 ? l : "  " + l.TrimStart())));
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"^(?<marks>#{2,4})\s*Release notes?\s*:?\s*$", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SectionHeading();

    [GeneratedRegex(@"^(?<marks>#{1,6})\s", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AnyHeading();

    [GeneratedRegex(@"^\s*(\*\*)?(?<lang>EN|SR)(\*\*)?\s*:(\*\*)?\s*(?<text>.*)$", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LanguageMarker();

    [GeneratedRegex(@"^\s*([-*+]|\d+[.)])\s", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ListMarker();
}
