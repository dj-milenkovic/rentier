namespace Rentier.Application.DTOs;

/// <summary>
/// Represents the current state of the auto-update workflow.
/// </summary>
public enum UpdateState
{
    /// <summary>No update activity. Initial state on app launch.</summary>
    Idle,

    /// <summary>Currently checking the update server for a newer version.</summary>
    Checking,

    /// <summary>A newer version is available and the user has been notified.</summary>
    UpdateAvailable,

    /// <summary>The update package is being downloaded.</summary>
    Downloading,

    /// <summary>The update package has been downloaded and is ready to apply.</summary>
    Downloaded,

    /// <summary>An error occurred during the update check or download.</summary>
    Error,

    /// <summary>The user dismissed the update notification for this session.</summary>
    Dismissed,
}
