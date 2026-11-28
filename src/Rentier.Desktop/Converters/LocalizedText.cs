using System.Reflection;
using Rentier.Desktop.Resources;
using Rentier.Desktop.Services;

namespace Rentier.Desktop.Converters;

/// <summary>
/// Resolves a string key against the app-wide <c>Localizer</c> resource — the same
/// <see cref="ILocalizationService"/> the AXAML <c>[Key]</c> bindings use — so converters show
/// text in the user's language. Without an application or localizer (plain unit tests) it falls
/// back to the neutral English value from <see cref="Strings"/>.
/// </summary>
internal static class LocalizedText
{
    public static string Get(string key)
    {
        var app = global::Avalonia.Application.Current;
        if (app is not null
            && app.Resources.TryGetValue("Localizer", out var resource)
            && resource is ILocalizationService localizer)
        {
            return localizer[key];
        }

        return typeof(Strings).GetProperty(key, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string
               ?? key;
    }
}
