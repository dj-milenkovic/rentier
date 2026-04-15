using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
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

public class HolidaySettingsViewModelTests
{
    private static IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>> MockQueryHandler()
        => Substitute.For<IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>>();
    private static ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>> MockSaveHandler()
        => Substitute.For<ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>>();
    private static ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>> MockFetchHandler()
        => Substitute.For<ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>>();

    private static HolidaySettingsViewModel CreateVm(
        IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>? query = null,
        ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>? save = null,
        ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>? fetch = null)
        => new(query ?? MockQueryHandler(), save ?? MockSaveHandler(), fetch ?? MockFetchHandler(), ImmediateScheduler.Instance);

    [Fact]
    public void OnActivate_LoadsHolidays()
    {
        var queryHandler = MockQueryHandler();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(new HolidayConfDto(new List<HolidayEntryDto> { new(new DateOnly(2025, 1, 1), "New Year") }, 2025, 2028)));
        var vm = CreateVm(query: queryHandler);
        using var _ = vm.Activator.Activate();
        vm.Entries.Count.Should().Be(1);
        vm.StartYear.Should().Be(2025);
        vm.EndYear.Should().Be(2028);
    }

    [Fact]
    public void AddRow_AppendsBlankEntry()
    {
        var queryHandler = MockQueryHandler();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(new HolidayConfDto(new List<HolidayEntryDto>(), 2025, 2028)));
        var vm = CreateVm(query: queryHandler);
        using var _ = vm.Activator.Activate();
        var countBefore = vm.Entries.Count;
        vm.AddRowCommand.Execute().Subscribe();
        vm.Entries.Count.Should().Be(countBefore + 1);
    }

    [Fact]
    public void DeleteRow_RemovesEntry()
    {
        var queryHandler = MockQueryHandler();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(new HolidayConfDto(new List<HolidayEntryDto> { new(new DateOnly(2025, 1, 1), "Test") }, 2025, 2028)));
        var vm = CreateVm(query: queryHandler);
        using var _ = vm.Activator.Activate();
        var entry = vm.Entries[0];
        vm.SelectedEntry = entry;
        vm.DeleteRowCommand.Execute(entry).Subscribe();
        vm.Entries.Should().NotContain(entry);
    }

    [Fact]
    public async Task SaveCommand_DispatchesSaveHolidayConfCommand()
    {
        var queryHandler = MockQueryHandler();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(new HolidayConfDto(new List<HolidayEntryDto>(), 2025, 2028)));
        var saveHandler = MockSaveHandler();
        saveHandler.HandleAsync(Arg.Any<SaveHolidayConfCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));
        var vm = CreateVm(query: queryHandler, save: saveHandler);
        using var _ = vm.Activator.Activate();
        vm.Entries.Add(new HolidayEntryViewModel { Date = new DateOnly(2025, 1, 1), Name = "Nova godina" });
        await vm.SaveCommand.Execute().FirstAsync();
        await saveHandler.Received(1).HandleAsync(
            Arg.Is<SaveHolidayConfCommand>(c => c.Holidays.Any(h => h.Name == "Nova godina")),
            Arg.Any<CancellationToken>());
    }

    // T005: Date mutation
    [Fact]
    public void HolidayEntryViewModel_DateProperty_CanBeSetAndRead()
    {
        var entry = new HolidayEntryViewModel();
        var expected = new DateOnly(2026, 6, 15);
        entry.Date = expected;
        entry.Date.Should().Be(expected);
    }

    // T014: Command gating when IsLoading
    [Fact]
    public void AddRowCommand_WhenIsLoadingTrue_CannotExecute()
    {
        var vm = CreateVm();
        SetIsLoading(vm, true);
        bool? can = null;
        vm.AddRowCommand.CanExecute.Subscribe(v => can = v);
        can.Should().BeFalse();
    }

    [Fact]
    public void SaveCommand_WhenIsLoadingTrue_CannotExecute()
    {
        var vm = CreateVm();
        SetIsLoading(vm, true);
        bool? can = null;
        vm.SaveCommand.CanExecute.Subscribe(v => can = v);
        can.Should().BeFalse();
    }

    [Fact]
    public void FetchFromWebCommand_WhenIsLoadingTrue_CannotExecute()
    {
        var vm = CreateVm();
        SetIsLoading(vm, true);
        bool? can = null;
        vm.FetchFromWebCommand.CanExecute.Subscribe(v => can = v);
        can.Should().BeFalse();
    }

    [Fact]
    public void DeleteRowCommand_WhenIsLoadingTrue_CannotExecute()
    {
        var vm = CreateVm();
        vm.SelectedEntry = new HolidayEntryViewModel();
        SetIsLoading(vm, true);
        bool? can = null;
        vm.DeleteRowCommand.CanExecute.Subscribe(v => can = v);
        can.Should().BeFalse();
    }

    // T015: HasItems
    [Fact]
    public void HasItems_WhenEntriesEmpty_ReturnsFalse()
    {
        var vm = CreateVm();
        vm.HasItems.Should().BeFalse();
    }

    [Fact]
    public void HasItems_AfterAddingEntry_ReturnsTrue()
    {
        var vm = CreateVm();
        vm.Entries.Add(new HolidayEntryViewModel { Date = new DateOnly(2025, 1, 1), Name = "Test" });
        vm.HasItems.Should().BeTrue();
    }

    [Fact]
    public void HasItems_RaisesPropertyChanged_WhenEntriesCountChanges()
    {
        var vm = CreateVm();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        vm.Entries.Add(new HolidayEntryViewModel());
        raised.Should().Contain(nameof(HolidaySettingsViewModel.HasItems));
    }

    private static void SetIsLoading(HolidaySettingsViewModel vm, bool value)
        => typeof(HolidaySettingsViewModel)
            .GetProperty(nameof(HolidaySettingsViewModel.IsLoading), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(vm, value);
}
