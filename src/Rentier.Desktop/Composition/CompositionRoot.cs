using Microsoft.Extensions.DependencyInjection;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Composition;

/// <summary>
/// DI composition root for Desktop services.
/// Architecture note: Desktop references Application + Domain only per plan.md FR-003.
/// Infrastructure services (ICredentialStore) registered when the IMAP feature
/// introduces the Desktop → Infrastructure DI composition extension.
/// </summary>
public static class CompositionRoot
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        services.AddTransient<FilingsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        return services;
    }
}
