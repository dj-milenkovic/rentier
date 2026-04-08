using Avalonia.ReactiveUI;
using ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class MailboxSettingsView : ReactiveUserControl<MailboxSettingsViewModel>
{
    public MailboxSettingsView()
    {
        InitializeComponent();
        this.WhenActivated(disposables => { });
    }
}
