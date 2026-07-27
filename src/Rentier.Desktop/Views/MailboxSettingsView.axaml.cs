using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Reactive;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class MailboxSettingsView : ReactiveUserControl<MailboxSettingsViewModel>
{
    public MailboxSettingsView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables) => { });
    }
}
