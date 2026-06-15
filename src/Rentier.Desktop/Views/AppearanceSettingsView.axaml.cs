using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Avalonia;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

[ExcludeFromCodeCoverage]
public partial class AppearanceSettingsView : ReactiveUserControl<AppearanceSettingsViewModel>
{
    public AppearanceSettingsView()
    {
        InitializeComponent();
    }
}
