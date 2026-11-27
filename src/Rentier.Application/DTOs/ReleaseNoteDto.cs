namespace Rentier.Application.DTOs;

/// <summary>One version's release notes in one language.</summary>
/// <param name="Version">The version as shown to users, e.g. <c>v1.4.0</c>.</param>
/// <param name="Markdown">The notes as Markdown.</param>
public sealed record ReleaseNoteDto(string Version, string Markdown);
