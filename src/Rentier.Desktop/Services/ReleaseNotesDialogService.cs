using System.Diagnostics.CodeAnalysis;
using Rentier.Desktop.Dialogs;
using Rentier.Desktop.Resources;

namespace Rentier.Desktop.Services;

/// <summary>Shows <see cref="ReleaseNotesDialogHelper"/> with localized labels.</summary>
[ExcludeFromCodeCoverage(Justification = "Needs a real window.")]
internal sealed class ReleaseNotesDialogService(ILocalizationService localization) : IReleaseNotesDialogService
{
    public Task<bool> ShowAsync(ReleaseNotesDialogRequest request) =>
        ReleaseNotesDialogHelper.ShowAsync(
            request,
            new ReleaseNotesDialogLabels(
                Title: localization[nameof(Strings.ReleaseNotesDialog_Title)],
                UpdateNow: localization[nameof(Strings.Update_Now_Button)],
                Close: localization[nameof(Strings.ReleaseNotesDialog_Close)],
                OlderReleases: localization[nameof(Strings.WhatsNew_OlderReleases)]),
            ReleasesPage.Uri);
}
