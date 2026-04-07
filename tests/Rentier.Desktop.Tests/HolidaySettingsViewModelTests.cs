using System.Reactive.Concurrency;
using System.Reactive.Linq;
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
        => new(
            query ?? MockQueryHandler(),
            save ?? MockSaveHandler(),
            import ?? MockImportHandler(),
            ImmediateScheduler.Instance);

    [Fact]
    public void OnActivate_LoadsHolidays()
    {
        var queryHandler = MockQueryHandler();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(
                new HolidayConfDto(
                    new List<HolidayEntryDto> { new(new DateOnly(2025, 1, 1), "New Year") },
                    2025, 2028)));

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
            .Returns(Result<HolidayConfDto, Error>.Success(
                new HolidayConfDto(new List<HolidayEntryDto>(), 2025, 2028)));

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
            .Returns(Result<HolidayConfDto, Error>.Success(
                new HolidayConfDto(
                    new List<HolidayEntryDto> { new(new DateOnly(2025, 1, 1), "Test") },
                    2025, 2028)));

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
            .Returns(Result<HolidayConfDto, Error>.Success(
                new HolidayConfDto(new List<HolidayEntryDto>(), 2025, 2028)));

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
            .Returns(Result<HolidayConfDto, Error>.Success(
                new HolidayConfDto(new List<HolidayEntryDto>(), 2025, 2028)));

        var importHandler = MockImportHandler();
        importHandler.HandleAsync(Arg.Any<ImportHolidaysFromWebCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(
                new List<HolidayEntryDto> { new(new DateOnly(2025, 3, 1), "St. David") }));

        var saveHandler = MockSaveHandler();

        var vm = CreateVm(query: queryHandler, save: saveHandler, import: importHandler);
        using var _ = vm.Activator.Activate();

        await vm.ImportCommand.Execute(2025).FirstAsync();

        vm.Entries.Count.Should().Be(1);
        vm.Entries[0].Name.Should().Be("St. David");
        await saveHandler.DidNotReceive().HandleAsync(
            Arg.Any<SaveHolidayConfCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportCommand_OnFailure_SetsErrorMessage()
    {
        var queryHandler = MockQueryHandler();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(
                new HolidayConfDto(
                    new List<HolidayEntryDto> { new(new DateOnly(2025, 1, 1), "Existing") },
                    2025, 2028)));

        var importHandler = MockImportHandler();
        importHandler.HandleAsync(Arg.Any<ImportHolidaysFromWebCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                new Error("FETCH_FAILED", "timeout")));

        var vm = CreateVm(query: queryHandler, import: importHandler);
        using var _ = vm.Activator.Activate();
        var initialCount = vm.Entries.Count;

        await vm.ImportCommand.Execute(2025).FirstAsync();

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.Entries.Count.Should().Be(initialCount);
    }
}
