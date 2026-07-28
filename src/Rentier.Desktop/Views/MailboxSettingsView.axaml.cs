using ReactiveUI.Avalonia;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class MailboxSettingsView : ReactiveUserControl<MailboxSettingsViewModel>
{
    public MailboxSettingsView()
    {
        InitializeComponent();
        this.ActivateViewModelOnLoad();
    }
}

