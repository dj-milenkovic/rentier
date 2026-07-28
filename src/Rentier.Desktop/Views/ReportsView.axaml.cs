using ReactiveUI.Avalonia.Reactive;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class ReportsView : ReactiveUserControl<ReportsViewModel>
{
    public ReportsView()
    {
        InitializeComponent();
        this.ActivateViewModelOnLoad();
    }
}
