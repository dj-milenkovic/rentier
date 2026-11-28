namespace Rentier.Application.Queries;

/// <summary>
/// The release notes built into the running copy: its own version and the ones before it,
/// newest first, in the language of <paramref name="CultureCode"/>.
/// </summary>
public sealed record GetInstalledReleaseNotesQuery(string CultureCode);
