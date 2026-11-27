using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReactiveUI.Primitives.Concurrency;
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
using Rentier.Application.ReleaseNotes;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.UnitTests.Desktop;

[Collection("MainWindowViewModelTests")]
public class MainWindowViewModel_UpdateTests
{
    private static IUpdateService BuildUpdateService(
        UpdateCheckResult? checkResult = null,
        bool isInstalled = true)
    {
        var svc = Substitute.For<IUpdateService>();
        svc.IsInstalled.Returns(isInstalled);
        svc.CheckForUpdatesAsync(Arg.Any<CancellationToken>())
           .Returns(checkResult ?? new UpdateCheckResult(false, null));
        return svc;
    }

    private static readonly string[] UpdateErrorKeys =
    [
        "Update_Error_NotInstalled", "Update_Error_NoPending", "Update_Error_DownloadFailed",
        "Update_Error_DownloadCancelled", "Update_Error_ApplyFailed", "Update_Error_Unknown",
    ];

    private static Task<Result<bool, Error>> DownloadSucceeded()
        => Task.FromResult(Result<bool, Error>.Success(true));

    private static Task<Result<bool, Error>> DownloadFailed(string message)
        => DownloadFailedWith(ErrorCodes.UPDATE_DOWNLOAD_FAILED, message);

    private static Task<Result<bool, Error>> DownloadFailedWith(string code, string message = "service text")
        => Task.FromResult(Result<bool, Error>.Failure(new Error(code, message)));

    private static async Task<MainWindowViewModel> CreateVmInDownloadedStateAsync(IUpdateService updateService)
    {
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadSucceeded());
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);
        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);
        return vm;
    }

    private static IServiceProvider BuildProvider(IUpdateService? updateService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(TestClock.Provider());

        updateService ??= BuildUpdateService();
        services.AddSingleton(updateService);

        var getFilings = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(new FilingsPageResult([], 0, 1)));

        services.AddTransient(_ => getFilings);
        services.AddTransient(_ => Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>());
        services.AddTransient(_ => Substitute.For<ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>>());
        services.AddTransient(_ => Substitute.For<ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>>());
        services.AddTransient(_ => Substitute.For<ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>>());
        services.AddTransient(_ => Substitute.For<ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>>());
        services.AddTransient(_ => Substitute.For<ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>>());
        DesktopTestDoubles.AddUiServiceDoubles(services);
        services.AddTransient<FilingsHandlers>();
        services.AddTransient(_ => Substitute.For<ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>>());
        services.AddTransient(_ => Substitute.For<ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>>());

        var getProfileForManualFiling = Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();
        getProfileForManualFiling.HandleAsync(Arg.Any<GetTaxpayerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaxpayerProfileDto?, Error>.Success(null));
        services.AddTransient(_ => getProfileForManualFiling);

        services.AddTransient(_ => Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>());
        services.AddTransient(_ => Substitute.For<ISyncAllCommandHandler>());

        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(new ReportsPageResult([], 0, 1)));
        services.AddTransient(_ => getReports);

        services.AddTransient(_ => Substitute.For<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>());
        services.AddTransient(_ => Substitute.For<ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>>());
        services.AddTransient(_ => Substitute.For<ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>>());
        services.AddTransient<ReportsHandlers>();

        services.AddSingleton(BuildProfileVm());
        services.AddSingleton(BuildHolidayVm());
        services.AddSingleton(BuildMailboxVm());
        services.AddSingleton(BuildImporterVm());
        services.AddSingleton(BuildAppearanceVm());
        services.AddSingleton(DesktopTestDoubles.DiagnosticsViewModel());
        services.AddSingleton(DesktopTestDoubles.WhatsNewViewModel());

        return services.BuildServiceProvider();
    }

    private static ProfileSettingsViewModel BuildProfileVm()
    {
        return new ProfileSettingsViewModel(
            Substitute.For<ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>>(),
            Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>());
    }

    private static HolidaySettingsViewModel BuildHolidayVm()
    {
        return new HolidaySettingsViewModel(
            Substitute.For<IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>>(),
            Substitute.For<ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>>(),
            Substitute.For<ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>>());
    }

    private static MailboxSettingsViewModel BuildMailboxVm()
    {
        return new MailboxSettingsViewModel(
            Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>(),
            Substitute.For<ICommandHandler<AddMailboxCommand, Result<Guid, Error>>>(),
            Substitute.For<ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>>(),
            Substitute.For<ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>>());
    }

    private static ImporterSettingsViewModel BuildImporterVm()
    {
        return new ImporterSettingsViewModel(
            new ImporterSettingsHandlers(
                Substitute.For<IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>>(),
                Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>(),
                Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>(),
                Substitute.For<ICommandHandler<AddImporterCommand, Result<Guid, Error>>>(),
                Substitute.For<ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>>(),
                Substitute.For<ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>>()),
            DesktopTestDoubles.Confirmation());
    }

    private static AppearanceSettingsViewModel BuildAppearanceVm()
    {
        var themeService = Substitute.For<IThemeService>();
        themeService.GetPreference().Returns(ThemePreference.System);
        var locService = Substitute.For<ILocalizationService>();
        locService.CurrentCultureCode.Returns("sr-Latn");
        locService.CultureChanged.Returns(Signal.Never<string>());
        var setCmd = Substitute.For<ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>>();
        setCmd.HandleAsync(Arg.Any<SetUserPreferenceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));
        return new AppearanceSettingsViewModel(themeService, locService, setCmd);
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
        locService["Update_Available_Message"].Returns("Update v{0} available");
        locService["Update_Downloading_Message"].Returns("Downloading update... {0}%");
        locService["Update_Error_Message"].Returns("Update failed: {0}");
        // Failure reasons resolve to their own key name, so a test can see which one was chosen.
        foreach (var key in UpdateErrorKeys)
            locService[key].Returns(key);
        return locService;
    }

    private static IAppVersionService BuildAppVersionService(string displayVersion = "v1.2.3")
    {
        var svc = Substitute.For<IAppVersionService>();
        svc.DisplayVersion.Returns(displayVersion);
        return svc;
    }

    private static MainWindowViewModel CreateVm(
        IUpdateService? updateService = null,
        IReleaseNotesDialogService? releaseNotesDialog = null,
        string cultureCode = "en")
    {
        var svc = updateService ?? BuildUpdateService();
        var provider = BuildProvider(svc);
        var localization = BuildLocalizationService();
        localization.CurrentCultureCode.Returns(cultureCode);
        localization["ReleaseNotesDialog_Subheader"].Returns("From {0} to {1}");
        return new MainWindowViewModel(
            provider, localization, svc, BuildAppVersionService(),
            provider.GetRequiredService<INavigationRequestSource>(),
            releaseNotesDialog ?? Substitute.For<IReleaseNotesDialogService>(),
            ImmediateSequencer.Instance);
    }

    [Fact]
    public void InitialState_IsIdle()
    {
        var vm = CreateVm();
        vm.CurrentUpdateState.Should().Be(UpdateState.Idle);
    }

    [Fact]
    public void InitialState_UpdateBarVisible_IsFalse()
    {
        var vm = CreateVm();
        vm.UpdateBarVisible.Should().BeFalse();
    }

    [Fact]
    public void InitialState_AvailableVersion_IsNull()
    {
        var vm = CreateVm();
        vm.AvailableVersion.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdateCommand_WhenUpdateAvailable_TransitionsToUpdateAvailable()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        var vm = CreateVm(updateService);

        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CurrentUpdateState.Should().Be(UpdateState.UpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdateCommand_WhenUpdateAvailable_SetsAvailableVersion()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.5.1"));
        var vm = CreateVm(updateService);

        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.AvailableVersion.Should().Be("2.5.1");
    }

    [Fact]
    public async Task CheckForUpdateCommand_WhenNoUpdate_StaysIdle()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(false, null));
        var vm = CreateVm(updateService);

        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CurrentUpdateState.Should().Be(UpdateState.Idle);
    }

    [Fact]
    public async Task CheckForUpdateCommand_WhenUpdateAvailable_UpdateBarVisibleIsTrue()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "3.0.0"));
        var vm = CreateVm(updateService);

        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateBarVisible.Should().BeTrue();
    }

    [Fact]
    public async Task DismissUpdateCommand_FromUpdateAvailable_TransitionsToDismissed()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.DismissUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CurrentUpdateState.Should().Be(UpdateState.Dismissed);
    }

    [Fact]
    public async Task DismissUpdateCommand_FromUpdateAvailable_HidesUpdateBar()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.DismissUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateBarVisible.Should().BeFalse();
    }

    [Fact]
    public async Task BeginUpdateCommand_WhenDownloadCompletes_TransitionsToDownloaded()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadSucceeded());
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CurrentUpdateState.Should().Be(UpdateState.Downloaded);
    }

    [Fact]
    public async Task BeginUpdateCommand_WhenDownloadFails_TransitionsToError()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadFailed("Network error"));
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CurrentUpdateState.Should().Be(UpdateState.Error);
    }

    [Fact]
    public async Task BeginUpdateCommand_WhenDownloadFails_SetsUpdateErrorMessage()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadFailed("Network error"));
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateBarVisible_WhenDownloaded_IsTrue()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadSucceeded());
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateBarVisible.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateBarVisible_WhenError_IsTrue()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadFailed("Network error"));
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateBarVisible.Should().BeTrue();
    }

    [Fact]
    public async Task DismissRestartCommand_FromDownloaded_TransitionsToIdle()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadSucceeded());
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);
        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.DismissRestartCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CurrentUpdateState.Should().Be(UpdateState.Idle);
    }

    [Fact]
    public async Task DismissRestartCommand_FromDownloaded_DoesNotApplyUpdate()
    {
        // "Later" leaves the downloaded package for Velopack to apply on the next launch.
        // It must not start an apply: Velopack's WaitExitThenApplyUpdates kills the app if it
        // is still running 60 seconds later.
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        var vm = await CreateVmInDownloadedStateAsync(updateService);

        await vm.DismissRestartCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        updateService.DidNotReceive().ApplyUpdateAndRestart();
    }

    [Fact]
    public async Task RestartNowCommand_FromDownloaded_AppliesUpdateOnce()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.ApplyUpdateAndRestart().Returns(Result<bool, Error>.Success(true));
        var vm = await CreateVmInDownloadedStateAsync(updateService);

        await vm.RestartNowCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        updateService.Received(1).ApplyUpdateAndRestart();
    }

    [Fact]
    public async Task RestartNowCommand_WhenApplyFails_TransitionsToError()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.ApplyUpdateAndRestart()
            .Returns(Result<bool, Error>.Failure(new Error(ErrorCodes.UPDATE_APPLY_FAILED, "Update.exe missing")));
        var vm = await CreateVmInDownloadedStateAsync(updateService);

        await vm.RestartNowCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CurrentUpdateState.Should().Be(UpdateState.Error);
    }

    [Fact]
    public async Task RestartNowCommand_WhenApplyFails_SetsUpdateErrorMessage()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.ApplyUpdateAndRestart()
            .Returns(Result<bool, Error>.Failure(new Error(ErrorCodes.UPDATE_APPLY_FAILED, "Update.exe missing")));
        var vm = await CreateVmInDownloadedStateAsync(updateService);

        await vm.RestartNowCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateErrorMessage.Should().Be("Update_Error_ApplyFailed");
    }

    [Theory]
    [InlineData(ErrorCodes.UPDATE_NOT_INSTALLED, "Update_Error_NotInstalled")]
    [InlineData(ErrorCodes.UPDATE_NO_PENDING, "Update_Error_NoPending")]
    [InlineData(ErrorCodes.UPDATE_DOWNLOAD_FAILED, "Update_Error_DownloadFailed")]
    [InlineData(ErrorCodes.UPDATE_DOWNLOAD_CANCELLED, "Update_Error_DownloadCancelled")]
    [InlineData(ErrorCodes.UPDATE_APPLY_FAILED, "Update_Error_ApplyFailed")]
    [InlineData("SOMETHING_ELSE", "Update_Error_Unknown")]
    public async Task UpdateErrorMessage_WhenDownloadFails_IsLocalizedTextForTheErrorCode(string code, string expectedKey)
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadFailedWith(code));
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateErrorMessage.Should().Be(expectedKey);
    }

    // ── Automatic check on activation ─────────────────────────────────────────

    [Fact]
    public void Activation_ChecksForUpdatesExactlyOnce()
    {
        var updateService = BuildUpdateService();
        var vm = CreateVm(updateService);

        using var _ = vm.Activator.Activate();

        updateService.Received(1).CheckForUpdatesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Activation_WhenUpdateAlreadyFound_DoesNotCheckAgain()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        var vm = CreateVm(updateService);
        vm.Activator.Activate().Dispose();
        updateService.ClearReceivedCalls();

        using var _ = vm.Activator.Activate();

        updateService.DidNotReceive().CheckForUpdatesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activation_WhenUpdateDownloaded_KeepsDownloadedState()
    {
        // Re-activation must not send a downloaded update back to "available": the Restart
        // button would disappear.
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        var vm = await CreateVmInDownloadedStateAsync(updateService);

        using var _ = vm.Activator.Activate();

        vm.CurrentUpdateState.Should().Be(UpdateState.Downloaded);
    }

    [Fact]
    public async Task Activation_WhenUpdateDismissed_KeepsBarHidden()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);
        await vm.DismissUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        using var _ = vm.Activator.Activate();

        vm.UpdateBarVisible.Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task Activation_WhenCheckCompletesOnThreadPool_ChangesUpdateStateOnUiThread()
    {
        // Bound properties must change on the UI thread. The check's I/O may complete on a
        // thread-pool thread; the continuation that sets CurrentUpdateState must not run there.
        var pendingCheck = new TaskCompletionSource<UpdateCheckResult>();
        var updateService = BuildUpdateService();
        updateService.CheckForUpdatesAsync(Arg.Any<CancellationToken>()).Returns(pendingCheck.Task);
        var vm = CreateVm(updateService);
        var raisedOnUiThread = new List<bool>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.CurrentUpdateState))
                raisedOnUiThread.Add(Dispatcher.UIThread.CheckAccess());
        };

        using var _ = vm.Activator.Activate();
        await Task.Run(() => pendingCheck.SetResult(new UpdateCheckResult(true, "2.0.0")),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        raisedOnUiThread.Should().NotBeEmpty().And.OnlyContain(onUiThread => onUiThread);
    }

    // ── Localized format-string property tests ────────────────────────────────

    [Fact]
    public async Task UpdateAvailableText_WhenVersionIsSet_ContainsVersion()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.5.1"));
        var vm = CreateVm(updateService);

        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateAvailableText.Should().Contain("2.5.1");
    }

    [Fact]
    public async Task UpdateAvailableText_WhenVersionIsSet_UsesFormatTemplate()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "3.0.0"));
        var vm = CreateVm(updateService);

        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateAvailableText.Should().Be("Update v3.0.0 available");
    }

    [Fact]
    public async Task DownloadingText_WhenProgressChanges_ContainsProgress()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var callback = ci.ArgAt<Action<int>>(0);
                callback(42);
                return DownloadSucceeded();
            });
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        // DownloadingText is computed from DownloadProgress; after completion progress may be any value.
        // Assert the format template is applied.
        vm.DownloadingText.Should().Contain("%");
    }

    [Fact]
    public async Task UpdateFailedText_WhenDownloadFails_DoesNotShowServiceErrorMessage()
    {
        // Error.Message is English-only and may quote an exception (internal URLs, paths).
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadFailed("Network error"));
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateFailedText.Should().NotContain("Network error");
    }

    [Fact]
    public async Task UpdateFailedText_WhenDownloadFails_UsesFormatTemplate()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.0.0"));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadFailed("Timeout"));
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateFailedText.Should().Be("Update failed: Update_Error_DownloadFailed");
    }

    // ── Release notes of the available update ────────────────────────────────

    private static readonly ReleaseNotesEntry[] TwoVersionsOfNotes =
    [
        new(new Version(2, 1, 0), "- Newest", "- Najnovije"),
        new(new Version(2, 0, 0), "- Older", "- Starije"),
    ];

    private static IReleaseNotesDialogService Dialog(bool updateNow)
    {
        var dialog = Substitute.For<IReleaseNotesDialogService>();
        dialog.ShowAsync(Arg.Any<ReleaseNotesDialogRequest>()).Returns(updateNow);
        return dialog;
    }

    [Fact]
    public async Task CheckForUpdateCommand_WhenUpdateCarriesNotes_CanShowUpdateReleaseNotes()
    {
        var vm = CreateVm(BuildUpdateService(new UpdateCheckResult(true, "2.1.0", TwoVersionsOfNotes)));

        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.UpdateReleaseNotes.Should().Equal(TwoVersionsOfNotes);
        vm.CanShowUpdateReleaseNotes.Should().BeTrue();
    }

    [Fact]
    public async Task CheckForUpdateCommand_WhenUpdateCarriesNoNotes_HidesWhatsNewButton()
    {
        var vm = CreateVm(BuildUpdateService(new UpdateCheckResult(true, "2.1.0")));

        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CanShowUpdateReleaseNotes.Should().BeFalse();
    }

    [Fact]
    public async Task DismissUpdateCommand_HidesWhatsNewButton()
    {
        var vm = CreateVm(BuildUpdateService(new UpdateCheckResult(true, "2.1.0", TwoVersionsOfNotes)));
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.DismissUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CanShowUpdateReleaseNotes.Should().BeFalse();
    }

    [Fact]
    public async Task CanShowUpdateReleaseNotes_WhenDownloaded_IsTrue()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.1.0", TwoVersionsOfNotes));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadSucceeded());
        var vm = CreateVm(updateService);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CurrentUpdateState.Should().Be(UpdateState.Downloaded);
        vm.CanShowUpdateReleaseNotes.Should().BeTrue();
    }

    [Theory]
    [InlineData("en", "- Newest", "- Older")]
    [InlineData("sr-Latn", "- Najnovije", "- Starije")]
    public async Task ShowUpdateReleaseNotesCommand_ShowsEveryVersionInUiLanguage(
        string culture, string newest, string older)
    {
        var dialog = Dialog(updateNow: false);
        var vm = CreateVm(
            BuildUpdateService(new UpdateCheckResult(true, "2.1.0", TwoVersionsOfNotes)), dialog, culture);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.ShowUpdateReleaseNotesCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await dialog.Received(1).ShowAsync(Arg.Is<ReleaseNotesDialogRequest>(r =>
            r.OfferUpdate
            && r.Subheader == $"From {vm.AppVersionText} to v2.1.0"
            && r.Notes.Select(n => n.Version).SequenceEqual(new[] { "v2.1.0", "v2.0.0" })
            && r.Notes[0].Markdown == newest
            && r.Notes[1].Markdown == older));
    }

    [Fact]
    public async Task ShowUpdateReleaseNotesCommand_WhenUserChoosesUpdateNow_StartsDownload()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.1.0", TwoVersionsOfNotes));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadSucceeded());
        var vm = CreateVm(updateService, Dialog(updateNow: true));
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.ShowUpdateReleaseNotesCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await updateService.Received(1).DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>());
        vm.CurrentUpdateState.Should().Be(UpdateState.Downloaded);
    }

    [Fact]
    public async Task ShowUpdateReleaseNotesCommand_WhenUserCloses_DoesNotDownload()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.1.0", TwoVersionsOfNotes));
        var vm = CreateVm(updateService, Dialog(updateNow: false));
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.ShowUpdateReleaseNotesCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await updateService.DidNotReceive().DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>());
        vm.CurrentUpdateState.Should().Be(UpdateState.UpdateAvailable);
    }

    [Fact]
    public async Task ShowUpdateReleaseNotesCommand_AfterDownload_DoesNotOfferUpdate()
    {
        var updateService = BuildUpdateService(new UpdateCheckResult(true, "2.1.0", TwoVersionsOfNotes));
        updateService.DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>())
            .Returns(DownloadSucceeded());
        var dialog = Dialog(updateNow: true);
        var vm = CreateVm(updateService, dialog);
        await vm.CheckForUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);
        await vm.BeginUpdateCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await vm.ShowUpdateReleaseNotesCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await dialog.Received(1).ShowAsync(Arg.Is<ReleaseNotesDialogRequest>(r => !r.OfferUpdate));
        await updateService.Received(1).DownloadUpdateAsync(Arg.Any<Action<int>>(), Arg.Any<CancellationToken>());
    }
}
