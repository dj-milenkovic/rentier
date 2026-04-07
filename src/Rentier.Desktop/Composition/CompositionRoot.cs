using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Dialogs;
using Rentier.Desktop.Resources;
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

        // Filing handlers — registered in CompositionRoot (not InfrastructureServiceExtensions)
        services.AddTransient<
            IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>,
            GetFilingsQueryHandler>();
        services.AddTransient<
            ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>,
            UpdateFilingStatusCommandHandler>();
        services.AddTransient<
            ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>,
            UpdatePaymentReferenceCommandHandler>();
        services.AddTransient<
            ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>,
            DeleteFilingCommandHandler>();

        // Confirmation delegate for delete — must be explicitly registered so FilingsViewModel resolves
        services.AddTransient<Func<string, Task<bool>>>(provider => msg =>
            ConfirmDialogHelper.ShowAsync(
                Strings.Filings_Delete_Confirmation_Title,
                msg,
                Strings.Filings_Delete_Confirm_Button,
                Strings.Filings_Delete_Cancel_Button));

        return services;
    }
}
