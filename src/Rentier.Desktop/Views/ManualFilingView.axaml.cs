using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Reactive;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

[ExcludeFromCodeCoverage]
public partial class ManualFilingView : ReactiveUserControl<ManualFilingViewModel>
{
    public ManualFilingView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable _) => { });
    }
}
