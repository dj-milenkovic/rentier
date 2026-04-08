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
    private static ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>> MockImportHandler()
        => Substitute.For<ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>>();

    private static HolidaySettingsViewModel CreateVm(
        IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>? query = null,
        ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>? save = null,
        ICommandHandler<ImportHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>? import = null)
        => new(query ?? MockQueryHandler(), save ?? MockSaveHandler(), import ?? MockImportHandler(), ImmediateScheduler.Instance);

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

    [Fact]
    public async Task ImportCommand_OnSuccess_MergesIntoEntries()
    {
        var queryHandler = MockQueryHandler();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(new HolidayConfDto(new List<HolidayEntryDto>(), 2025, 2028)));
        var importHandler = MockImportHandler();
        importHandler.HandleAsync(Arg.Any<ImportHolidaysFromWebCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(new List<HolidayEntryDto> { new(new DateOnly(2025, 3, 1), "St. David") }));
        var saveHandler = MockSaveHandler();
        var vm = CreateVm(query: queryHandler, save: saveHandler, import: importHandler);
        using var _ = vm.Activator.Activate();
        await vm.ImportCommand.Execute(2025).FirstAsync();
        vm.Entries.Count.Should().Be(1);
        vm.Entries[0].Name.Should().Be("St. David");
        await saveHandler.DidNotReceive().HandleAsync(Arg.Any<SaveHolidayConfCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportCommand_OnFailure_SetsErrorMessage()
    {
        var queryHandler = MockQueryHandler();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(new HolidayConfDto(new List<HolidayEntryDto> { new(new DateOnly(2025, 1, 1), "Existing") }, 2025, 2028)));
        var importHandler = MockImportHandler();
        importHandler.HandleAsync(Arg.Any<ImportHolidaysFromWebCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(new Error("HOLIDAY_IMPORT_FAILED", "timeout")));
        var vm = CreateVm(query: queryHandler, import: importHandler);
        using var _ = vm.Activator.Activate();
        var initialCount = vm.Entries.Count;
        await vm.ImportCommand.Execute(2025).FirstAsync();
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.Entries.Count.Should().Be(initialCount);
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

    // T005: HasUnsavedChanges after import success
    [Fact]
    public async Task ImportCommand_OnSuccess_SetsHasUnsavedChanges()
    {
        var queryHandler = MockQueryHandler();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(new HolidayConfDto(new List<HolidayEntryDto>(), 2025, 2028)));
        var importHandler = MockImportHandler();
        importHandler.HandleAsync(Arg.Any<ImportHolidaysFromWebCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(new List<HolidayEntryDto>
                { new(new DateOnly(2025, 1, 1), "A"), new(new DateOnly(2025, 2, 1), "B"), new(new DateOnly(2025, 3, 1), "C") }));
        var vm = CreateVm(query: queryHandler, import: importHandler);
        using var _ = vm.Activator.Activate();
        await vm.ImportCommand.Execute(2025).FirstAsync();
        vm.Entries.Count.Should().Be(3);
        vm.HasUnsavedChanges.Should().BeTrue();
        vm.ErrorMessage.Should().BeNull();
    }

    // T009: Import failure preserves entries
    [Fact]
    public async Task ImportCommand_OnFailure_PreservesExistingEntries()
    {
        var queryHandler = MockQueryHandler();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(new HolidayConfDto(
                new List<HolidayEntryDto> { new(new DateOnly(2025, 1, 1), "E1"), new(new DateOnly(2025, 2, 1), "E2") }, 2025, 2028)));
        var importHandler = MockImportHandler();
        importHandler.HandleAsync(Arg.Any<ImportHolidaysFromWebCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(new Error("HOLIDAY_IMPORT_FAILED", "network error")));
        var vm = CreateVm(query: queryHandler, import: importHandler);
        using var _ = vm.Activator.Activate();
        await vm.ImportCommand.Execute(2025).FirstAsync();
        vm.Entries.Count.Should().Be(2);
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // T012: ImportYear defaults and setter
    [Fact]
    public void ImportYear_DefaultsToCurrentYear()
    {
        var vm = CreateVm();
        vm.ImportYear.Should().Be(DateTime.Today.Year);
    }

    [Fact]
    public void ImportYear_WhenSet_ReflectsNewValue()
    {
        var vm = CreateVm();
        vm.ImportYear = 2030;
        vm.ImportYear.Should().Be(2030);
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
    public void ImportCommand_WhenIsLoadingTrue_CannotExecute()
    {
        var vm = CreateVm();
        SetIsLoading(vm, true);
        bool? can = null;
        vm.ImportCommand.CanExecute.Subscribe(v => can = v);
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
