namespace Rentier.Desktop.Services;

/// <summary>Opens a folder in the operating system's file manager.</summary>
public interface IFolderLauncher
{
    /// <summary>
    /// Opens <paramref name="directoryPath"/>, creating it first when it does not exist yet.
    /// Returns <c>false</c> when the folder could not be opened (no window, no file manager).
    /// </summary>
    Task<bool> OpenAsync(string directoryPath);
}
