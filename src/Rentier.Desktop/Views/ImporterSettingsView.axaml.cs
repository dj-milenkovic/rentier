using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Avalonia;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI;
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
