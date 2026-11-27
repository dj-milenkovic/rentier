using Rentier.Application.ReleaseNotes;

namespace Rentier.ReleaseNotes;

/// <summary>
/// Builds the release-notes document for a release: the new version plus the releases before
/// it, <see cref="ReleaseNotesDocument.MAX_VERSIONS"/> in all. Versions up to the newest one in
/// <c>.github/release-notes/history.md</c> take their notes from that file (written by hand for releases
/// made before PR descriptions carried notes); later versions take them from the
/// "## Release note" sections of the PRs merged for them.
/// </summary>
public static class ReleaseNotesBuilder
{
    /// <summary>The versions the document covers, newest first: <paramref name="newVersion"/> and the latest tags before it.</summary>
    public static IReadOnlyList<Version> VersionsToCover(string newVersion, IEnumerable<string> tags)
    {
        if (!ReleaseNotesDocument.TryParseVersion(newVersion, out var newest))
            throw new ArgumentException($"'{newVersion}' is not a version.", nameof(newVersion));

        var older = tags
            .Select(t => ReleaseNotesDocument.TryParseVersion(t, out var v) ? v : null)
            .OfType<Version>()
            .Where(v => v < newest);

        return [.. older.Append(newest).Distinct().OrderByDescending(v => v).Take(ReleaseNotesDocument.MAX_VERSIONS)];
    }

    /// <summary>The covered versions whose notes come from PR descriptions (newer than everything in the history file).</summary>
    public static IReadOnlyList<Version> VersionsNeedingPrNotes(
        IReadOnlyList<ReleaseNotesEntry> history, IReadOnlyList<Version> versions)
    {
        var coveredUpTo = history.Count > 0 ? history.Max(e => e.Version)! : null;
        return [.. versions.Where(v => coveredUpTo is null || v > coveredUpTo)];
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
    /// descriptions of the PRs merged for a version, oldest PR first. A version newer than the
    /// history whose PRs carry no note gets <see cref="GenericEntry"/> and is listed in
    /// <see cref="BuildResult.WithoutUserFacingChanges"/>.
    /// </summary>
    public static BuildResult Build(
        IReadOnlyList<ReleaseNotesEntry> history,
        IReadOnlyList<Version> versions,
        Func<Version, IEnumerable<string>> prBodies)
    {
        var fromPrs = VersionsNeedingPrNotes(history, versions).ToHashSet();
        var entries = new List<ReleaseNotesEntry>();
        var withoutChanges = new List<Version>();

        foreach (var version in versions)
        {
            if (!fromPrs.Contains(version))
            {
                var recorded = history.FirstOrDefault(e => e.Version == version);
                if (recorded is not null)
                    entries.Add(recorded);
                continue;
            }

            var notes = prBodies(version).Select(PrReleaseNote.Parse).Where(n => !n.IsEmpty).ToList();
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
}

/// <summary>The built entries, and the versions that had no user-facing change.</summary>
public sealed record BuildResult(IReadOnlyList<ReleaseNotesEntry> Entries, IReadOnlyList<Version> WithoutUserFacingChanges);
