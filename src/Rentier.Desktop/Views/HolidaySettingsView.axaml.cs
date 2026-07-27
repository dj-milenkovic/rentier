using ReactiveUI.Avalonia;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class HolidaySettingsView : ReactiveUserControl<HolidaySettingsViewModel>
{
    public HolidaySettingsView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables) => { });
    }
}
