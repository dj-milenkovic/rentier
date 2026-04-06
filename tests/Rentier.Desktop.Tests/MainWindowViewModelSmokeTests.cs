using FluentAssertions;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.Desktop.Tests;

public class MainWindowViewModelSmokeTests
{
    [Fact]
    public void MainWindowViewModel_Constructed_NavigationEntriesHasThreeItems()
    {
        var filingsVm = new FilingsViewModel();
        var reportsVm = new ReportsViewModel();
        var settingsVm = new SettingsViewModel();
        var vm = new MainWindowViewModel(filingsVm, reportsVm, settingsVm);

        vm.NavigationEntries.Count.Should().Be(3);
    }

    [Fact]
    public void MainWindowViewModel_Constructed_InitialViewModelIsFilingsViewModel()
    {
        var filingsVm = new FilingsViewModel();
        var reportsVm = new ReportsViewModel();
        var settingsVm = new SettingsViewModel();
        var vm = new MainWindowViewModel(filingsVm, reportsVm, settingsVm);

        vm.CurrentViewModel.Should().BeOfType<FilingsViewModel>();
    }
}
