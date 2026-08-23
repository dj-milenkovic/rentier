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
    private const string SdkDefaultVersion = "1.0.0";

    public AppVersionService() : this(ResolveInformationalVersion()) { }

    internal AppVersionService(string? informationalVersion)
    {
        DisplayVersion = string.IsNullOrWhiteSpace(informationalVersion)
                          || informationalVersion == SdkDefaultVersion
            ? "dev"
            : $"v{informationalVersion}";
    }

    public string DisplayVersion { get; }

    [ExcludeFromCodeCoverage]
    private static string? ResolveInformationalVersion() =>
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
}
