using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Avalonia;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

[ExcludeFromCodeCoverage]
public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
        this.ActivateViewModelOnLoad();
    }

    public MainWindow(MainWindowViewModel vm) : this()
    {
        DataContext = vm;
    }
}

