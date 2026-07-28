using ReactiveUI.Avalonia;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class DashboardView : ReactiveUserControl<DashboardViewModel>
{
    public DashboardView()
    {
        InitializeComponent();
        this.ActivateViewModelOnLoad();
    }
}

