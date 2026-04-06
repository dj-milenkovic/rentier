using Avalonia.ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class FilingsView : ReactiveUserControl<FilingsViewModel>
{
    public FilingsView()
    {
        InitializeComponent();
    }
}
