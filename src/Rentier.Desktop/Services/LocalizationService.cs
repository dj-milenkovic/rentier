using System.ComponentModel;
using System.Globalization;
using System.Reactive.Subjects;
using System.Reflection;
using Rentier.Desktop.Resources;

namespace Rentier.Desktop.Services;

public sealed class LocalizationService : ILocalizationService
{
    // Cached English strings built via reflection on the static Strings class
    private static readonly IReadOnlyDictionary<string, string> _englishStrings =
        typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .ToDictionary(p => p.Name, p => (string?)p.GetValue(null) ?? string.Empty);

    private readonly Subject<string> _cultureChanged = new();
    private string _currentCultureCode;

    public LocalizationService()
    {
        _currentCultureCode = "sr-Latn";
    }

    public string this[string key]
    {
        get
        {
            // Try current culture
            var currentDict = GetDictionary(_currentCultureCode);
            if (currentDict is not null && currentDict.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                return value;

            // Fallback to Serbian
            if (_currentCultureCode != "sr-Latn")
            {
                var srDict = GetDictionary("sr-Latn");
                if (srDict is not null && srDict.TryGetValue(key, out var srValue) && !string.IsNullOrEmpty(srValue))
                    return srValue;
            }

            // Fallback to English (neutral)
            if (_englishStrings.TryGetValue(key, out var enValue) && !string.IsNullOrEmpty(enValue))
                return enValue;

            return $"[{key}]";
        }
    }

    public string CurrentCultureCode => _currentCultureCode;

    public IObservable<string> CultureChanged => _cultureChanged;

    public void SetCulture(string cultureCode)
    {
        _currentCultureCode = cultureCode;
        _cultureChanged.OnNext(cultureCode);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static IReadOnlyDictionary<string, string>? GetDictionary(string cultureCode) =>
        cultureCode switch
        {
            "en" => _englishStrings,
            "sr-Latn" => SrLatnStrings.All,
            _ => null
        };
}

