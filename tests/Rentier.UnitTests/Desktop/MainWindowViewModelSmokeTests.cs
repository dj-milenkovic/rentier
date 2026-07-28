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
using Rentier.Application.Repositories;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;
using ReactiveUI.Primitives.Concurrency;
using Xunit;

namespace Rentier.UnitTests;

[Collection("MainWindowViewModelTests")]
public class MainWindowViewModelSmokeTests
{
    private static ProfileSettingsViewModel CreateProfileVm() =>
        new(
            Substitute.For<ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>>(),
            Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>());

    private static HolidaySettingsViewModel CreateHolidayVm() =>
        new(
            Substitute.For<IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>>(),
            Substitute.For<ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>>(),
            Substitute.For<ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>>());

    private static MailboxSettingsViewModel CreateMailboxVm() =>
        new(
            Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>(),
            Substitute.For<ICommandHandler<AddMailboxCommand, Result<Guid, Error>>>(),
            Substitute.For<ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>>(),
            Substitute.For<ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>>());

    private static ImporterSettingsViewModel CreateImporterVm() =>
        new(
            new ImporterSettingsHandlers(
                Substitute.For<IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>>(),
                Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>(),
                Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>(),
                Substitute.For<ICommandHandler<AddImporterCommand, Result<Guid, Error>>>(),
                Substitute.For<ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>>(),
                Substitute.For<ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>>()));

    private static AppearanceSettingsViewModel CreateAppearanceVm()
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

    private static IServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        var getFilings = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(new FilingsPageResult([], 0, 1)));

        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(new ReportsPageResult([], 0, 1)));

        var getDashboard = Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>();
        getDashboard.HandleAsync(Arg.Any<GetDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto, Error>.Success(
                new DashboardDto([], [], 0, 0, 0, 0m, null)));

        // FilingsViewModel deps
        services.AddSingleton(getFilings);
        services.AddSingleton(Substitute.For<ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>>());
        services.AddSingleton(Substitute.For<ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>>());
        services.AddSingleton(Substitute.For<ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>>());
        services.AddSingleton(Substitute.For<ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>>());
        services.AddSingleton(Substitute.For<ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>>());
        services.AddSingleton<Func<string, Task<bool>>>(_ => Task.FromResult(false));
        services.AddSingleton<Func<ExportFilingResult, Task>>(_ => Task.CompletedTask);
        services.AddTransient<FilingsHandlers>();

        // ManualFilingViewModel deps
        services.AddSingleton(Substitute.For<ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>>());
        services.AddSingleton(Substitute.For<ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>>());
        services.AddSingleton(Substitute.For<ITaxpayerProfileRepository>());

        // Other VM deps
        services.AddSingleton(Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>());
        services.AddSingleton(getReports);
        services.AddSingleton(getDashboard);
        services.AddSingleton(Substitute.For<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>());
        services.AddSingleton(Substitute.For<ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>>());
        services.AddSingleton(Substitute.For<ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>>());
        services.AddSingleton<Func<string, string, Task<bool>>>((_, _) => Task.FromResult(false));
        services.AddSingleton<Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>>>(
            () => Task.FromResult<(Guid, string, byte[])?>(null));
        services.AddSingleton(Substitute.For<ISyncAllCommandHandler>());

        // Settings sub-ViewModels (singletons matching production DI)
        services.AddSingleton(CreateProfileVm());
        services.AddSingleton(CreateHolidayVm());
        services.AddSingleton(CreateMailboxVm());
        services.AddSingleton(CreateImporterVm());
        services.AddSingleton(CreateAppearanceVm());
        services.AddSingleton(_ => Substitute.For<IUpdateService>());

        return services.BuildServiceProvider();
    }

    private static MainWindowViewModel CreateVm()
    {
        var provider = CreateProvider();
        var locService = Substitute.For<ILocalizationService>();
        locService.CultureChanged.Returns(Signal.Never<string>());
        var updateService = provider.GetRequiredService<IUpdateService>();
        return new MainWindowViewModel(provider, locService, updateService);
    }

    [Fact]
    public void MainWindowViewModel_Constructed_NavigationEntriesHasTenItems()
    {
        var vm = CreateVm();

        // 4 top-level (Dashboard, Filings, Reports, Sync) + 1 Settings group + 5 children
        vm.NavigationEntries.Count.Should().Be(10);
    }

    [Fact]
    public void MainWindowViewModel_Constructed_InitialViewModelIsDashboardViewModel()
    {
        var vm = CreateVm();

        vm.CurrentViewModel.Should().BeOfType<DashboardViewModel>();
    }

    [Fact]
    public void NavigationEntries_SettingsGroup_IsGroupTrueAndExpandedByDefault()
    {
        var vm = CreateVm();

        // Entry at index 4 is the Settings group header
        var settingsGroup = vm.NavigationEntries[4];

        settingsGroup.IsGroup.Should().BeTrue();
        settingsGroup.IsExpanded.Should().BeTrue();
        settingsGroup.ViewModel.Should().BeNull();
    }

    [Fact]
    public void NavigationEntries_SettingsChildren_HaveCorrectIndentLevelAndParentGroup()
    {
        var vm = CreateVm();
        var settingsGroup = vm.NavigationEntries[4];

        // Entries 5–9 are the Settings children
        for (int i = 5; i <= 9; i++)
        {
            var child = vm.NavigationEntries[i];
            child.IndentLevel.Should().Be(1, $"entry [{i}] should have IndentLevel=1");
            child.ParentGroup.Should().BeSameAs(settingsGroup, $"entry [{i}] should reference the Settings group");
            child.IsVisible.Should().BeTrue($"entry [{i}] should be visible by default (group starts expanded)");
            child.IsGroup.Should().BeFalse($"entry [{i}] is a child, not a group header");
        }
    }

    [Fact]
    public void SelectingGroupHeader_TogglesIsExpanded_DoesNotChangeCurrentViewModel()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        var settingsGroup = vm.NavigationEntries[4];
        var initialViewModel = vm.CurrentViewModel;
        var initialExpanded = settingsGroup.IsExpanded; // true by default

        // Select the group header — should toggle expand and NOT navigate
        vm.SelectedEntry = settingsGroup;

        vm.CurrentViewModel.Should().BeSameAs(initialViewModel, "CurrentViewModel must not change when clicking a group header");
        settingsGroup.IsExpanded.Should().Be(!initialExpanded, "IsExpanded must be toggled");
    }

    [Fact]
    public void SelectingProfileChild_SetsCurrentViewModelToProfileSettingsViewModel()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        // Entry[5] is the Profile child
        var profileEntry = vm.NavigationEntries[5];

        vm.SelectedEntry = profileEntry;

        vm.CurrentViewModel.Should().BeOfType<ProfileSettingsViewModel>();
    }
}

