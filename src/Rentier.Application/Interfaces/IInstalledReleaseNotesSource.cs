namespace Rentier.Application.Interfaces;

/// <summary>
/// Reads the release-notes document built into the running copy of Rentier
/// (see <see cref="ReleaseNotes.ReleaseNotesDocument"/>).
/// </summary>
public interface IInstalledReleaseNotesSource
{
    /// <summary>The document's Markdown, or null when this build carries none.</summary>
    Task<string?> ReadAsync(CancellationToken cancellationToken = default);
}
