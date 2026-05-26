using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Rentier.Application.DTOs;
using Rentier.Desktop.Resources;

namespace Rentier.Desktop.Dialogs;

/// <summary>
/// Shows a file picker for CSV selection followed by an importer selection dialog.
/// Returns the chosen tuple, or null if the user cancels at any step.
/// </summary>
internal static class ImportDialogHelper
{
    public static async Task<(Guid ImporterId, string FileName, byte[] Content)?> ShowAsync(
        IReadOnlyList<ImporterDto> importers)
    {
        var owner = (Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null)
            return null;

        // Step 1: pick a CSV file
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel is null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Reports_Import_FilePickerTitle,
            FileTypeFilter =
            [
                new FilePickerFileType(Strings.Reports_Import_FilePickerFilter)
                {
                    Patterns = ["*.csv"]
                }
            ],
            AllowMultiple = false
        });

        if (files.Count == 0)
            return null;

        var file = files[0];
        byte[] content;
        await using (var stream = await file.OpenReadAsync())
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            content = ms.ToArray();
        }

        // Step 2: show importer selection if multiple importers exist
        if (importers.Count == 0)
        {
            await ShowInfoDialogAsync(owner, Strings.Reports_Import_Title,
                Strings.Reports_Import_NoImporters);
            return null;
        }

        Guid selectedImporterId;
        if (importers.Count == 1)
        {
            // Auto-select the only importer
            selectedImporterId = importers[0].Id;
        }
        else
        {
            // Show importer picker dialog
            var picked = await ShowImporterPickerAsync(owner, importers);
            if (picked is null)
                return null;
            selectedImporterId = picked.Value;
        }

        return (selectedImporterId, file.Name, content);
    }

    private static async Task ShowInfoDialogAsync(Window owner, string title, string message)
    {
        var okButton = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
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
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    okButton
                }
            }
        };
        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    private static async Task<Guid?> ShowImporterPickerAsync(
        Window owner, IReadOnlyList<ImporterDto> importers)
    {
        var comboBox = new ComboBox
        {
            ItemsSource = importers,
            DisplayMemberBinding = new Avalonia.Data.Binding(nameof(ImporterDto.DisplayName)),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var confirmButton = new Button { Content = Strings.ImportDialog_Import_Button, Margin = new Thickness(0, 0, 4, 0), IsDefault = true };
        var cancelButton = new Button { Content = "Cancel", IsCancel = true };

        var dialog = new Window
        {
            Title = Strings.Reports_Import_Title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = Strings.ImportDialog_SelectImporter },
                    comboBox,
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
        cancelButton.Click += (_, _) => dialog.Close(false);

        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (!confirmed || comboBox.SelectedItem is not ImporterDto selected)
            return null;

        return selected.Id;
    }
}
