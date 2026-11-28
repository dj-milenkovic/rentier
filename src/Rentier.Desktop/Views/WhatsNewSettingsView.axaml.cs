using System.Diagnostics.CodeAnalysis;
using ReactiveUI.Avalonia;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

[ExcludeFromCodeCoverage]
public partial class WhatsNewSettingsView : ReactiveUserControl<WhatsNewSettingsViewModel>
{
    public WhatsNewSettingsView()
    {
        InitializeComponent();
        this.ActivateViewModelOnLoad();
    }
}
