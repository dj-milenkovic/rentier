using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Reactive;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

[ExcludeFromCodeCoverage]
public partial class ImporterSettingsView : ReactiveUserControl<ImporterSettingsViewModel>
{
    public ImporterSettingsView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables) => { });
    }
}
