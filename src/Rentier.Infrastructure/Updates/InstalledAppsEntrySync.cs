using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Rentier.Application.Common;
using Rentier.Infrastructure.Logging;
using Velopack.Locators;

namespace Rentier.Infrastructure.Updates;

/// <summary>
/// Keeps the Windows "Installed apps" entry that the installer wrote in step with the version
/// the app has updated itself to. The installer registers its own version once; in-app updates
/// replace the program files but never touch that entry, so without this the Apps list would
/// keep showing the version that was first installed.
/// </summary>
public sealed class InstalledAppsEntrySync
{
    /// <summary>
    /// The installer's per-user uninstall key (under HKCU), named after its AppId. A contract test
    /// checks that the AppId here matches <c>rentier-setup.iss</c>.
    /// </summary>
    public const string UNINSTALL_KEY_PATH =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{F7A3B2C1-D4E5-4F6A-8B9C-0D1E2F3A4B5C}_is1";

    internal const string INSTALL_LOCATION = "InstallLocation";
    internal const string DISPLAY_VERSION = "DisplayVersion";
    internal const string DISPLAY_NAME = "DisplayName";

    /// <summary>Matches <c>UninstallDisplayName</c> in <c>rentier-setup.iss</c>.</summary>
    public const string APP_DISPLAY_NAME = "Rentier";

    private static readonly Error SyncFailed =
        Error.Infrastructure("The installed apps entry could not be updated.");

    private readonly IUninstallEntry _entry;
    private readonly ILogger<InstalledAppsEntrySync> _logger;

    internal InstalledAppsEntrySync(IUninstallEntry entry, ILogger<InstalledAppsEntrySync> logger)
    {
        _entry = entry;
        _logger = logger;
    }

    /// <summary>
    /// Updates the entry for this running copy, if there is one. Does nothing (returns
    /// <c>false</c>) off Windows, outside a Velopack install, or for a copy the installer did not
    /// install (the portable ZIP, or a different folder than the registered one).
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Reads the real Velopack install and the real registry; the logic is covered through Sync.")]
    public static Result<bool, Error> SyncThisInstall(ILogger<InstalledAppsEntrySync> logger)
    {
        if (!OperatingSystem.IsWindows() || !VelopackLocator.IsCurrentSet)
            return Result<bool, Error>.Success(false);

        var locator = VelopackLocator.Current;
        var version = locator.CurrentlyInstalledVersion?.ToString();
        var rootAppDir = locator.RootAppDir;
        if (version is null || string.IsNullOrEmpty(rootAppDir))
            return Result<bool, Error>.Success(false);

        return new InstalledAppsEntrySync(new RegistryUninstallEntry(), logger).Sync(rootAppDir, version);
    }

    /// <summary>
    /// Writes <paramref name="installedVersion"/> into the entry when it is registered for
    /// <paramref name="rootAppDir"/>. Returns <c>true</c> when something was written.
    /// </summary>
    internal Result<bool, Error> Sync(string rootAppDir, string installedVersion)
    {
        try
        {
            var installLocation = _entry.GetString(INSTALL_LOCATION);
            if (installLocation is null || !SameFolder(installLocation, rootAppDir))
                return Result<bool, Error>.Success(false);

            var changed = false;
            if (_entry.GetString(DISPLAY_VERSION) != installedVersion)
            {
                _entry.SetString(DISPLAY_VERSION, installedVersion);
                changed = true;
            }

            // Installers up to v1.3.1 put the version into the name too ("Rentier 1.3.1").
            if (_entry.GetString(DISPLAY_NAME) != APP_DISPLAY_NAME)
            {
                _entry.SetString(DISPLAY_NAME, APP_DISPLAY_NAME);
                changed = true;
            }

            if (changed)
                _logger.InstalledAppsEntryUpdated(installedVersion);
            return Result<bool, Error>.Success(changed);
        }
        // Infrastructure boundary: a locked or unreadable key must never stop the app from
        // starting; the Apps list just keeps its old version.
        catch (Exception ex)
        {
            _logger.InstalledAppsEntryUpdateFailed(ex);
            return Result<bool, Error>.Failure(SyncFailed);
        }
    }

    private static bool SameFolder(string a, string b)
        => string.Equals(TrimSeparators(a), TrimSeparators(b), StringComparison.OrdinalIgnoreCase);

    private static string TrimSeparators(string path) => path.Trim().TrimEnd('\\', '/');
}

/// <summary>The string values of the installer's uninstall key; seam for tests.</summary>
internal interface IUninstallEntry
{
    /// <summary>The value, or <c>null</c> when the value or the whole key does not exist.</summary>
    string? GetString(string name);

    void SetString(string name, string value);
}
