using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Application.DTOs;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;

namespace Rentier.Infrastructure.Tests.Repositories;

/// <summary>
/// Covers the <see cref="ReportRepository.GetPagedAsync"/> filter paths that are not
/// exercised by the root-level <c>ReportRepositoryTests</c>:
/// <c>ImportDateContains</c>, <c>EmailDateContains</c>, and sort-ascending order.
/// </summary>
[Trait("Category", "Integration")]
public class ReportRepositoryFilterTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private ReportRepository _repository = null!;
    private Importer _importer = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _repository = new ReportRepository(_context);

        _importer = Importer.Create("Test Importer");
        await _context.Importers.AddAsync(_importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ── ImportDateContains ──────────────────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_ImportDateContains_CurrentYear_MatchesAllReports()
    {
        // Report.Create always sets ImportDate = today; filtering by the current year must match.
        await _repository.AddAsync(
            Report.Create(_importer.Id, "r1.csv", null, null),
            TestContext.Current.CancellationToken);
        await _repository.AddAsync(
            Report.Create(_importer.Id, "r2.csv", null, null),
            TestContext.Current.CancellationToken);

        var currentYear = DateOnly.FromDateTime(DateTime.UtcNow).Year.ToString();
        var filter = new ReportColumnFilter(ImportDateContains: currentYear);

        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true, TestContext.Current.CancellationToken);

        total.Should().Be(2);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_ImportDateContains_UnmatchedYear_ReturnsEmpty()
    {
        await _repository.AddAsync(
            Report.Create(_importer.Id, "report.csv", null, null),
            TestContext.Current.CancellationToken);

        // Year "1900" will never match a report whose ImportDate is today
        var filter = new ReportColumnFilter(ImportDateContains: "1900");
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true, TestContext.Current.CancellationToken);

        total.Should().Be(0);
        items.Should().BeEmpty();
    }

    // ── EmailDateContains ───────────────────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_EmailDateContains_MatchesSubstringOfEmailDate()
    {
        // EmailDate is stored as DateOnly TEXT "yyyy-MM-dd"; "2024-01" matches January 2024 only.
        var janReport = Report.Create(_importer.Id, "jan.csv", null, null, new DateOnly(2024, 1, 15));
        var marReport = Report.Create(_importer.Id, "march.csv", null, null, new DateOnly(2024, 3, 20));

        await _repository.AddAsync(janReport, TestContext.Current.CancellationToken);
        await _repository.AddAsync(marReport, TestContext.Current.CancellationToken);

        var filter = new ReportColumnFilter(EmailDateContains: "2024-01");
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true, TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items[0].ReportName.Should().Be("jan.csv");
    }

    [Fact]
    public async Task GetPagedAsync_EmailDateContains_NoEmailDate_NotReturned()
    {
        // A report without an email date must not appear when an email-date filter is set.
        var noEmailDate = Report.Create(_importer.Id, "manual.csv", null, null);
        var withEmailDate = Report.Create(_importer.Id, "email.csv", null, null, new DateOnly(2024, 5, 1));

        await _repository.AddAsync(noEmailDate, TestContext.Current.CancellationToken);
        await _repository.AddAsync(withEmailDate, TestContext.Current.CancellationToken);

        var filter = new ReportColumnFilter(EmailDateContains: "2024");
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true, TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items[0].ReportName.Should().Be("email.csv");
    }

    [Fact]
    public async Task GetPagedAsync_EmailDateContains_Unmatched_ReturnsEmpty()
    {
        await _repository.AddAsync(
            Report.Create(_importer.Id, "r.csv", null, null, new DateOnly(2024, 1, 1)),
            TestContext.Current.CancellationToken);

        var filter = new ReportColumnFilter(EmailDateContains: "1900");
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true, TestContext.Current.CancellationToken);

        total.Should().Be(0);
        items.Should().BeEmpty();
    }

    // ── Sort ascending ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_SortAscending_OrdersByEmailDateAscending()
    {
        var older = Report.Create(_importer.Id, "older.csv", null, null, new DateOnly(2024, 1, 15));
        var newer = Report.Create(_importer.Id, "newer.csv", null, null, new DateOnly(2024, 3, 15));

        await _repository.AddAsync(older, TestContext.Current.CancellationToken);
        await _repository.AddAsync(newer, TestContext.Current.CancellationToken);

        var (items, _) = await _repository.GetPagedAsync(null, 0, 10, sortDescending: false, TestContext.Current.CancellationToken);

        items.Should().HaveCount(2);
        items[0].ReportName.Should().Be("older.csv");
        items[1].ReportName.Should().Be("newer.csv");
    }

    [Fact]
    public async Task GetPagedAsync_SortDescending_OrdersByEmailDateDescending()
    {
        var older = Report.Create(_importer.Id, "older.csv", null, null, new DateOnly(2024, 1, 15));
        var newer = Report.Create(_importer.Id, "newer.csv", null, null, new DateOnly(2024, 3, 15));

        await _repository.AddAsync(older, TestContext.Current.CancellationToken);
        await _repository.AddAsync(newer, TestContext.Current.CancellationToken);

        var (items, _) = await _repository.GetPagedAsync(null, 0, 10, sortDescending: true, TestContext.Current.CancellationToken);

        items.Should().HaveCount(2);
        items[0].ReportName.Should().Be("newer.csv");
        items[1].ReportName.Should().Be("older.csv");
    }

    // ── GetAllAsync sort ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_SortAscending_OrdersOldestEmailDateFirst()
    {
        var older = Report.Create(_importer.Id, "older.csv", null, null, new DateOnly(2023, 6, 1));
        var newer = Report.Create(_importer.Id, "newer.csv", null, null, new DateOnly(2024, 1, 1));

        await _repository.AddAsync(older, TestContext.Current.CancellationToken);
        await _repository.AddAsync(newer, TestContext.Current.CancellationToken);

        var all = await _repository.GetAllAsync(sortDescending: false, TestContext.Current.CancellationToken);

        all.Select(r => r.ReportName).Should().ContainInOrder("older.csv", "newer.csv");
    }

    // ── GetByStatusAsync extended ───────────────────────────────────────────

    [Fact]
    public async Task GetByStatusAsync_NoMatchingStatus_ReturnsEmpty()
    {
        var report = Report.Create(_importer.Id, "r.csv", null, null);
        // Default status is Init; querying for Processed returns nothing
        await _repository.AddAsync(report, TestContext.Current.CancellationToken);

        var result = await _repository.GetByStatusAsync(ReportStatus.Processed, TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }
}
