using Rentier.Application.DTOs;

namespace Rentier.Desktop.Services;

/// <summary>What the release-notes dialog shows.</summary>
/// <param name="Subheader">Localized line under the title, e.g. "Changes from v1.3.1 to v1.5.0".</param>
/// <param name="Notes">One entry per version, newest first, already in the UI language.</param>
/// <param name="OfferUpdate">Whether to show an "Update Now" button next to Close.</param>
public sealed record ReleaseNotesDialogRequest(
    string Subheader,
    IReadOnlyList<ReleaseNoteDto> Notes,
    bool OfferUpdate);

/// <summary>Shows release notes in a modal window.</summary>
public interface IReleaseNotesDialogService
{
    /// <summary>
    /// Shows the dialog. Returns <c>true</c> when the user chose "Update Now", <c>false</c> when
    /// they closed it (or no window is available, e.g. in headless tests).
    /// </summary>
    Task<bool> ShowAsync(ReleaseNotesDialogRequest request);
}
