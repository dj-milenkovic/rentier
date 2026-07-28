using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Avalonia.Reactive;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

[ExcludeFromCodeCoverage]
public partial class ManualFilingView : ReactiveUserControl<ManualFilingViewModel>
{
    public ManualFilingView()
    {
        InitializeComponent();
        this.ActivateViewModelOnLoad();
    }
}
