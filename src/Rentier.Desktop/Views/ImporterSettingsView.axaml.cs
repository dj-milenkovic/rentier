using Avalonia.ReactiveUI;
using ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class ImporterSettingsView : ReactiveUserControl<ImporterSettingsViewModel>
{
    public ImporterSettingsView()
    {
        InitializeComponent();
        this.WhenActivated(disposables => { });
    }
}
