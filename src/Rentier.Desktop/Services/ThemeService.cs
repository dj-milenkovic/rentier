using System.Text.Json;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Rentier.Desktop.Services;

/// <summary>
/// Reads/writes the theme preference from <c>%LOCALAPPDATA%\Rentier\ui.json</c>
/// and applies it via <see cref="Avalonia.Application.RequestedThemeVariant"/>.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Rentier", "ui.json");

    private ThemePreference _preference;

    public ThemeService()
    {
        _preference = LoadFromFile();
    }

    public ThemePreference GetPreference() => _preference;

    public void SetPreference(ThemePreference preference)
    {
        _preference = preference;
        SaveToFile(preference);
        ApplyVariant(preference);
    }

    /// <summary>Applies the saved preference — call once on startup before the window shows.</summary>
    public static void ApplyOnStartup(ThemePreference preference) => ApplyVariant(preference);

    private static void ApplyVariant(ThemePreference preference)
    {
        var variant = preference switch
        {
            ThemePreference.Dark => ThemeVariant.Dark,
            ThemePreference.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };

        // Always marshal to UI thread; this may be called from a background context.
        if (Dispatcher.UIThread.CheckAccess())
            Apply(variant);
        else
            Dispatcher.UIThread.Invoke(() => Apply(variant));

        static void Apply(ThemeVariant v)
        {
            if (Avalonia.Application.Current is { } app)
                app.RequestedThemeVariant = v;
        }
    }

    private static ThemePreference LoadFromFile()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return ThemePreference.System;

            var json = File.ReadAllText(SettingsPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("theme", out var prop) &&
                Enum.TryParse<ThemePreference>(prop.GetString(), out var parsed))
                return parsed;
        }
        catch { /* fall through to default */ }

        return ThemePreference.System;
    }

    private static void SaveToFile(ThemePreference preference)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(new { theme = preference.ToString() });
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* ignore transient I/O failures */ }
    }
}
