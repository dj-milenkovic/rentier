using Avalonia.ReactiveUI;
using ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class ManualFilingView : ReactiveUserControl<ManualFilingViewModel>
{
    public ManualFilingView()
    {
        InitializeComponent();
        this.WhenActivated(_ => { });
    }
}
