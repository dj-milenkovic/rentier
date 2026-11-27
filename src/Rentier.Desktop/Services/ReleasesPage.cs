using System.Reflection;

namespace Rentier.Desktop.Services;

/// <summary>
/// The GitHub Releases page, linked for versions older than the built-in release notes cover.
/// Read from the <c>ReleasesPageUrl</c> assembly metadata (the <c>RentierReleasesPageUrl</c>
/// MSBuild property in Rentier.Desktop.csproj).
/// </summary>
public static class ReleasesPage
{
    /// <summary>The page's address, or null when the build carries none.</summary>
    public static Uri? Uri { get; } = Resolve(
        typeof(ReleasesPage).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "ReleasesPageUrl")?.Value);

    internal static Uri? Resolve(string? url) =>
        System.Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == System.Uri.UriSchemeHttps
            ? uri
            : null;
}
