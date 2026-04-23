using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests;

public class MainWindowViewModelTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // Dashboard
        services.AddTransient(_ =>
            Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>());

        // Filings
        var getFilings = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(new FilingsPageResult([], 0, 1)));
        services.AddTransient(_ => getFilings);

        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>>());
        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>>());
        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>>());
        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>>());
        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>>());
        services.AddTransient<Func<string, Task<bool>>>(_ => _ => Task.FromResult(false));
        services.AddTransient<Func<ExportFilingResult, Task>>(_ => _ => Task.CompletedTask);

        // ManualFiling (created on-demand inside navigation delegate)
        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>>());
        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>>());
        // ManualFilingViewModel now uses the Application query, not the repository directly (C1 fix)
        var getProfileForManualFiling = Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();
        getProfileForManualFiling.HandleAsync(Arg.Any<GetTaxpayerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaxpayerProfileDto?, Error>.Success(null));
        services.AddTransient(_ => getProfileForManualFiling);

        // Reports
        var syncMailbox = Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>();
        services.AddTransient(_ => syncMailbox);

        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(new ReportsPageResult([], 0, 1)));
        services.AddTransient(_ => getReports);

        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>());
        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>>());
        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>>());
        services.AddTransient<Func<string, string, Task<bool>>>(_ => (_, _) => Task.FromResult(false));
        services.AddTransient<Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>>>(
            _ => () => Task.FromResult<(Guid, string, byte[])?>(null));

        // Sync
        services.AddTransient(_ =>
            Substitute.For<ISyncAllCommandHandler>());

        return services.BuildServiceProvider();
    }

    private static SettingsViewModel BuildSettingsVm()
    {
        var saveProfile = Substitute.For<ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>>();
        var getProfile  = Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();
        getProfile.HandleAsync(Arg.Any<GetTaxpayerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaxpayerProfileDto?, Error>.Success(null));
        var profileVm = new ProfileSettingsViewModel(saveProfile, getProfile);

        var getHolidays  = Substitute.For<IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>>();
        var saveHolidays = Substitute.For<ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>>();
        var fetchHolidays = Substitute.For<ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>>();
        var holidayVm = new HolidaySettingsViewModel(getHolidays, saveHolidays, fetchHolidays);

        var getMailboxes    = Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>();
        var addMailbox      = Substitute.For<ICommandHandler<AddMailboxCommand, Result<Guid, Error>>>();
        var updateMailbox   = Substitute.For<ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>>();
        var deleteMailbox   = Substitute.For<ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>>();
        var mailboxVm = new MailboxSettingsViewModel(getMailboxes, addMailbox, updateMailbox, deleteMailbox);

        var getImporters   = Substitute.For<IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>>();
        var addImporter    = Substitute.For<ICommandHandler<AddImporterCommand, Result<Guid, Error>>>();
        var updateImporter = Substitute.For<ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>>();
        var deleteImporter = Substitute.For<ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>>();
        var importerVm = new ImporterSettingsViewModel(
            getImporters, getProfile, getMailboxes,
            addImporter, updateImporter, deleteImporter);

        var themeService = Substitute.For<IThemeService>();
        themeService.GetPreference().Returns(ThemePreference.System);
        var appearanceVm = new AppearanceSettingsViewModel(themeService);

        return new SettingsViewModel(profileVm, holidayVm, mailboxVm, importerVm, appearanceVm);
    }

    private static MainWindowViewModel CreateVm() =>
        new(BuildProvider(), BuildSettingsVm());

    // ── Constructor tests ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultSelectedEntry_IsDashboard()
    {
        var vm = CreateVm();

        vm.SelectedEntry.ViewModel.Should().BeOfType<DashboardViewModel>();
    }

    [Fact]
    public void Constructor_CurrentViewModel_IsDashboardViewModel()
    {
        var vm = CreateVm();

        vm.CurrentViewModel.Should().BeOfType<DashboardViewModel>();
    }

    [Fact]
    public void NavigationEntries_ContainsFiveEntries()
    {
        var vm = CreateVm();

        vm.NavigationEntries.Should().HaveCount(5);
    }

    // ── Navigation tests ──────────────────────────────────────────────────────

    [Fact]
    public void SelectedEntry_WhenChangedToFilings_CurrentViewModelIsFilingsViewModel()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();
        var filingsEntry = vm.NavigationEntries.First(e => e.ViewModel is FilingsViewModel);

        vm.SelectedEntry = filingsEntry;

        vm.CurrentViewModel.Should().BeOfType<FilingsViewModel>();
    }

    [Fact]
    public void SelectedEntry_WhenChangedToSync_CurrentViewModelIsSyncViewModel()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();
        var syncEntry = vm.NavigationEntries.First(e => e.ViewModel is SyncViewModel);

        vm.SelectedEntry = syncEntry;

        vm.CurrentViewModel.Should().BeOfType<SyncViewModel>();
    }

    // ── Cross-VM navigation (Reports → Filings) ───────────────────────────────

    [Fact]
    public void Navigate_ToFilingsWithReportId_SetsReportIdFilterOnFilingsViewModel()
    {
        var vm = CreateVm();
        var reportId = Guid.NewGuid();
        var reportsVm = (ReportsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is ReportsViewModel).ViewModel;

        reportsVm.ViewFilingsCommand.Execute(reportId).Subscribe();

        var filingsVm = (FilingsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is FilingsViewModel).ViewModel;
        filingsVm.ReportIdFilter.Should().Be(reportId);
    }

    [Fact]
    public void Navigate_ToFilingsWithReportId_ChangesSelectedEntryToFilings()
    {
        var vm = CreateVm();
        var reportId = Guid.NewGuid();
        var reportsVm = (ReportsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is ReportsViewModel).ViewModel;

        reportsVm.ViewFilingsCommand.Execute(reportId).Subscribe();

        vm.SelectedEntry.ViewModel.Should().BeOfType<FilingsViewModel>();
    }

    // ── ReportIdFilter cleared on back-navigation (Bug #2 regression tests) ──

    [Fact]
    public void Navigate_FromDashboardToFilings_ClearsReportIdFilter()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        // Pre-set a stale report filter via Reports → Filings navigation
        var reportId = Guid.NewGuid();
        var reportsVm = (ReportsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is ReportsViewModel).ViewModel;
        reportsVm.ViewFilingsCommand.Execute(reportId).Subscribe();

        var filingsVm = (FilingsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is FilingsViewModel).ViewModel;
        filingsVm.ReportIdFilter.Should().Be(reportId); // precondition

        // Navigate to Filings from Dashboard — must clear the stale filter
        var dashboardVm = (DashboardViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is DashboardViewModel).ViewModel;
        dashboardVm.NavigateToFilingsCommand.Execute().Subscribe();

        filingsVm.ReportIdFilter.Should().BeNull();
    }

    [Fact]
    public void Navigate_BackFromManualFilingCancel_ClearsReportIdFilter()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        // Pre-set a stale report filter
        var reportId = Guid.NewGuid();
        var reportsVm = (ReportsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is ReportsViewModel).ViewModel;
        reportsVm.ViewFilingsCommand.Execute(reportId).Subscribe();

        var filingsVm = (FilingsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is FilingsViewModel).ViewModel;
        filingsVm.ReportIdFilter.Should().Be(reportId); // precondition

        // Navigate to ManualFiling
        filingsVm.NewFilingCommand.Execute().Subscribe();
        var manualVm = vm.CurrentViewModel as ManualFilingViewModel;
        manualVm.Should().NotBeNull();

        // Cancel (form not dirty → no confirmation dialog) → navigates back
        manualVm!.CancelCommand.Execute().Subscribe();

        filingsVm.ReportIdFilter.Should().BeNull();
    }
}
