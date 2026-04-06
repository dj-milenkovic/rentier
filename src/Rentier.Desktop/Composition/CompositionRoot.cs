using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Composition;

/// <summary>
/// DI composition root for Desktop services.
/// Infrastructure services registered via AddInfrastructureServices in App.axaml.cs.
/// </summary>
public static class CompositionRoot
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        // Application handlers
        services.AddTransient<
            ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>,
            SaveTaxpayerProfileCommandHandler>();
        services.AddTransient<
            IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>,
            GetTaxpayerProfileQueryHandler>();

        // ViewModels
        services.AddTransient<ProfileSettingsViewModel>();
        services.AddTransient<FilingsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        return services;
    }
}
