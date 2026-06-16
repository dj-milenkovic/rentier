namespace Rentier.Tests.Common;

/// <summary>
/// Test environment detection and configuration.
/// Allows same test binaries to run in CI and locally with appropriate behavior.
/// </summary>
public static class TestEnvironment
{
    /// <summary>
    /// Detects if running in a CI environment (GitHub Actions, etc.).
    /// CI sets the CI environment variable to "true".
    /// </summary>
    public static bool IsCi =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_WORKFLOW"));

    /// <summary>
    /// Detects if running locally (not in CI).
    /// </summary>
    public static bool IsLocal => !IsCi;

    /// <summary>
    /// Returns recommended max parallel threads based on environment.
    /// CI limits to 4 to avoid runner resource exhaustion.
    /// Local uses all available processors for faster feedback.
    /// </summary>
    public static int MaxParallelThreads
    {
        get
        {
            if (IsCi) return 4;  // Respect CI runner limits
            return Environment.ProcessorCount;
        }
    }

    /// <summary>
    /// Returns the database connection string for testing.
    /// CI uses in-memory SQLite (shared cache) for isolation and speed.
    /// Local uses a file-based SQLite database for inspection/debugging.
    /// </summary>
    public static string GetDatabaseConnectionString()
    {
        return IsCi
            ? "Data Source=:memory:;Cache=Shared"
            : $"Data Source={Path.Combine(Path.GetTempPath(), "rentier-test.db")}";
    }

    /// <summary>
    /// Returns descriptive environment string for logging.
    /// </summary>
    public static string GetEnvironmentDescription()
    {
        return IsCi
            ? $"CI (GitHub Actions, maxThreads={MaxParallelThreads})"
            : $"Local (maxThreads={MaxParallelThreads})";
    }
}
