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

        // Holiday handlers
        services.AddTransient<
            IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>,
            GetHolidayConfQueryHandler>();
        services.AddTransient<
            ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>,
            SaveHolidayConfCommandHandler>();
        services.AddTransient<
            ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>,
            ImportHolidaysFromWebCommandHandler>();
        services.AddTransient<HolidaySettingsViewModel>();

        // Mailbox handlers
        services.AddTransient<
            IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>,
            GetMailboxesQueryHandler>();
        services.AddTransient<
            ICommandHandler<AddMailboxCommand, Result<Guid, Error>>,
            AddMailboxCommandHandler>();
        services.AddTransient<
            ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>,
            UpdateMailboxCommandHandler>();
        services.AddTransient<
            ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>,
            DeleteMailboxCommandHandler>();
        services.AddTransient<MailboxSettingsViewModel>();

        // Importer handlers
        services.AddTransient<
            IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>,
            GetImportersQueryHandler>();
        services.AddTransient<
            ICommandHandler<AddImporterCommand, Result<Guid, Error>>,
            AddImporterCommandHandler>();
        services.AddTransient<
            ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>,
            UpdateImporterCommandHandler>();
        services.AddTransient<
            ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>,
            DeleteImporterCommandHandler>();
        services.AddTransient<ImporterSettingsViewModel>();

        return services;
    }
}
