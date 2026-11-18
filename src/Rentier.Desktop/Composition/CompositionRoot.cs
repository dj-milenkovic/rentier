using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Application.Services;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Composition;

/// <summary>
/// DI composition root for Desktop services.
/// Infrastructure services registered via AddInfrastructureServicesAsync in App.axaml.cs.
/// </summary>
public static class CompositionRoot
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        // The one clock for the whole app. Application handlers and Infrastructure services take
        // TimeProvider by constructor injection and never read DateTime.Now/UtcNow/Today directly.
        // TryAdd so a host (or a test) that registered its own clock first keeps it.
        services.TryAddSingleton(TimeProvider.System);

        // Theme service (Desktop-only UI concern — not an Application layer service)
        services.AddSingleton<IThemeService, ThemeService>();

        // T030: Localization service (singleton — same instance used by AXAML Localizer resource)
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // App version service (Desktop-only UI concern — reads the entry assembly's version)
        services.AddSingleton<IAppVersionService, AppVersionService>();

        // T030: UserPreference CQRS handlers
        services.AddTransient<
            IQueryHandler<GetUserPreferenceQuery, Result<string?, Error>>,
            GetUserPreferenceQueryHandler>();
        services.AddTransient<
            ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>,
            SetUserPreferenceCommandHandler>();

        // Application shared services

        // Application handlers
        services.AddTransient<
            ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>,
            SaveTaxpayerProfileCommandHandler>();
        services.AddTransient<
            IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>,
            GetTaxpayerProfileQueryHandler>();
        services.AddTransient<ISyncAllCommandHandler, SyncAllCommandHandler>();

        // Handlers that depend on Infrastructure services (sync and report processing)
        services.AddTransient<ISyncMailboxCommandHandler, SyncMailboxCommandHandler>();
        services.AddTransient<
            ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>,
            ProcessReportsCommandHandler>();

        // ViewModels — settings sub-ViewModels are singletons for in-session state persistence
        services.AddSingleton<ProfileSettingsViewModel>();
        services.AddSingleton<AppearanceSettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        // Holiday handlers
        services.AddTransient<
            ICommandHandler<EnsureHolidaysSeededCommand, Result<bool, Error>>,
            EnsureHolidaysSeededCommandHandler>();
        services.AddTransient<
            IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>,
            GetHolidayConfQueryHandler>();
        services.AddTransient<
            ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>,
            SaveHolidayConfCommandHandler>();
        services.AddTransient<
            ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>,
            FetchHolidaysFromWebCommandHandler>();
        services.AddSingleton<HolidaySettingsViewModel>();

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
        services.AddSingleton<MailboxSettingsViewModel>();

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
        services.AddTransient<ImporterSettingsHandlers>();
        services.AddSingleton<ImporterSettingsViewModel>();

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
        services.AddTransient<
            ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>,
            ExportFilingCommandHandler>();
        services.AddTransient<
            ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>,
            BulkDeleteFilingsCommandHandler>();
        services.AddTransient<FilingsHandlers>();

        // UI services ViewModels use instead of ad-hoc delegates: modal confirmation, native save
        // dialog, report import dialog, and cross-page navigation. NavigationService is one
        // instance behind both faces — pages raise requests, MainWindowViewModel performs them.
        services.AddSingleton<IConfirmationService, ConfirmationService>();
        services.AddSingleton<IFileSaveService, FileSaveService>();
        services.AddTransient<IReportImportDialogService, ReportImportDialogService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        services.AddSingleton<INavigationRequestSource>(sp => sp.GetRequiredService<NavigationService>());

        // Manual Filing handlers
        services.AddTransient<
            ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>,
            CalculateManualFilingCommandHandler>();
        services.AddTransient<
            ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>,
            CreateManualFilingCommandHandler>();

        // Dashboard handler
        services.AddTransient<
            IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>,
            GetDashboardQueryHandler>();

        // Reports handlers
        services.AddTransient<
            IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>,
            GetReportsQueryHandler>();
        services.AddTransient<
            ICommandHandler<ImportReportCommand, Result<Guid, Error>>,
            ImportReportCommandHandler>();
        services.AddTransient<
            ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>,
            DeleteReportCommandHandler>();
        services.AddTransient<
            ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>,
            BulkDeleteReportsCommandHandler>();
        services.AddTransient<ReportsHandlers>();

        return services;
    }
}
