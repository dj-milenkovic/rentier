using System.Diagnostics.CodeAnalysis;
using Rentier.Desktop.Dialogs;

namespace Rentier.Desktop.Services;

/// <summary>Shows the shared modal confirmation dialog owned by the main window.</summary>
[ExcludeFromCodeCoverage]
internal sealed class ConfirmationService : IConfirmationService
{
    public Task<bool> ConfirmAsync(ConfirmationRequest request) =>
        ConfirmDialogHelper.ShowAsync(
            request.Title, request.Message, request.ConfirmText, request.CancelText);
}
