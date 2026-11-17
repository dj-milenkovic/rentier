using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Application.DTOs;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Rentier.Tests.Common.Fakes;

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
    public async Task GetPagedAsync_ImportDateContains_ImportYear_MatchesAllReports()
    {
        // Both reports are imported on TestClock.TODAY; filtering by that year must match.
        await _repository.AddAsync(
            Report.Create(_importer.Id, "r1.csv", null, null, TestClock.TODAY),
            TestContext.Current.CancellationToken);
        await _repository.AddAsync(
            Report.Create(_importer.Id, "r2.csv", null, null, TestClock.TODAY),
            TestContext.Current.CancellationToken);

        var importYear = TestClock.TODAY.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var filter = new ReportColumnFilter(ImportDateContains: importYear);

        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true, TestContext.Current.CancellationToken);

        total.Should().Be(2);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_ImportDateContains_UnmatchedYear_ReturnsEmpty()
    {
        await _repository.AddAsync(
            Report.Create(_importer.Id, "report.csv", null, null, TestClock.TODAY),
            TestContext.Current.CancellationToken);

        // Year "1900" will never match a report whose ImportDate is today
        var filter = new ReportColumnFilter(ImportDateContains: "1900");
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true, TestContext.Current.CancellationToken);

        total.Should().Be(0);
        items.Should().BeEmpty();
    }

    // ── FilingCountValue ────────────────────────────────────────────────────

    /// <summary>Adds one report per entry in <paramref name="filingCounts"/>, with that many linked filings.</summary>
    private async Task SeedReportsWithFilingCountsAsync(params int[] filingCounts)
    {
        var profile = new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "John Doe", "Belgrade", "018");
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);

        for (var r = 0; r < filingCounts.Length; r++)
        {
            var report = Report.Create(_importer.Id, $"r{r}.csv", null, null, TestClock.TODAY);
            await _context.Reports.AddAsync(report, TestContext.Current.CancellationToken);
            for (var i = 0; i < filingCounts[r]; i++)
            {
                var date = new DateOnly(2024, 1 + i, 15);
                await _context.Filings.AddAsync(Filing.CreateFromIncome(
                    new FilingInfo(IncomeType.Dividend, $"Co{r}-{i}", date, 1000m, 0m, 150m, 150m),
                    profile.Id, date.AddDays(30), new FilingProvenance(ReportId: report.Id)),
                    TestContext.Current.CancellationToken);
            }
        }

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetPagedAsync_FilingCountValue_TotalCountIncludesOnlyMatchingReports()
    {
        await SeedReportsWithFilingCountsAsync(0, 2, 0, 2, 1);

        var (_, total) = await _repository.GetPagedAsync(
            new ReportColumnFilter(FilingCountValue: 2), 0, 10, true, TestContext.Current.CancellationToken);

        total.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedAsync_FilingCountValueZero_MatchesReportsWithoutFilings()
    {
        await SeedReportsWithFilingCountsAsync(0, 2, 0, 1);

        var (items, _) = await _repository.GetPagedAsync(
            new ReportColumnFilter(FilingCountValue: 0), 0, 10, true, TestContext.Current.CancellationToken);

        items.Select(r => r.ReportName).Should().BeEquivalentTo("r0.csv", "r2.csv");
    }

    [Fact]
    public async Task GetPagedAsync_FilingCountValue_PagesOverFilteredSet()
    {
        // Matching reports are spread out; page 1 of size 1 must still return a match.
        await SeedReportsWithFilingCountsAsync(0, 0, 0, 3, 0, 3);

        var (items, _) = await _repository.GetPagedAsync(
            new ReportColumnFilter(FilingCountValue: 3), 0, 1, true, TestContext.Current.CancellationToken);

        items.Should().ContainSingle().Which.ReportName.Should().BeOneOf("r3.csv", "r5.csv");
    }

    // ── EmailDateContains ───────────────────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_EmailDateContains_MatchesSubstringOfEmailDate()
    {
        // EmailDate is stored as DateOnly TEXT "yyyy-MM-dd"; "2024-01" matches January 2024 only.
        var janReport = Report.Create(_importer.Id, "jan.csv", null, null, TestClock.TODAY, new DateOnly(2024, 1, 15));
        var marReport = Report.Create(_importer.Id, "march.csv", null, null, TestClock.TODAY, new DateOnly(2024, 3, 20));

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
        var noEmailDate = Report.Create(_importer.Id, "manual.csv", null, null, TestClock.TODAY);
        var withEmailDate = Report.Create(_importer.Id, "email.csv", null, null, TestClock.TODAY, new DateOnly(2024, 5, 1));

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
            Report.Create(_importer.Id, "r.csv", null, null, TestClock.TODAY, new DateOnly(2024, 1, 1)),
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
        var older = Report.Create(_importer.Id, "older.csv", null, null, TestClock.TODAY, new DateOnly(2024, 1, 15));
        var newer = Report.Create(_importer.Id, "newer.csv", null, null, TestClock.TODAY, new DateOnly(2024, 3, 15));

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
        var older = Report.Create(_importer.Id, "older.csv", null, null, TestClock.TODAY, new DateOnly(2024, 1, 15));
        var newer = Report.Create(_importer.Id, "newer.csv", null, null, TestClock.TODAY, new DateOnly(2024, 3, 15));

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
        var older = Report.Create(_importer.Id, "older.csv", null, null, TestClock.TODAY, new DateOnly(2023, 6, 1));
        var newer = Report.Create(_importer.Id, "newer.csv", null, null, TestClock.TODAY, new DateOnly(2024, 1, 1));

        await _repository.AddAsync(older, TestContext.Current.CancellationToken);
        await _repository.AddAsync(newer, TestContext.Current.CancellationToken);

        var all = await _repository.GetAllAsync(sortDescending: false, TestContext.Current.CancellationToken);

        all.Select(r => r.ReportName).Should().ContainInOrder("older.csv", "newer.csv");
    }

    // ── GetByStatusAsync extended ───────────────────────────────────────────

    [Fact]
    public async Task GetByStatusAsync_NoMatchingStatus_ReturnsEmpty()
    {
        var report = Report.Create(_importer.Id, "r.csv", null, null, TestClock.TODAY);
        // Default status is Init; querying for Processed returns nothing
        await _repository.AddAsync(report, TestContext.Current.CancellationToken);

        var result = await _repository.GetByStatusAsync(ReportStatus.Processed, TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }
}
