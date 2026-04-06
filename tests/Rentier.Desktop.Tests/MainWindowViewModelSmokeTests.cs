using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.Desktop.Tests;

public class MainWindowViewModelSmokeTests
{
    private static ProfileSettingsViewModel CreateProfileVm() =>
        new(
            Substitute.For<ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>>(),
            Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>());

    [Fact]
    public void MainWindowViewModel_Constructed_NavigationEntriesHasThreeItems()
    {
        var filingsVm = new FilingsViewModel();
        var reportsVm = new ReportsViewModel();
        var settingsVm = new SettingsViewModel(CreateProfileVm());
        var vm = new MainWindowViewModel(filingsVm, reportsVm, settingsVm);

        vm.NavigationEntries.Count.Should().Be(3);
    }

    [Fact]
    public void MainWindowViewModel_Constructed_InitialViewModelIsFilingsViewModel()
    {
        var filingsVm = new FilingsViewModel();
        var reportsVm = new ReportsViewModel();
        var settingsVm = new SettingsViewModel(CreateProfileVm());
        var vm = new MainWindowViewModel(filingsVm, reportsVm, settingsVm);

        vm.CurrentViewModel.Should().BeOfType<FilingsViewModel>();
    }
}
