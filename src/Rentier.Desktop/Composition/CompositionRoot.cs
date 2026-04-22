using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Services;
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
        // Application shared services
        services.AddTransient<ManualFilingCalculator>();

        // Application handlers
        services.AddTransient<
            ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>,
            SaveTaxpayerProfileCommandHandler>();
        services.AddTransient<
            IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>,
            GetTaxpayerProfileQueryHandler>();
        services.AddTransient<ISyncAllCommandHandler, SyncAllCommandHandler>();

        // Handlers that depend on Infrastructure services (sync and report processing)
        services.AddTransient<
            ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>,
            SyncMailboxCommandHandler>();
        services.AddTransient<
            ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>,
            ProcessReportsCommandHandler>();

        // ViewModels
        services.AddTransient<ProfileSettingsViewModel>();
        services.AddTransient<SettingsViewModel>();
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
            ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>,
            ImportHolidaysFromWebCommandHandler>();
        services.AddTransient<
            ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>,
            FetchHolidaysFromWebCommandHandler>();
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
        services.AddTransient<
            ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>,
            ExportFilingCommandHandler>();
        services.AddTransient<
            ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>,
            BulkDeleteFilingsCommandHandler>();

        // Confirmation delegate for delete — must be explicitly registered so FilingsViewModel resolves
        services.AddTransient<Func<string, Task<bool>>>(provider => msg =>
            ConfirmDialogHelper.ShowAsync(
                Strings.Filings_Delete_Confirmation_Title,
                msg,
                Strings.Filings_Delete_Confirm_Button,
                Strings.Filings_Delete_Cancel_Button));

        // Export file-writer delegate — opens native save dialog and writes bytes to disk
        services.AddTransient<Func<ExportFilingResult, Task>>(provider =>
            async (ExportFilingResult result) =>
            {
            var lifetime = Avalonia.Application.Current?.ApplicationLifetime
                as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(lifetime?.MainWindow);
            if (topLevel is null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = Strings.Filings_Export_SaveDialog_Title,
                    SuggestedFileName = result.SuggestedFileName,
                    FileTypeChoices =
                    [
                        new Avalonia.Platform.Storage.FilePickerFileType(
                            Strings.Filings_Export_Filter_Xml) { Patterns = ["*.xml"] }
                    ]
                });

            if (file is null) return;

            await using var stream = await file.OpenWriteAsync();
            try
            {
                await stream.WriteAsync(result.Bytes);
            }
            catch
            {
                await file.DeleteAsync();
                throw;
            }
        });

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

        // 2-arg confirmation delegate for report delete (distinct type from 1-arg Func<string,Task<bool>>)
        services.AddTransient<Func<string, string, Task<bool>>>(provider => (title, msg) =>
            ConfirmDialogHelper.ShowAsync(
                title,
                msg,
                Strings.Reports_Delete_Confirm_Button,
                Strings.Reports_Delete_Cancel_Button));

        // CSV file picker + importer selection delegate for report import
        services.AddTransient<Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>>>(
            provider => async () =>
            {
                var getImporters = provider.GetRequiredService<
                    IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>>();
                var importersResult = await getImporters.HandleAsync(new GetImportersQuery());
                IReadOnlyList<ImporterDto> importers = importersResult.IsSuccess
                    ? importersResult.Value
                    : [];
                return await ImportDialogHelper.ShowAsync(importers);
            });

        return services;
    }
}
