using Avalonia.ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
{
    public SettingsView()
    {
        InitializeComponent();
    }
}
