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

public class HolidaySettingsViewModelFetchTests
{
    private static IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>> LoadedQueryHandler(
        IReadOnlyList<HolidayEntryDto>? holidays = null,
        int startYear = 2024,
        int endYear = 2026)
    {
        var handler = Substitute.For<IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>>();
        handler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(
                new HolidayConfDto(holidays ?? new List<HolidayEntryDto>(), startYear, endYear)));
        return handler;
    }

    private static ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>> MockSaveHandler()
        => Substitute.For<ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>>();

    private static ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>> MockFetchHandler()
        => Substitute.For<ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>>();

    private static HolidaySettingsViewModel CreateVm(
        IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>? query = null,
        ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>? fetch = null)
        => new(query ?? LoadedQueryHandler(), MockSaveHandler(), fetch ?? MockFetchHandler(), ImmediateScheduler.Instance);

    [Fact]
    public async Task FetchFromWebCommand_OnSuccess_MergesNewEntriesOnly()
    {
        var existing = new List<HolidayEntryDto> { new(new DateOnly(2025, 1, 1), "Nova godina") };
        var fetched = new List<HolidayEntryDto>
        {
            new(new DateOnly(2025, 1, 1), "Nova godina"),   // duplicate — should be skipped
            new(new DateOnly(2025, 2, 15), "Sretenje")       // new — should be added
        };

        var fetchHandler = MockFetchHandler();
        fetchHandler.HandleAsync(Arg.Any<FetchHolidaysFromWebCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(fetched));

        var vm = CreateVm(query: LoadedQueryHandler(existing), fetch: fetchHandler);
        using var _ = vm.Activator.Activate();

        await vm.FetchFromWebCommand.Execute().FirstAsync();

        vm.Entries.Should().HaveCount(2);
        vm.Entries.Should().Contain(e => e.Name == "Nova godina");
        vm.Entries.Should().Contain(e => e.Name == "Sretenje");
    }

    [Fact]
    public async Task FetchFromWebCommand_OnSuccess_SetsHasUnsavedChanges()
    {
        var fetched = new List<HolidayEntryDto>
        {
            new(new DateOnly(2025, 1, 1), "Nova godina")
        };

        var fetchHandler = MockFetchHandler();
        fetchHandler.HandleAsync(Arg.Any<FetchHolidaysFromWebCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(fetched));

        var vm = CreateVm(fetch: fetchHandler);
        using var _ = vm.Activator.Activate();

        await vm.FetchFromWebCommand.Execute().FirstAsync();

        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task FetchFromWebCommand_OnSuccess_ShowsSuccessMessage()
    {
        var fetched = new List<HolidayEntryDto>
        {
            new(new DateOnly(2025, 1, 1), "Nova godina"),
            new(new DateOnly(2025, 2, 15), "Sretenje")
        };

        var fetchHandler = MockFetchHandler();
        fetchHandler.HandleAsync(Arg.Any<FetchHolidaysFromWebCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(fetched));

        var vm = CreateVm(fetch: fetchHandler);
        using var _ = vm.Activator.Activate();

        await vm.FetchFromWebCommand.Execute().FirstAsync();

        vm.SuccessMessage.Should().NotBeNullOrEmpty();
        vm.SuccessMessage.Should().Contain("2");  // 2 holidays added
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task FetchFromWebCommand_OnFailure_ShowsErrorMessage()
    {
        var fetchHandler = MockFetchHandler();
        fetchHandler.HandleAsync(Arg.Any<FetchHolidaysFromWebCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                new Error("HOLIDAY_FETCH_ALL_FAILED", "Failed to fetch holidays for all years: 2024, 2025, 2026")));

        var vm = CreateVm(fetch: fetchHandler);
        using var _ = vm.Activator.Activate();
        var initialCount = vm.Entries.Count;

        await vm.FetchFromWebCommand.Execute().FirstAsync();

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.SuccessMessage.Should().BeNull();
        vm.Entries.Count.Should().Be(initialCount); // existing data preserved
    }

    [Fact]
    public async Task FetchFromWebCommand_DoesNotDuplicateExistingDate()
    {
        var existingDate = new DateOnly(2025, 1, 1);
        var existing = new List<HolidayEntryDto> { new(existingDate, "Nova godina") };
        var fetched = new List<HolidayEntryDto>
        {
            new(existingDate, "Nova godina"),    // same date — must not be added again
            new(new DateOnly(2025, 5, 1), "Praznik rada")  // new date
        };

        var fetchHandler = MockFetchHandler();
        fetchHandler.HandleAsync(Arg.Any<FetchHolidaysFromWebCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(fetched));

        var vm = CreateVm(query: LoadedQueryHandler(existing), fetch: fetchHandler);
        using var _ = vm.Activator.Activate();

        await vm.FetchFromWebCommand.Execute().FirstAsync();

        var entriesWithExistingDate = vm.Entries.Where(e => e.Date == existingDate).ToList();
        entriesWithExistingDate.Should().HaveCount(1, "duplicate dates must not be added");
        vm.Entries.Should().HaveCount(2);
    }
}
