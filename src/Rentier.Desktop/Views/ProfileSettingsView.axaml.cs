using Avalonia.ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class ProfileSettingsView : ReactiveUserControl<ProfileSettingsViewModel>
{
    public ProfileSettingsView()
    {
        InitializeComponent();
    }
}
