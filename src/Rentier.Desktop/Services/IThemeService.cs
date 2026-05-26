namespace Rentier.Desktop.Services;

public enum ThemePreference { System, Light, Dark }

/// <summary>
/// Persists and applies the user's preferred Avalonia theme variant.
/// Lives in Desktop — purely a UI concern, not part of the Application or Domain.
/// </summary>
public interface IThemeService
{
    ThemePreference GetPreference();

    /// <summary>Persists the preference and applies it immediately on the UI thread.</summary>
    void SetPreference(ThemePreference preference);
}
