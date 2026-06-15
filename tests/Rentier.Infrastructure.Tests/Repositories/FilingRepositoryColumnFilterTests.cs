using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Application.Enums;
using Rentier.Application.Queries;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;

namespace Rentier.Infrastructure.Tests.Repositories;

/// <summary>
/// Covers the feature-050 multi-select column filters and the PaymentReference text filter
/// on <see cref="FilingRepository.GetPagedAsync"/> — paths that are not exercised by the
/// root-level <c>FilingRepositoryTests</c>.
/// </summary>
[Trait("Category", "Integration")]
public class FilingRepositoryColumnFilterTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private FilingRepository _repository = null!;
    private TaxpayerProfile _profile = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _repository = new FilingRepository(_context);

        // Single profile shared by all tests in this class
        _profile = new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "Test User", "Belgrade", "018");
        await _context.TaxpayerProfiles.AddAsync(_profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ── helper factories ────────────────────────────────────────────────────

    private Filing MakeDividend(string entity, DateOnly incomeDate, decimal tax = 100m)
        => Filing.CreateFromIncome(_profile.Id, IncomeType.Dividend, entity,
            incomeDate, 1000m, 0m, tax, tax, incomeDate.AddDays(30));

    private Filing MakeInterest(string entity, DateOnly incomeDate, decimal tax = 50m)
        => Filing.CreateFromIncome(_profile.Id, IncomeType.Interest, entity,
            incomeDate, 500m, 0m, tax, tax, incomeDate.AddDays(30));

    // ── multi-select Statuses (feature 050) ────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_MultiSelectStatuses_ReturnsAllMatchingStatuses()
    {
        var initFiling = MakeDividend("InitCo", new DateOnly(2024, 1, 1));
        var filedFiling = MakeDividend("FiledCo", new DateOnly(2024, 2, 1));
        var paidFiling = MakeDividend("PaidCo", new DateOnly(2024, 3, 1));
        filedFiling.AdvanceStatus(FilingStatus.Filed);
        paidFiling.AdvanceStatus(FilingStatus.Filed);
        paidFiling.AdvanceStatus(FilingStatus.Paid);

        await _repository.AddAsync(initFiling, TestContext.Current.CancellationToken);
        await _repository.AddAsync(filedFiling, TestContext.Current.CancellationToken);
        await _repository.AddAsync(paidFiling, TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(Statuses: new HashSet<FilingStatus>
        {
            FilingStatus.Init,
            FilingStatus.Filed
        });

        var (items, total) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(2);
        items.Should().NotContain(f => f.Status == FilingStatus.Paid);
        items.Select(f => f.Status).Should()
            .BeEquivalentTo(new[] { FilingStatus.Init, FilingStatus.Filed });
    }

    [Fact]
    public async Task GetPagedAsync_MultiSelectStatuses_SingleEntry_FiltersCorrectly()
    {
        var initFiling = MakeDividend("InitCo", new DateOnly(2024, 1, 1));
        var filedFiling = MakeDividend("FiledCo", new DateOnly(2024, 2, 1));
        filedFiling.AdvanceStatus(FilingStatus.Filed);

        await _repository.AddAsync(initFiling, TestContext.Current.CancellationToken);
        await _repository.AddAsync(filedFiling, TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(Statuses: new HashSet<FilingStatus> { FilingStatus.Filed });
        var (items, total) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items[0].Status.Should().Be(FilingStatus.Filed);
    }

    [Fact]
    public async Task GetPagedAsync_MultiSelectStatuses_EmptySet_ReturnsAll()
    {
        // Statuses with Count == 0 must be treated as "no filter" (guard: Count: > 0)
        await _repository.AddAsync(MakeDividend("Co1", new DateOnly(2024, 1, 1)), TestContext.Current.CancellationToken);
        await _repository.AddAsync(MakeDividend("Co2", new DateOnly(2024, 2, 1)), TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(Statuses: new HashSet<FilingStatus>());  // empty — no filter applied
        var (items, total) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(2);
    }

    // ── multi-select IncomeTypes (feature 050) ──────────────────────────────

    [Fact]
    public async Task GetPagedAsync_MultiSelectIncomeTypes_ReturnsAllMatchingTypes()
    {
        await _repository.AddAsync(MakeDividend("DivCo1", new DateOnly(2024, 1, 1)), TestContext.Current.CancellationToken);
        await _repository.AddAsync(MakeInterest("BankCo", new DateOnly(2024, 2, 1)), TestContext.Current.CancellationToken);
        await _repository.AddAsync(MakeDividend("DivCo2", new DateOnly(2024, 3, 1)), TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(IncomeTypes: new HashSet<IncomeType>
        {
            IncomeType.Dividend,
            IncomeType.Interest
        });

        var (items, total) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(3);
    }

    [Fact]
    public async Task GetPagedAsync_MultiSelectIncomeTypes_SingleType_ExcludesOthers()
    {
        await _repository.AddAsync(MakeDividend("DivCo", new DateOnly(2024, 1, 1)), TestContext.Current.CancellationToken);
        await _repository.AddAsync(MakeInterest("BankCo", new DateOnly(2024, 2, 1)), TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(IncomeTypes: new HashSet<IncomeType> { IncomeType.Interest });
        var (items, total) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items[0].IncomeType.Should().Be(IncomeType.Interest);
    }

    // ── FilingDeadlineText (feature 050) ────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_FilterByFilingDeadlineText_MatchesYear()
    {
        // Deadline is incomeDate + 30 days, stored in SQLite as "yyyy-MM-dd" TEXT.
        // Searching "2024" should only match the 2024 deadline.
        var f2024 = MakeDividend("Co2024", new DateOnly(2024, 1, 1)); // deadline → 2024-01-31
        var f2025 = Filing.CreateFromIncome(_profile.Id, IncomeType.Dividend, "Co2025",
            new DateOnly(2025, 1, 1), 1000m, 0m, 100m, 100m, new DateOnly(2025, 1, 31));

        await _repository.AddAsync(f2024, TestContext.Current.CancellationToken);
        await _repository.AddAsync(f2025, TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(FilingDeadlineText: "2024");
        var (items, total) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items[0].PayingEntity.Should().Be("Co2024");
    }

    [Fact]
    public async Task GetPagedAsync_FilterByFilingDeadlineText_NoMatch_ReturnsEmpty()
    {
        await _repository.AddAsync(MakeDividend("Co", new DateOnly(2024, 1, 1)), TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(FilingDeadlineText: "1900");
        var (items, total) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(0);
        items.Should().BeEmpty();
    }

    // ── PaymentReference text filter ────────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_FilterByPaymentReference_ReturnsMatchingFilings()
    {
        var fWithRef = MakeDividend("Co1", new DateOnly(2024, 1, 1));
        fWithRef.SetPaymentReference("2024-0001-ABC");
        var fNoRef = MakeDividend("Co2", new DateOnly(2024, 2, 1));

        await _repository.AddAsync(fWithRef, TestContext.Current.CancellationToken);
        await _repository.AddAsync(fNoRef, TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(PaymentReference: "0001");
        var (items, total) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items[0].PayingEntity.Should().Be("Co1");
    }

    [Fact]
    public async Task GetPagedAsync_FilterByPaymentReference_NullReferences_NotReturned()
    {
        // Filing with a null PaymentReference must not appear when a reference filter is set
        var fNoRef = MakeDividend("NoRefCo", new DateOnly(2024, 1, 1));
        // fNoRef.PaymentReference is null by default — not set
        await _repository.AddAsync(fNoRef, TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(PaymentReference: "ABC");
        var (items, total) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(0);
        items.Should().BeEmpty();
    }

    // ── Pagination edge cases ───────────────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_SkipBeyondLastPage_ReturnsEmptyItemsWithCorrectTotal()
    {
        for (var i = 0; i < 3; i++)
            await _repository.AddAsync(
                MakeDividend($"Co{i}", new DateOnly(2024, 1, i + 1)),
                TestContext.Current.CancellationToken);

        var (items, total) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 100, 10, ct: TestContext.Current.CancellationToken);

        total.Should().Be(3);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_EmptyDatabase_ReturnsZeroTotalAndEmptyItems()
    {
        var (items, total) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 10, ct: TestContext.Current.CancellationToken);

        total.Should().Be(0);
        items.Should().BeEmpty();
    }
}
