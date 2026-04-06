using ReactiveUI;

namespace Rentier.Desktop.ViewModels;

public sealed class SettingsViewModel : ReactiveObject
{
    public ProfileSettingsViewModel ProfileTab { get; }

    public SettingsViewModel(ProfileSettingsViewModel profileTab)
    {
        ProfileTab = profileTab;
    }
}
