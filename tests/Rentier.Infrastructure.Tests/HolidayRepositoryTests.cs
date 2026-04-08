using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Application.DTOs;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Xunit;

namespace Rentier.Infrastructure.Tests;

public class HolidayRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private HolidayRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new HolidayRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetHolidayConf_EmptyDatabase_ReturnsEmptyDto()
    {
        var dto = await _repository.GetHolidayConfAsync();

        dto.Holidays.Should().BeEmpty();
        dto.StartYear.Should().Be(0);
        dto.EndYear.Should().Be(0);
    }

    [Fact]
    public async Task GetHolidayConf_WithData_ReturnsSortedDtoAndRange()
    {
        var yearRange = new HolidayYearRange(2025, 2028);
        var h1 = PublicHoliday.Create(new DateOnly(2025, 6, 28), "Vidovdan");
        var h2 = PublicHoliday.Create(new DateOnly(2025, 1, 1), "Nova godina");
        var h3 = PublicHoliday.Create(new DateOnly(2025, 5, 1), "Praznik rada");

        await _repository.SaveHolidaysAsync(new[] { h1, h2, h3 }, yearRange);

        var dto = await _repository.GetHolidayConfAsync();

        dto.Holidays.Should().HaveCount(3);
        dto.Holidays[0].Date.Should().Be(new DateOnly(2025, 1, 1));
        dto.Holidays[1].Date.Should().Be(new DateOnly(2025, 5, 1));
        dto.Holidays[2].Date.Should().Be(new DateOnly(2025, 6, 28));
        dto.StartYear.Should().Be(2025);
        dto.EndYear.Should().Be(2028);
    }

    [Fact]
    public async Task SaveHolidays_ReplacesAllExistingRows()
    {
        var yearRange1 = new HolidayYearRange(2025, 2028);
        var h1 = PublicHoliday.Create(new DateOnly(2025, 1, 1), "Nova godina");
        var h2 = PublicHoliday.Create(new DateOnly(2025, 1, 7), "Božić");
        await _repository.SaveHolidaysAsync(new[] { h1, h2 }, yearRange1);

        var yearRange2 = new HolidayYearRange(2026, 2029);
        var newHoliday = PublicHoliday.Create(new DateOnly(2026, 1, 1), "Nova godina 2026");
        await _repository.SaveHolidaysAsync(new[] { newHoliday }, yearRange2);

        var dto = await _repository.GetHolidayConfAsync();
        dto.Holidays.Should().HaveCount(1);
        dto.Holidays[0].Name.Should().Be("Nova godina 2026");
        dto.StartYear.Should().Be(2026);
    }

    [Fact]
    public async Task GetYearRange_WhenExists_ReturnsSingleton()
    {
        var yearRange = new HolidayYearRange(2024, 2027);
        await _repository.SaveHolidaysAsync(Array.Empty<PublicHoliday>(), yearRange);

        var result = await _repository.GetYearRangeAsync();

        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.StartYear.Should().Be(2024);
    }
}
