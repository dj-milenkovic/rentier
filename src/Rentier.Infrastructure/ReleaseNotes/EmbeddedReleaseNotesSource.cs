using System.Reflection;
using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure.ReleaseNotes;

/// <summary>
/// Reads the release-notes document embedded into this assembly at build time
/// (the <c>RentierReleaseNotesPath</c> MSBuild property in Rentier.Infrastructure.csproj).
/// </summary>
public sealed class EmbeddedReleaseNotesSource : IInstalledReleaseNotesSource
{
    internal const string RESOURCE_NAME = "Rentier.ReleaseNotes.md";

    private readonly Assembly _assembly;
    private readonly string _resourceName;

    public EmbeddedReleaseNotesSource()
        : this(typeof(EmbeddedReleaseNotesSource).Assembly, RESOURCE_NAME) { }

    internal EmbeddedReleaseNotesSource(Assembly assembly, string resourceName)
    {
        _assembly = assembly;
        _resourceName = resourceName;
    }

    /// <inheritdoc />
    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = _assembly.GetManifestResourceStream(_resourceName);
        if (stream is null)
            return null;

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}
