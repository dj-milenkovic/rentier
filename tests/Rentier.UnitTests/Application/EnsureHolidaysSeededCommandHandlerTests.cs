using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Xunit;
using Rentier.Tests.Common.Fakes;

namespace Rentier.UnitTests;

public class EnsureHolidaysSeededCommandHandlerTests
{
    private readonly IHolidayRepository _repo = Substitute.For<IHolidayRepository>();
    private readonly FixedTimeProvider _clock = TestClock.Provider();
    private readonly EnsureHolidaysSeededCommandHandler _sut;

    public EnsureHolidaysSeededCommandHandlerTests()
    {
        _sut = new EnsureHolidaysSeededCommandHandler(_repo, _clock);
    }

    [Fact]
    public async Task HandleAsync_WhenHolidaysAlreadyExist_ReturnsFalseWithoutSaving()
    {
        var existingRange = new HolidayYearRange(2024, 2027);
        _repo.GetYearRangeAsync(Arg.Any<CancellationToken>()).Returns(existingRange);

        var result = await _sut.HandleAsync(new EnsureHolidaysSeededCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        await _repo.DidNotReceive().SaveHolidaysAsync(
            Arg.Any<IReadOnlyList<PublicHoliday>>(),
            Arg.Any<HolidayYearRange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenNoHolidaysExist_SeedsAndReturnsTrue()
    {
        _repo.GetYearRangeAsync(Arg.Any<CancellationToken>()).Returns((HolidayYearRange?)null);

        var result = await _sut.HandleAsync(new EnsureHolidaysSeededCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        await _repo.Received(1).SaveHolidaysAsync(
            Arg.Any<IReadOnlyList<PublicHoliday>>(),
            Arg.Any<HolidayYearRange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenNoHolidaysExist_SeedsCurrentYearRange()
    {
        _repo.GetYearRangeAsync(Arg.Any<CancellationToken>()).Returns((HolidayYearRange?)null);

        await _sut.HandleAsync(new EnsureHolidaysSeededCommand(), CancellationToken.None);

        await _repo.Received(1).SaveHolidaysAsync(
            Arg.Any<IReadOnlyList<PublicHoliday>>(),
            Arg.Is<HolidayYearRange>(range =>
                range!.StartYear == TestClock.TODAY.Year && range.EndYear == TestClock.TODAY.Year + 3),
            Arg.Any<CancellationToken>());
    }

    // ── Year boundary: the seeded range follows the LOCAL calendar year ─────────

    // 2025-12-31 23:30 UTC is already 2026-01-01 00:30 in Serbia (UTC+1).
    private static readonly DateTimeOffset NEW_YEARS_EVE_LATE_UTC = new(2025, 12, 31, 23, 30, 0, TimeSpan.Zero);

    private async Task<HolidayYearRange> SeedAtAsync(DateTimeOffset utcNow, TimeSpan localOffset)
    {
        _repo.GetYearRangeAsync(Arg.Any<CancellationToken>()).Returns((HolidayYearRange?)null);
        HolidayYearRange? saved = null;
        await _repo.SaveHolidaysAsync(
            Arg.Any<IReadOnlyList<PublicHoliday>>(),
            Arg.Do<HolidayYearRange>(r => saved = r),
            Arg.Any<CancellationToken>());
        var sut = new EnsureHolidaysSeededCommandHandler(
            _repo, new FixedTimeProvider(utcNow, FixedTimeProvider.FixedOffsetZone(localOffset)));

        await sut.HandleAsync(new EnsureHolidaysSeededCommand(), CancellationToken.None);

        return saved!;
    }

    [Fact]
    public async Task HandleAsync_LocalNewYearAlreadyPassedButUtcHasNot_SeedsFromLocalYear()
    {
        var range = await SeedAtAsync(NEW_YEARS_EVE_LATE_UTC, TimeSpan.FromHours(1));

        range.StartYear.Should().Be(2026);
    }

    [Fact]
    public async Task HandleAsync_UtcNewYearAlreadyPassedButLocalHasNot_SeedsFromLocalYear()
    {
        // 2026-01-01 03:00 UTC is still 2025-12-31 22:00 at UTC-5.
        var range = await SeedAtAsync(
            new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(-5));

        range.StartYear.Should().Be(2025);
    }

    [Fact]
    public async Task HandleAsync_LastLocalSecondOfYear_SeedsFromThatYear()
    {
        var range = await SeedAtAsync(
            new DateTimeOffset(2025, 12, 31, 22, 59, 59, TimeSpan.Zero), TimeSpan.FromHours(1));

        range.StartYear.Should().Be(2025);
    }

    [Fact]
    public async Task HandleAsync_FirstLocalInstantOfYear_SeedsThroughThreeYearsAhead()
    {
        var range = await SeedAtAsync(
            new DateTimeOffset(2025, 12, 31, 23, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

        range.EndYear.Should().Be(2029);
    }
}
