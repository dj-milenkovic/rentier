using System.Text.RegularExpressions;
using Rentier.Application.ReleaseNotes;

namespace Rentier.ReleaseNotes;

/// <summary>
/// Builds the release-notes document for a release: the new version plus the releases before
/// it, <see cref="ReleaseNotesDocument.MAX_VERSIONS"/> in all, counting only versions released
/// after release notes were introduced. Each version's notes are the "## Release note" sections
/// of the PRs merged for it, leaving out PRs reverted in the same version.
/// </summary>
public static partial class ReleaseNotesBuilder
{
    /// <summary>
    /// The versions the document covers, newest first: <paramref name="newVersion"/> and the latest
    /// tags before it that are newer than <paramref name="since"/> (the last release made before
    /// PR descriptions carried release notes).
    /// </summary>
    public static IReadOnlyList<Version> VersionsToCover(string newVersion, IEnumerable<string> tags, string since)
    {
        if (!ReleaseNotesDocument.TryParseVersion(newVersion, out var newest))
            throw new ArgumentException($"'{newVersion}' is not a version.", nameof(newVersion));
        if (!ReleaseNotesDocument.TryParseVersion(since, out var cutoff))
            throw new ArgumentException($"'{since}' is not a version.", nameof(since));

        var older = tags
            .Select(t => ReleaseNotesDocument.TryParseVersion(t, out var v) ? v : null)
            .OfType<Version>()
            .Where(v => v < newest && v > cutoff);

        return [.. older.Append(newest).Distinct().OrderByDescending(v => v).Take(ReleaseNotesDocument.MAX_VERSIONS)];
    }

    /// <summary>
    /// What a version without user-facing changes shows: such a release is only made on purpose,
    /// with the release workflow's override (e.g. a dependency security fix).
    /// </summary>
    public static ReleaseNotesEntry GenericEntry(Version version) => new(
        version,
        "- Stability and security improvements.",
        "- Poboljšanja stabilnosti i bezbednosti.");

    /// <summary>
    /// Builds the entries for <paramref name="versions"/>. <paramref name="prBodies"/> returns the
    /// descriptions of the PRs merged for a version, oldest PR first. A PR that another PR of the
    /// same version reverts (<see cref="RevertedPrs"/>) is left out. A version whose PRs carry
    /// no note gets <see cref="GenericEntry"/> and is listed in
    /// <see cref="BuildResult.WithoutUserFacingChanges"/>.
    /// </summary>
    public static BuildResult Build(IReadOnlyList<Version> versions, Func<Version, IEnumerable<PrBody>> prBodies)
    {
        var entries = new List<ReleaseNotesEntry>();
        var withoutChanges = new List<Version>();

        foreach (var version in versions)
        {
            var prs = prBodies(version).ToList();
            var reverted = RevertedPrs(prs.Select(p => p.Body));
            var notes = prs
                .Where(p => !reverted.Contains(p.Number))
                .Select(p => PrReleaseNote.Parse(p.Body))
                .Where(n => !n.IsEmpty)
                .ToList();
            var entry = new ReleaseNotesEntry(
                version,
                string.Join('\n', notes.Select(n => n.English).Where(t => t.Length > 0)),
                string.Join('\n', notes.Select(n => n.Serbian).Where(t => t.Length > 0)));

            if (entry.HasNotes)
            {
                entries.Add(entry);
            }
            else
            {
                withoutChanges.Add(version);
                entries.Add(GenericEntry(version));
            }
        }

        return new BuildResult(entries, withoutChanges);
    }

    /// <summary>
    /// The PRs these descriptions revert: a line <c>Reverts #N</c> or <c>Reverts owner/repo#N</c>,
    /// as GitHub's Revert button writes it.
    /// </summary>
    internal static HashSet<int> RevertedPrs(IEnumerable<string> bodies) =>
    [
        .. bodies.SelectMany(body => RevertsLine().Matches(body.ReplaceLineEndings("\n")))
            .Select(m => int.TryParse(m.Groups["pr"].Value, out var n) ? n : 0)
            .Where(n => n > 0),
    ];

    [GeneratedRegex(@"^\s*Reverts\s+(?:[\w.-]+/[\w.-]+)?#(?<pr>\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RevertsLine();
}

/// <summary>The description of one merged PR.</summary>
public sealed record PrBody(int Number, string Body);

/// <summary>The built entries, and the versions that had no user-facing change.</summary>
public sealed record BuildResult(IReadOnlyList<ReleaseNotesEntry> Entries, IReadOnlyList<Version> WithoutUserFacingChanges);
