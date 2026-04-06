using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Desktop.Composition;
using Rentier.Desktop.ViewModels;
using Rentier.Desktop.Views;

namespace Rentier.Desktop;

/// <summary>
/// DI composition root. Infrastructure services (ICredentialStore) will be
/// registered here when the IMAP mailbox feature is implemented.
/// </summary>
public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddDesktopServices();
            var provider = services.BuildServiceProvider();

            var mainVm = provider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow(mainVm);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
