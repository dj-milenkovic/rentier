using ReactiveUI.Avalonia.Reactive;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class HolidaySettingsView : ReactiveUserControl<HolidaySettingsViewModel>
{
    public HolidaySettingsView()
    {
        InitializeComponent();
        this.ActivateViewModelOnLoad();
    }
}
