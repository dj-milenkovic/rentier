using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Reactive;
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
