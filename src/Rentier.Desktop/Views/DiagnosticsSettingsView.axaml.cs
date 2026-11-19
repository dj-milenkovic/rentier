using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Avalonia;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

[ExcludeFromCodeCoverage]
public partial class DiagnosticsSettingsView : ReactiveUserControl<DiagnosticsSettingsViewModel>
{
    public DiagnosticsSettingsView()
    {
        InitializeComponent();
    }
}
