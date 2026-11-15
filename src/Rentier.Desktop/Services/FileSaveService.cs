using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Rentier.Desktop.Services;

/// <summary>Opens the native save dialog on the main window and writes the bytes to disk.</summary>
[ExcludeFromCodeCoverage]
internal sealed class FileSaveService : IFileSaveService
{
    public async Task<bool> SaveAsync(FileSaveRequest request)
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime;
        var topLevel = TopLevel.GetTopLevel(lifetime?.MainWindow);
        if (topLevel is null) return false;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = request.Title,
                SuggestedFileName = request.SuggestedFileName,
                FileTypeChoices =
                [
                    new FilePickerFileType(request.FileTypeName) { Patterns = request.Patterns }
                ]
            });

        if (file is null) return false;

        await using var stream = await file.OpenWriteAsync();
        try
        {
            await stream.WriteAsync(request.Content);
        }
        catch
        {
            await file.DeleteAsync();
            throw;
        }

        return true;
    }
}
