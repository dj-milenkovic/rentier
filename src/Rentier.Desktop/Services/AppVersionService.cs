using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Rentier.Desktop.Services;

/// <summary>
/// Resolves the running app's display version from the entry assembly's
/// AssemblyInformationalVersion (set via -p:Version in release CI — see
/// .github/workflows/auto-release.yml). Local dev builds that don't pass
/// -p:Version fall back to the SDK default "1.0.0", which is treated as "dev"
/// rather than shown as a misleading release version.
/// </summary>
public sealed class AppVersionService : IAppVersionService
{
    private const string SDK_DEFAULT_VERSION = "1.0.0";

    public AppVersionService() : this(ResolveInformationalVersion()) { }

    internal AppVersionService(string? informationalVersion)
    {
        // The SDK's built-in SourceLink support appends "+<commit-sha>" to
        // AssemblyInformationalVersion; that build metadata is not part of the
        // version we want to show or compare against the SDK default.
        var version = informationalVersion;
        var plusIndex = version?.IndexOf('+') ?? -1;
        if (plusIndex >= 0)
        {
            version = version![..plusIndex];
        }

        DisplayVersion = string.IsNullOrWhiteSpace(version) || version == SDK_DEFAULT_VERSION
            ? "dev"
            : $"v{version}";
    }

    public string DisplayVersion { get; }

    [ExcludeFromCodeCoverage]
    private static string? ResolveInformationalVersion() =>
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
}
