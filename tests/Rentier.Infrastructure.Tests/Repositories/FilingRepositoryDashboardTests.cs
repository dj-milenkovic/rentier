using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Xunit;

namespace Rentier.Infrastructure.Tests.Repositories;

public class FilingRepositoryDashboardTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private FilingRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new FilingRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static TaxpayerProfile MakeProfile()
        => new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "Test User", "Belgrade", "11001");

    private static Filing MakeFiling(
        Guid profileId,
        DateOnly deadline,
        FilingStatus status = FilingStatus.Init,
        string entity = "ACME",
        decimal taxPayable = 100m)
    {
        var f = Filing.CreateFromIncome(
            profileId, IncomeType.Dividend, entity,
            new DateOnly(2024, 3, 1), 1000m, 150m, 150m, taxPayable,
            deadline);
        if (status == FilingStatus.Filed) f.AdvanceStatus(FilingStatus.Filed);
        if (status == FilingStatus.Paid) { f.AdvanceStatus(FilingStatus.Filed); f.AdvanceStatus(FilingStatus.Paid); }
        return f;
    }

    private async Task<TaxpayerProfile> AddProfileAsync()
    {
        var p = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(p);
        await _context.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task GetUpcomingAsync_ReturnsOnlyInitAndFiled_WithinDays()
    {
        var profile = await AddProfileAsync();
        var today = new DateOnly(2024, 6, 1);
        var initFiling = MakeFiling(profile.Id, today.AddDays(5), FilingStatus.Init);
        var filedFiling = MakeFiling(profile.Id, today.AddDays(10), FilingStatus.Filed);
        await _repository.AddAsync(initFiling);
        await _repository.AddAsync(filedFiling);

        var result = await _repository.GetUpcomingAsync(today, 30);

        result.Should().HaveCount(2);
        result.Should().Contain(f => f.Status == FilingStatus.Init);
        result.Should().Contain(f => f.Status == FilingStatus.Filed);
    }

    [Fact]
    public async Task GetUpcomingAsync_ExcludesPaid()
    {
        var profile = await AddProfileAsync();
        var today = new DateOnly(2024, 6, 1);
        var paidFiling = MakeFiling(profile.Id, today.AddDays(5), FilingStatus.Paid);
        await _repository.AddAsync(paidFiling);

        var result = await _repository.GetUpcomingAsync(today, 30);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingAsync_ExcludesOutsideDateRange()
    {
        var profile = await AddProfileAsync();
        var today = new DateOnly(2024, 6, 1);
        var tooEarly = MakeFiling(profile.Id, today.AddDays(-1), FilingStatus.Init);
        var tooLate = MakeFiling(profile.Id, today.AddDays(31), FilingStatus.Init);
        var inRange = MakeFiling(profile.Id, today.AddDays(15), FilingStatus.Init);
        await _repository.AddAsync(tooEarly);
        await _repository.AddAsync(tooLate);
        await _repository.AddAsync(inRange);

        var result = await _repository.GetUpcomingAsync(today, 30);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(inRange.Id);
    }

    [Fact]
    public async Task GetUpcomingAsync_OrdersByDeadlineAscending()
    {
        var profile = await AddProfileAsync();
        var today = new DateOnly(2024, 6, 1);
        var later = MakeFiling(profile.Id, today.AddDays(20), FilingStatus.Init);
        var sooner = MakeFiling(profile.Id, today.AddDays(5), FilingStatus.Init);
        await _repository.AddAsync(later);
        await _repository.AddAsync(sooner);

        var result = await _repository.GetUpcomingAsync(today, 30);

        result.Should().HaveCount(2);
        result[0].FilingDeadline.Should().BeBefore(result[1].FilingDeadline);
    }

    [Fact]
    public async Task GetOverdueAsync_ReturnsBeforeToday_ExcludesPaid()
    {
        var profile = await AddProfileAsync();
        var today = new DateOnly(2024, 6, 1);
        var overdueInit = MakeFiling(profile.Id, today.AddDays(-5), FilingStatus.Init);
        var overdueFiled = MakeFiling(profile.Id, today.AddDays(-10), FilingStatus.Filed);
        var overduePaid = MakeFiling(profile.Id, today.AddDays(-3), FilingStatus.Paid);
        await _repository.AddAsync(overdueInit);
        await _repository.AddAsync(overdueFiled);
        await _repository.AddAsync(overduePaid);

        var result = await _repository.GetOverdueAsync(today);

        result.Should().HaveCount(2);
        result.Should().NotContain(f => f.Status == FilingStatus.Paid);
    }

    [Fact]
    public async Task GetOverdueAsync_ExcludesToday()
    {
        var profile = await AddProfileAsync();
        var today = new DateOnly(2024, 6, 1);
        var todayFiling = MakeFiling(profile.Id, today, FilingStatus.Init);
        await _repository.AddAsync(todayFiling);

        var result = await _repository.GetOverdueAsync(today);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOverdueAsync_OrdersByDeadlineAscending()
    {
        var profile = await AddProfileAsync();
        var today = new DateOnly(2024, 6, 1);
        var newer = MakeFiling(profile.Id, today.AddDays(-1), FilingStatus.Init);
        var older = MakeFiling(profile.Id, today.AddDays(-10), FilingStatus.Init);
        await _repository.AddAsync(newer);
        await _repository.AddAsync(older);

        var result = await _repository.GetOverdueAsync(today);

        result.Should().HaveCount(2);
        result[0].FilingDeadline.Should().BeBefore(result[1].FilingDeadline);
    }

    [Fact]
    public async Task GetFilingStatsAsync_CountsCorrectly()
    {
        var profile = await AddProfileAsync();
        var deadline = new DateOnly(2024, 6, 30);
        await _repository.AddAsync(MakeFiling(profile.Id, deadline, FilingStatus.Init));
        await _repository.AddAsync(MakeFiling(profile.Id, deadline.AddDays(1), FilingStatus.Init));
        await _repository.AddAsync(MakeFiling(profile.Id, deadline.AddDays(2), FilingStatus.Filed));
        await _repository.AddAsync(MakeFiling(profile.Id, deadline.AddDays(3), FilingStatus.Paid));

        var (initCount, filedCount, paidCount, _) = await _repository.GetFilingStatsAsync();

        initCount.Should().Be(2);
        filedCount.Should().Be(1);
        paidCount.Should().Be(1);
    }

    [Fact]
    public async Task GetFilingStatsAsync_TotalUnpaidExcludesPaid()
    {
        var profile = await AddProfileAsync();
        var deadline = new DateOnly(2024, 6, 30);
        await _repository.AddAsync(MakeFiling(profile.Id, deadline, FilingStatus.Init, taxPayable: 100m));
        await _repository.AddAsync(MakeFiling(profile.Id, deadline.AddDays(1), FilingStatus.Filed, taxPayable: 200m));
        await _repository.AddAsync(MakeFiling(profile.Id, deadline.AddDays(2), FilingStatus.Paid, taxPayable: 500m));

        var (_, _, _, totalUnpaid) = await _repository.GetFilingStatsAsync();

        totalUnpaid.Should().Be(300m);
    }

    [Fact]
    public async Task GetFilingStatsAsync_EmptyDatabase_ReturnsZeros()
    {
        var (initCount, filedCount, paidCount, totalUnpaid) = await _repository.GetFilingStatsAsync();

        initCount.Should().Be(0);
        filedCount.Should().Be(0);
        paidCount.Should().Be(0);
        totalUnpaid.Should().Be(0m);
    }
}
