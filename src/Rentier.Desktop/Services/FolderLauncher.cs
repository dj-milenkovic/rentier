using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;

namespace Rentier.Desktop.Services;

/// <summary>
/// Opens a folder through Avalonia's launcher (Explorer on Windows, <c>open</c> on macOS,
/// <c>xdg-open</c> on Linux).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Needs a real window and the OS shell.")]
internal sealed class FolderLauncher(ILogger<FolderLauncher> logger) : IFolderLauncher
{
    public async Task<bool> OpenAsync(string directoryPath)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);

            var lifetime = Avalonia.Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime;
            var topLevel = TopLevel.GetTopLevel(lifetime?.MainWindow);
            if (topLevel is null)
                return false;

            return await topLevel.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(directoryPath));
        }
        // Shell launch failures (no file manager, blocked by policy) vary by platform; the caller
        // shows the path so the user can open it by hand.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                       or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not open the log folder");
            return false;
        }
    }
}
