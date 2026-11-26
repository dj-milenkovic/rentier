using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
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
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.UnitTests;

[Collection("MainWindowViewModelTests")]
public class MainWindowViewModelTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(TestClock.Provider());

        // UpdateService
        services.AddSingleton(_ => DesktopTestDoubles.UpdateService());

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
        DesktopTestDoubles.AddUiServiceDoubles(services);
        services.AddTransient<FilingsHandlers>();

        // ManualFiling
        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>>());
        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>>());
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
        services.AddTransient<ReportsHandlers>();

        // Sync
        services.AddTransient(_ =>
            Substitute.For<ISyncAllCommandHandler>());

        // Settings sub-ViewModels as singletons
        services.AddSingleton(BuildProfileVm(getProfileForManualFiling));
        services.AddSingleton(BuildHolidayVm());
        services.AddSingleton(BuildMailboxVm());
        services.AddSingleton(BuildImporterVm(getProfileForManualFiling));
        services.AddSingleton(BuildAppearanceVm());
        services.AddSingleton(DesktopTestDoubles.DiagnosticsViewModel());

        return services.BuildServiceProvider();
    }

    private static ProfileSettingsViewModel BuildProfileVm(
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>? getProfile = null)
    {
        var saveProfile = Substitute.For<ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>>();
        getProfile ??= Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();
        return new ProfileSettingsViewModel(saveProfile, getProfile);
    }

    private static HolidaySettingsViewModel BuildHolidayVm()
    {
        var getHolidays = Substitute.For<IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>>();
        var saveHolidays = Substitute.For<ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>>();
        var fetchHolidays = Substitute.For<ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>>();
        return new HolidaySettingsViewModel(getHolidays, saveHolidays, fetchHolidays);
    }

    private static MailboxSettingsViewModel BuildMailboxVm()
    {
        var getMailboxes = Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>();
        var addMailbox = Substitute.For<ICommandHandler<AddMailboxCommand, Result<Guid, Error>>>();
        var updateMailbox = Substitute.For<ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>>();
        var deleteMailbox = Substitute.For<ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>>();
        return new MailboxSettingsViewModel(getMailboxes, addMailbox, updateMailbox, deleteMailbox);
    }

    private static ImporterSettingsViewModel BuildImporterVm(
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> getProfile)
    {
        var getImporters = Substitute.For<IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>>();
        var getMailboxes = Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>();
        var addImporter = Substitute.For<ICommandHandler<AddImporterCommand, Result<Guid, Error>>>();
        var updateImporter = Substitute.For<ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>>();
        var deleteImporter = Substitute.For<ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>>();
        return new ImporterSettingsViewModel(
            new ImporterSettingsHandlers(
                getImporters, getProfile, getMailboxes,
                addImporter, updateImporter, deleteImporter),
            DesktopTestDoubles.Confirmation());
    }

    private static AppearanceSettingsViewModel BuildAppearanceVm()
    {
        var themeService = Substitute.For<IThemeService>();
        themeService.GetPreference().Returns(ThemePreference.System);
        var locService = Substitute.For<ILocalizationService>();
        locService.CurrentCultureCode.Returns("sr-Latn");
        locService.CultureChanged.Returns(Signal.Never<string>());
        var setPreferenceCmd = Substitute.For<ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>>();
        setPreferenceCmd.HandleAsync(Arg.Any<SetUserPreferenceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));
        return new AppearanceSettingsViewModel(themeService, locService, setPreferenceCmd);
    }

    private static ILocalizationService BuildLocalizationService()
    {
        var locService = Substitute.For<ILocalizationService>();
        locService.CultureChanged.Returns(Signal.Never<string>());
        locService["Nav_Dashboard"].Returns("Dashboard");
        locService["Nav_Filings"].Returns("Filings");
        locService["Nav_Reports"].Returns("Reports");
        locService["Nav_Sync"].Returns("Sync");
        locService["Nav_Settings"].Returns("Settings");
        locService["Nav_Settings_Profile"].Returns("Profile");
        locService["Nav_Settings_Holidays"].Returns("Holidays");
        locService["Nav_Settings_Mailboxes"].Returns("Mailboxes");
        locService["Nav_Settings_Importers"].Returns("Importers");
        locService["Nav_Settings_Appearance"].Returns("Appearance");
        return locService;
    }

    private static IAppVersionService BuildAppVersionService(string displayVersion = "v1.2.3")
    {
        var svc = Substitute.For<IAppVersionService>();
        svc.DisplayVersion.Returns(displayVersion);
        return svc;
    }

    private static MainWindowViewModel CreateVm()
    {
        var provider = BuildProvider();
        var updateService = provider.GetRequiredService<IUpdateService>();
        return new(provider, BuildLocalizationService(), updateService, BuildAppVersionService(),
            provider.GetRequiredService<INavigationRequestSource>());
    }

    // ── Constructor tests ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultCurrentViewModel_IsDashboard()
    {
        var vm = CreateVm();

        vm.CurrentViewModel.Should().BeOfType<DashboardViewModel>();
    }

    [Fact]
    public void Constructor_CurrentViewModel_IsDashboardViewModel()
    {
        var vm = CreateVm();

        vm.CurrentViewModel.Should().BeOfType<DashboardViewModel>();
    }

    [Fact]
    public void NavigationEntries_ContainsTenEntries()
    {
        var vm = CreateVm();

        // 4 top-level + 1 Settings group header + 5 children
        vm.NavigationEntries.Should().HaveCount(11);
    }

    [Fact]
    public void Constructor_SetsAppVersionText_FromAppVersionServiceDisplayVersion()
    {
        var provider = BuildProvider();
        var updateService = provider.GetRequiredService<IUpdateService>();
        var appVersionService = BuildAppVersionService("v9.9.9");

        var vm = new MainWindowViewModel(
            provider, BuildLocalizationService(), updateService, appVersionService,
            provider.GetRequiredService<INavigationRequestSource>());

        vm.AppVersionText.Should().Be("v9.9.9");
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
            .First(e => e.ViewModel is ReportsViewModel).ViewModel!;

        reportsVm.ViewFilingsCommand.Execute(reportId).Subscribe();

        var filingsVm = (FilingsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is FilingsViewModel).ViewModel!;
        filingsVm.ReportIdFilter.Should().Be(reportId);
    }

    [Fact]
    public void Navigate_ToFilingsWithReportId_ChangesCurrentViewModelToFilings()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();
        var reportId = Guid.NewGuid();
        var reportsVm = (ReportsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is ReportsViewModel).ViewModel!;

        reportsVm.ViewFilingsCommand.Execute(reportId).Subscribe();

        vm.CurrentViewModel.Should().BeOfType<FilingsViewModel>();
    }

    // ── ReportIdFilter cleared on back-navigation (Bug #2 regression tests) ──

    [Fact]
    public void Navigate_FromDashboardToFilings_ClearsReportIdFilter()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        var reportId = Guid.NewGuid();
        var reportsVm = (ReportsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is ReportsViewModel).ViewModel!;
        reportsVm.ViewFilingsCommand.Execute(reportId).Subscribe();

        var filingsVm = (FilingsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is FilingsViewModel).ViewModel!;
        filingsVm.ReportIdFilter.Should().Be(reportId); // precondition

        var dashboardVm = (DashboardViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is DashboardViewModel).ViewModel!;
        dashboardVm.NavigateToFilingsCommand.Execute().Subscribe();

        filingsVm.ReportIdFilter.Should().BeNull();
    }

    [Fact]
    public void Navigate_BackFromManualFilingCancel_ClearsReportIdFilter()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        var reportId = Guid.NewGuid();
        var reportsVm = (ReportsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is ReportsViewModel).ViewModel!;
        reportsVm.ViewFilingsCommand.Execute(reportId).Subscribe();

        var filingsVm = (FilingsViewModel)vm.NavigationEntries
            .First(e => e.ViewModel is FilingsViewModel).ViewModel!;
        filingsVm.ReportIdFilter.Should().Be(reportId); // precondition

        filingsVm.NewFilingCommand.Execute().Subscribe();
        var manualVm = vm.CurrentViewModel as ManualFilingViewModel;
        manualVm.Should().NotBeNull();

        manualVm!.CancelCommand.Execute().Subscribe();

        filingsVm.ReportIdFilter.Should().BeNull();
    }

    // ── Navigation requests raised through INavigationService ─────────────────

    private static (MainWindowViewModel Vm, INavigationService Navigation) CreateVmWithNavigation()
    {
        var provider = BuildProvider();
        var vm = new MainWindowViewModel(
            provider, BuildLocalizationService(), provider.GetRequiredService<IUpdateService>(),
            BuildAppVersionService(), provider.GetRequiredService<INavigationRequestSource>());
        return (vm, provider.GetRequiredService<INavigationService>());
    }

    [Fact]
    public void NavigateToManualFiling_CalledTwice_OpensFreshFormEachTime()
    {
        var (vm, navigation) = CreateVmWithNavigation();
        using var _ = vm.Activator.Activate();
        navigation.NavigateToManualFiling();
        var first = vm.CurrentViewModel;

        navigation.NavigateToManualFiling();

        vm.CurrentViewModel.Should().BeOfType<ManualFilingViewModel>().And.NotBeSameAs(first);
    }

    [Fact]
    public void NavigateToManualFiling_Called_DoesNotAddSidebarEntry()
    {
        var (vm, navigation) = CreateVmWithNavigation();
        using var _ = vm.Activator.Activate();

        navigation.NavigateToManualFiling();

        vm.NavigationEntries.Should().NotContain(e => e.ViewModel is ManualFilingViewModel);
    }
}

