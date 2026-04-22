using System.Reactive.Linq;
using ReactiveUI;
using Rentier.Desktop.Services;

namespace Rentier.Desktop.ViewModels;

public sealed class AppearanceSettingsViewModel : ReactiveObject
{
    private readonly IThemeService _themeService;
    private ThemePreference _selectedTheme;

    public AppearanceSettingsViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        _selectedTheme = themeService.GetPreference();

        // Auto-apply whenever the selection changes (no separate Save button needed).
        this.WhenAnyValue(x => x.SelectedTheme)
            .Skip(1)
            .Subscribe(p => _themeService.SetPreference(p));
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
}
