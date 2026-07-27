using ReactiveUI.Avalonia.Reactive;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class ProfileSettingsView : ReactiveUserControl<ProfileSettingsViewModel>
{
    public ProfileSettingsView()
    {
        InitializeComponent();
    }
}
