using FluentAssertions;
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

    private static ReportsViewModel CreateReportsVm() =>
        new(Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>());

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
            _ => Task.FromResult(false),
            ImmediateScheduler.Instance);
    }

    [Fact]
    public void MainWindowViewModel_Constructed_NavigationEntriesHasThreeItems()
    {
        var filingsVm = CreateFilingsVm();
        var reportsVm = CreateReportsVm();
        var settingsVm = new SettingsViewModel(CreateProfileVm(), CreateHolidayVm(), CreateMailboxVm(), CreateImporterVm());
        var vm = new MainWindowViewModel(filingsVm, reportsVm, settingsVm);

        vm.NavigationEntries.Count.Should().Be(3);
    }

    [Fact]
    public void MainWindowViewModel_Constructed_InitialViewModelIsFilingsViewModel()
    {
        var filingsVm = CreateFilingsVm();
        var reportsVm = CreateReportsVm();
        var settingsVm = new SettingsViewModel(CreateProfileVm(), CreateHolidayVm(), CreateMailboxVm(), CreateImporterVm());
        var vm = new MainWindowViewModel(filingsVm, reportsVm, settingsVm);

        vm.CurrentViewModel.Should().BeOfType<FilingsViewModel>();
    }
}
