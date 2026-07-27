using System.Reactive.Linq;
using ReactiveUI.Reactive;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Desktop.Services;

namespace Rentier.Desktop.ViewModels;

public sealed class AppearanceSettingsViewModel : ReactiveObject
{
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private ThemePreference _selectedTheme;
    private string _selectedLanguage;

    public static IReadOnlyList<(string Code, string DisplayName)> LanguageOptions { get; } =
    [
        ("en", "English"),
        ("sr-Latn", "Srpski"),
    ];

    public AppearanceSettingsViewModel(
        IThemeService themeService,
        ILocalizationService localizationService,
        ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>> setPreferenceHandler)
    {
        _themeService = themeService;
        _localizationService = localizationService;
        _selectedTheme = themeService.GetPreference();
        _selectedLanguage = localizationService.CurrentCultureCode;

        // Auto-apply theme whenever the selection changes (no separate Save button needed).
        this.WhenAnyValue(x => x.SelectedTheme)
            .Skip(1)
            .Subscribe(p => _themeService.SetPreference(p));

        // T031: When language changes, call SetCulture and persist to DB
        this.WhenAnyValue(x => x.SelectedLanguage)
            .Skip(1)
            .DistinctUntilChanged()
            .Subscribe(code =>
            {
                _localizationService.SetCulture(code);
                // Fire-and-forget persistence
                _ = setPreferenceHandler.HandleAsync(new SetUserPreferenceCommand("Language", code));
            });
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set => this.RaiseAndSetIfChanged(ref _selectedLanguage, value);
    }

    public ThemePreference SelectedTheme
    {
        get => _selectedTheme;
        set => this.RaiseAndSetIfChanged(ref _selectedTheme, value);
    }

    public bool IsSystem
    {
        get => SelectedTheme == ThemePreference.System;
        set { if (value) SelectedTheme = ThemePreference.System; }
    }

    public bool IsLight
    {
        get => SelectedTheme == ThemePreference.Light;
        set { if (value) SelectedTheme = ThemePreference.Light; }
    }

    public bool IsDark
    {
        get => SelectedTheme == ThemePreference.Dark;
        set { if (value) SelectedTheme = ThemePreference.Dark; }
    }

    public bool IsEnglish
    {
        get => SelectedLanguage == "en";
        set { if (value) SelectedLanguage = "en"; }
    }

    public bool IsSrLatn
    {
        get => SelectedLanguage == "sr-Latn";
        set { if (value) SelectedLanguage = "sr-Latn"; }
    }
}
