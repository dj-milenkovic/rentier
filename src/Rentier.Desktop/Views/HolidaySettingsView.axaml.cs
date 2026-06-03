using ReactiveUI.Avalonia;
using ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class HolidaySettingsView : ReactiveUserControl<HolidaySettingsViewModel>
{
    public HolidaySettingsView()
    {
        InitializeComponent();
        this.WhenActivated(disposables => { });
    }
}
