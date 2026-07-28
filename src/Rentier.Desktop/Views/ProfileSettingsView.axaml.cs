using ReactiveUI.Avalonia.Reactive;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class ProfileSettingsView : ReactiveUserControl<ProfileSettingsViewModel>
{
    public ProfileSettingsView()
    {
        InitializeComponent();
        this.ActivateViewModelOnLoad();
    }
}
