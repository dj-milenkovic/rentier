namespace Rentier.Application.DTOs;

/// <summary>
/// Carries download progress information for an in-progress update download.
/// </summary>
/// <param name="ProgressPercent">Download progress as an integer from 0 to 100.</param>
public sealed record DownloadProgressInfo(int ProgressPercent);
