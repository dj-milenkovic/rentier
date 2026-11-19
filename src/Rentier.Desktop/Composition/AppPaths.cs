namespace Rentier.Desktop.Composition;

/// <summary>
/// Per-user data locations, defined once so the app, the smoke test and logging agree.
/// Everything lives under <c>%LOCALAPPDATA%\Rentier</c> (or the platform equivalent).
/// </summary>
internal static class AppPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rentier");

    public static string DatabasePath { get; } = Path.Combine(RootDirectory, "rentier.db");

    /// <summary>Isolated database for <c>--smoke-test</c>, so it never touches a real user database.</summary>
    public static string SmokeDatabasePath { get; } = Path.Combine(RootDirectory, "rentier-smoke.db");

    public static string LogDirectory { get; } = Path.Combine(RootDirectory, "logs");
}
