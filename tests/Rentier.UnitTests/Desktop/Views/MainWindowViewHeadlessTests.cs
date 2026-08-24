using Avalonia.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;
using Rentier.Desktop.Views;
using Xunit;
using ReactiveUI.Primitives.Signals;

namespace Rentier.UnitTests;

/// <summary>
/// Headless UI tests for <see cref="MainWindow"/>. Unlike <see cref="MainWindowViewModelTests"/>,
/// these deliberately avoid calling <c>vm.Activator.Activate()</c> directly — activation must
/// happen through the real View lifecycle (Show → Loaded), exactly as it does in production,
/// so these tests catch regressions where the View never triggers ViewModel activation.
/// </summary>
[Trait("Category", "UI")]
[Collection("MainWindowViewModelTests")]
public class MainWindowViewHeadlessTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(_ => Substitute.For<IUpdateService>());

        services.AddTransient(_ =>
            Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>());

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
        services.AddTransient<FilingsHandlers>();

        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>>());
        services.AddTransient(_ =>
            Substitute.For<ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>>());
        var getProfileForManualFiling = Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();
        getProfileForManualFiling.HandleAsync(Arg.Any<GetTaxpayerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaxpayerProfileDto?, Error>.Success(null));
        services.AddTransient(_ => getProfileForManualFiling);

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

        services.AddTransient(_ =>
            Substitute.For<ISyncAllCommandHandler>());

        services.AddSingleton(BuildProfileVm(getProfileForManualFiling));
        services.AddSingleton(BuildHolidayVm());
        services.AddSingleton(BuildMailboxVm());
        services.AddSingleton(BuildImporterVm(getProfileForManualFiling));
        services.AddSingleton(BuildAppearanceVm());

        return services.BuildServiceProvider();
    }

    private static ProfileSettingsViewModel BuildProfileVm(
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> getProfile)
    {
        var saveProfile = Substitute.For<ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>>();
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
                addImporter, updateImporter, deleteImporter));
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
        return new(provider, BuildLocalizationService(), updateService, BuildAppVersionService());
    }

    [AvaloniaFact]
    public void MainWindow_WhenSidebarSelectionChanges_ContentAreaSwapsToSelectedViewModel()
    {
        // Arrange — real MainWindow + real MainWindowViewModel. Activation is expected to
        // happen purely from window.Show() (as it does when Rentier actually launches) —
        // no manual vm.Activator.Activate() call, unlike MainWindowViewModelTests.
        var vm = CreateVm();
        var window = new MainWindow(vm);

        // Act
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var filingsEntry = vm.NavigationEntries.First(e => e.ViewModel is FilingsViewModel);
        vm.SelectedEntry = filingsEntry;
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.CurrentViewModel.Should().BeOfType<FilingsViewModel>();

        window.Close();
    }

    [AvaloniaFact]
    public void MainWindow_SidebarFooter_DisplaysAppVersionText()
    {
        // Arrange
        var vm = CreateVm();
        var window = new MainWindow(vm);

        // Act
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Assert
        var versionLabel = window.GetVisualDescendants()
            .OfType<SelectableTextBlock>()
            .FirstOrDefault(t => t.Text == vm.AppVersionText);

        versionLabel.Should().NotBeNull();

        window.Close();
    }

    [AvaloniaFact]
    public void MainWindow_WhenSettingsClickedTwiceInARow_TogglesExpandedBothTimes()
    {
        // Arrange — real MainWindow + real MainWindowViewModel, real ListBox click simulation.
        // Regression test for https://github.com/dj-milenkovic/rentier/issues/67: the second
        // consecutive click on the same sidebar group header used to be silently swallowed
        // because of a ListBox selection-model reentrancy bug (see MainWindowViewModel.cs).
        var vm = CreateVm();
        var window = new MainWindow(vm);

        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var settingsEntry = vm.NavigationEntries.First(e => e.IsGroup);
        var initiallyExpanded = settingsEntry.IsExpanded;

        ListBoxItem FindSettingsContainer() =>
            window.GetVisualDescendants().OfType<ListBoxItem>()
                .First(i => ReferenceEquals(i.DataContext, settingsEntry));

        void ClickSettings()
        {
            var container = FindSettingsContainer();
            var topLeft = container.TranslatePoint(new Point(0, 0), window)!.Value;
            var center = new Point(topLeft.X + container.Bounds.Width / 2, topLeft.Y + container.Bounds.Height / 2);
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        // Act
        ClickSettings();
        var afterFirstClick = settingsEntry.IsExpanded;

        ClickSettings();
        var afterSecondClick = settingsEntry.IsExpanded;

        window.Close();

        // Assert — each click must toggle the state; the bug manifests as the second
        // click leaving IsExpanded unchanged from afterFirstClick.
        afterFirstClick.Should().Be(!initiallyExpanded, "the first click should toggle the group");
        afterSecondClick.Should().Be(initiallyExpanded,
            "the second consecutive click must toggle it back — this is the bug from issue #67");
    }
}
