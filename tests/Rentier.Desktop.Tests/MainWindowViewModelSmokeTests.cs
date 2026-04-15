using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using System.Reactive.Concurrency;
using Xunit;

namespace Rentier.Desktop.Tests;

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
            Substitute.For<ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>>());

    private static MailboxSettingsViewModel CreateMailboxVm() =>
        new(
            Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>(),
            Substitute.For<ICommandHandler<AddMailboxCommand, Result<Guid, Error>>>(),
            Substitute.For<ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>>(),
            Substitute.For<ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>>());

    private static ImporterSettingsViewModel CreateImporterVm() =>
        new(
            Substitute.For<IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>>>(),
            Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>(),
            Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>(),
            Substitute.For<ICommandHandler<AddImporterCommand, Result<Guid, Error>>>(),
            Substitute.For<ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>>>(),
            Substitute.For<ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>>>());

    private static FilingsViewModel CreateFilingsVm()
    {
        var getFilings = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(
                new FilingsPageResult([], 0, 1)));
        return new FilingsViewModel(
            getFilings,
            Substitute.For<ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>>(),
            Substitute.For<ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>>(),
            Substitute.For<ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>>(),
            Substitute.For<ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>>(),
            Substitute.For<ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>>(),
            _ => Task.FromResult(false),
            _ => Task.CompletedTask,
            ImmediateScheduler.Instance);
    }

    private static IServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>>();
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ReportRowDto>, Error>.Success(Array.Empty<ReportRowDto>()));

        var getDashboard = Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>();
        getDashboard.HandleAsync(Arg.Any<GetDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto, Error>.Success(
                new DashboardDto([], [], 0, 0, 0, 0m, null)));

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

        return services.BuildServiceProvider();
    }

    [Fact]
    public void MainWindowViewModel_Constructed_NavigationEntriesHasFiveItems()
    {
        var filingsVm  = CreateFilingsVm();
        var settingsVm = new SettingsViewModel(CreateProfileVm(), CreateHolidayVm(), CreateMailboxVm(), CreateImporterVm());
        var vm = new MainWindowViewModel(filingsVm, CreateProvider(), settingsVm);

        vm.NavigationEntries.Count.Should().Be(5);
    }

    [Fact]
    public void MainWindowViewModel_Constructed_InitialViewModelIsDashboardViewModel()
    {
        var filingsVm  = CreateFilingsVm();
        var settingsVm = new SettingsViewModel(CreateProfileVm(), CreateHolidayVm(), CreateMailboxVm(), CreateImporterVm());
        var vm = new MainWindowViewModel(filingsVm, CreateProvider(), settingsVm);

        vm.CurrentViewModel.Should().BeOfType<DashboardViewModel>();
    }
}
