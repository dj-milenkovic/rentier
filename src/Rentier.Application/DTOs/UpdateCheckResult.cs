using Rentier.Application.ReleaseNotes;

namespace Rentier.Application.DTOs;

/// <summary>
/// Result of an update availability check.
/// </summary>
/// <param name="IsUpdateAvailable">Whether a newer version is available.</param>
/// <param name="TargetVersion">The version string of the available update, or null if no update is available.</param>
/// <param name="ReleaseNotes">
/// Notes for every version between the installed one (exclusive) and the update (inclusive),
/// newest first. Empty when there is no update or the update carries no notes.
/// </param>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string? TargetVersion,
    IReadOnlyList<ReleaseNotesEntry>? ReleaseNotes = null)
{
    /// <summary><see cref="ReleaseNotes"/>, never null.</summary>
    public IReadOnlyList<ReleaseNotesEntry> Notes => ReleaseNotes ?? [];
}
