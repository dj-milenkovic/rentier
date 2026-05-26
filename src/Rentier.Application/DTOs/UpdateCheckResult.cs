namespace Rentier.Application.DTOs;

/// <summary>
/// Result of an update availability check.
/// </summary>
/// <param name="IsUpdateAvailable">Whether a newer version is available.</param>
/// <param name="TargetVersion">The version string of the available update, or null if no update is available.</param>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string? TargetVersion);
