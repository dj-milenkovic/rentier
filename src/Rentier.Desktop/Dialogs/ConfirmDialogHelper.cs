using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Rentier.Desktop.Dialogs;

/// <summary>
/// Lightweight confirmation dialog built entirely in code (no AXAML dependency).
/// Used by the delete confirmation flow in FilingsViewModel.
/// </summary>
internal static class ConfirmDialogHelper
{
    /// <summary>
    /// Shows a modal confirmation dialog and returns <c>true</c> if the user confirms.
    /// Falls back to <c>false</c> when no owner window is available (e.g., headless tests).
    /// </summary>
    public static async Task<bool> ShowAsync(
        string title, string message, string confirmText, string cancelText)
    {
        var owner = (Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null)
            return false;

        var confirmButton = new Button { Content = confirmText, Margin = new Thickness(0, 0, 4, 0), IsDefault = true };
        var cancelButton  = new Button { Content = cancelText, IsCancel = true };

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { confirmButton, cancelButton }
                    }
                }
            }
        };

        confirmButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click  += (_, _) => dialog.Close(false);

        return await dialog.ShowDialog<bool>(owner);
    }
}
