namespace Rentier.Desktop.Services;

/// <summary>Localized texts for a modal yes/no confirmation.</summary>
public sealed record ConfirmationRequest(string Title, string Message, string ConfirmText, string CancelText);

/// <summary>
/// Asks the user to confirm an action. ViewModels depend on this abstraction instead of
/// ad-hoc delegates or the static dialog helpers, so they stay free of Avalonia windows.
/// </summary>
public interface IConfirmationService
{
    /// <summary>Returns <c>true</c> when the user confirms; <c>false</c> when they cancel or dismiss.</summary>
    Task<bool> ConfirmAsync(ConfirmationRequest request);
}
