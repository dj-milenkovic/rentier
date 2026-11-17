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

        try
        {
            // Scoped so the stream is closed before the cleanup below: deleting a file that is
            // still open fails on Windows and would mask the original write error.
            await using (var stream = await file.OpenWriteAsync())
            {
                await stream.WriteAsync(request.Content);
            }
        }
        catch
        {
            // Never leave a partial export behind.
            await file.DeleteAsync();
            throw;
        }

        return true;
    }
}
