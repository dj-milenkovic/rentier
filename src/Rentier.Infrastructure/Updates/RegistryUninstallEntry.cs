using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Rentier.Infrastructure.Updates;

/// <summary>The installer's uninstall key in the current user's registry hive.</summary>
[SupportedOSPlatform("windows")]
[ExcludeFromCodeCoverage(Justification = "Thin wrapper over the Windows registry.")]
internal sealed class RegistryUninstallEntry : IUninstallEntry
{
    public string? GetString(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(InstalledAppsEntrySync.UNINSTALL_KEY_PATH);
        return key?.GetValue(name) as string;
    }

    public void SetString(string name, string value)
    {
        using var key = Registry.CurrentUser.OpenSubKey(InstalledAppsEntrySync.UNINSTALL_KEY_PATH, writable: true)
            ?? throw new InvalidOperationException("The installer's uninstall key does not exist.");
        key.SetValue(name, value, RegistryValueKind.String);
    }
}
