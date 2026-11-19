namespace Rentier.Application.Common;

/// <summary>Keys of the key/value user preferences persisted through <c>SetUserPreferenceCommand</c>.</summary>
public static class UserPreferenceKeys
{
    public const string LANGUAGE = "Language";

    /// <summary>"true" when the user turned on Debug-level diagnostic logging.</summary>
    public const string DEBUG_LOGGING = "DebugLogging";
}
