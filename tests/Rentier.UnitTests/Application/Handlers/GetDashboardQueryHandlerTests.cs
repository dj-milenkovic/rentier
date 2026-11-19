using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.Handlers;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;
using Rentier.Tests.Common.Fakes;

namespace Rentier.UnitTests.Application.Handlers;

public class GetDashboardQueryHandlerTests
{
    private readonly IFilingRepository _filingRepo = Substitute.For<IFilingRepository>();
    private readonly IMailboxRepository _mailboxRepo = Substitute.For<IMailboxRepository>();
    private readonly GetDashboardQueryHandler _sut;
    private readonly DateOnly _today = TestClock.TODAY;

    public GetDashboardQueryHandlerTests()
    {
        _sut = new GetDashboardQueryHandler(_filingRepo, _mailboxRepo, TestClock.Provider(), NullLogger<GetDashboardQueryHandler>.Instance);

        // Default: empty returns
        _filingRepo.GetUpcomingAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Filing>().AsReadOnly() as IReadOnlyList<Filing>);
        _filingRepo.GetOverdueAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Filing>().AsReadOnly() as IReadOnlyList<Filing>);
        _filingRepo.GetFilingStatsAsync(Arg.Any<CancellationToken>())
            .Returns((0, 0, 0, 0m));
        _mailboxRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Mailbox>().AsReadOnly() as IReadOnlyList<Mailbox>);
    }

    private static Filing MakeFiling(
        FilingStatus status = FilingStatus.Init,
        DateOnly? deadline = null,
        string entity = "ACME Corp",
        decimal taxPayable = 100m)
    {
        var incomeDate = new DateOnly(2024, 3, 1);
        // grossTax == taxPayable with whtPaid == 0 satisfies the domain invariant:
        // TaxPayableRsd == Math.Max(GrossTaxPayable - WhtPaid, 0)
        var f = Filing.CreateFromIncome(
            new FilingInfo(IncomeType.Dividend, entity, incomeDate, 1000m, 0m, taxPayable, taxPayable),
            Guid.NewGuid(), deadline ?? new DateOnly(2024, 4, 30), new FilingProvenance());
        if (status == FilingStatus.Filed) f.AdvanceStatus(FilingStatus.Filed);
        if (status == FilingStatus.Paid) { f.AdvanceStatus(FilingStatus.Filed); f.AdvanceStatus(FilingStatus.Paid); }
        return f;
    }

    private static Mailbox MakeMailbox(DateOnly? lastSyncDate = null)
    {
        var m = Mailbox.Create("imap.test.com", 993, "user@test.com", TestClock.TODAY);
        // Set cursor: NeverSynced when null, SyncedTo otherwise
        MailboxCursor cursor = lastSyncDate.HasValue
            ? new MailboxCursor.SyncedTo(lastSyncDate.Value, 999L)
            : MailboxCursor.NeverSynced.Instance;
        m.UpdateCursor(cursor);
        return m;
    }

    [Fact]
    public async Task HandleAsync_NoFilings_ReturnsEmptyLists()
    {
        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.UpcomingDeadlines.Should().BeEmpty();
        result.Value.OverdueFilings.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_UpcomingFilings_ReturnedInDeadlineOrder()
    {
        var f1 = MakeFiling(deadline: _today.AddDays(10));
        var f2 = MakeFiling(deadline: _today.AddDays(5));
        var ordered = new List<Filing> { f2, f1 }.AsReadOnly() as IReadOnlyList<Filing>;
        _filingRepo.GetUpcomingAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ordered);

        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.UpcomingDeadlines.Should().HaveCount(2);
        result.Value.UpcomingDeadlines[0].FilingDeadline.Should().Be(f2.FilingDeadline);
        result.Value.UpcomingDeadlines[1].FilingDeadline.Should().Be(f1.FilingDeadline);
    }

    [Fact]
    public async Task HandleAsync_OverdueFilings_ReturnedCorrectly()
    {
        var f1 = MakeFiling(deadline: _today.AddDays(-5), entity: "Overdue Corp");
        var overdueList = new List<Filing> { f1 }.AsReadOnly() as IReadOnlyList<Filing>;
        _filingRepo.GetOverdueAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(overdueList);

        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.OverdueFilings.Should().HaveCount(1);
        result.Value.OverdueFilings[0].PayingEntity.Should().Be("Overdue Corp");
    }

    [Fact]
    public async Task HandleAsync_PaidFilingsExcluded_FromOverdue()
    {
        // Repository is responsible for filtering paid — handler maps what repo returns
        _filingRepo.GetOverdueAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Filing>().AsReadOnly() as IReadOnlyList<Filing>);

        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.OverdueFilings.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Stats_CountByStatus()
    {
        _filingRepo.GetFilingStatsAsync(Arg.Any<CancellationToken>())
            .Returns((3, 2, 5, 0m));

        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.InitCount.Should().Be(3);
        result.Value.FiledCount.Should().Be(2);
        result.Value.PaidCount.Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_Stats_TotalUnpaidExcludesPaid()
    {
        _filingRepo.GetFilingStatsAsync(Arg.Any<CancellationToken>())
            .Returns((1, 1, 1, 250m));

        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalUnpaidRsd.Should().Be(250m);
    }

    [Fact]
    public async Task HandleAsync_NoMailboxCursor_LastSyncDateIsNull()
    {
        _mailboxRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Mailbox>().AsReadOnly() as IReadOnlyList<Mailbox>);

        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.LastSyncDate.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_MultipleMailboxes_ReturnsMaxLastSyncDate()
    {
        var older = MakeMailbox(new DateOnly(2024, 1, 15));
        var newer = MakeMailbox(new DateOnly(2024, 6, 20));
        var noSync = MakeMailbox(null);
        _mailboxRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Mailbox> { older, noSync, newer }.AsReadOnly() as IReadOnlyList<Mailbox>);

        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.LastSyncDate.Should().Be(new DateOnly(2024, 6, 20));
    }

    [Fact]
    public async Task HandleAsync_UpcomingBoundary_ExactlyAt30Days_Included()
    {
        var f = MakeFiling(deadline: _today.AddDays(30));
        _filingRepo.GetUpcomingAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Filing> { f }.AsReadOnly() as IReadOnlyList<Filing>);

        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.UpcomingDeadlines.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_UpcomingBoundary_At31Days_Excluded()
    {
        // Handler must pass days=30 to the repository
        _filingRepo.GetUpcomingAsync(Arg.Any<DateOnly>(), Arg.Is<int>(d => d == 30), Arg.Any<CancellationToken>())
            .Returns(new List<Filing>().AsReadOnly() as IReadOnlyList<Filing>);

        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.UpcomingDeadlines.Should().BeEmpty();
        await _filingRepo.Received(1).GetUpcomingAsync(
            Arg.Any<DateOnly>(), 30, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OverdueBoundary_TodayNotOverdue()
    {
        // Handler must pass today to the repo; repo excludes today (FilingDeadline < today)
        DateOnly? capturedToday = null;
        _filingRepo.GetOverdueAsync(Arg.Do<DateOnly>(d => capturedToday = d), Arg.Any<CancellationToken>())
            .Returns(new List<Filing>().AsReadOnly() as IReadOnlyList<Filing>);

        await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        capturedToday.Should().Be(_today);
    }

    // ── "today" used for deadline logic is the LOCAL calendar date ──────────────

    private async Task<(DateOnly Overdue, DateOnly Upcoming)> CaptureTodayAsync(
        DateTimeOffset utcNow, TimeSpan localOffset)
    {
        DateOnly? overdueToday = null;
        DateOnly? upcomingToday = null;
        _filingRepo.GetOverdueAsync(Arg.Do<DateOnly>(d => overdueToday = d), Arg.Any<CancellationToken>())
            .Returns(new List<Filing>().AsReadOnly() as IReadOnlyList<Filing>);
        _filingRepo.GetUpcomingAsync(
                Arg.Do<DateOnly>(d => upcomingToday = d), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Filing>().AsReadOnly() as IReadOnlyList<Filing>);
        var sut = new GetDashboardQueryHandler(
            _filingRepo, _mailboxRepo,
            new FixedTimeProvider(utcNow, FixedTimeProvider.FixedOffsetZone(localOffset)), NullLogger<GetDashboardQueryHandler>.Instance);

        await sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        return (overdueToday!.Value, upcomingToday!.Value);
    }

    [Fact]
    public async Task HandleAsync_LocalDateAheadOfUtc_OverdueUsesLocalDate()
    {
        // 2025-03-31 23:30 UTC is 2025-04-01 01:30 in Serbia (UTC+2, summer time): a filing
        // due 2025-03-31 is already overdue for the taxpayer although the UTC date says otherwise.
        var (overdue, _) = await CaptureTodayAsync(
            new DateTimeOffset(2025, 3, 31, 23, 30, 0, TimeSpan.Zero), TimeSpan.FromHours(2));

        overdue.Should().Be(new DateOnly(2025, 4, 1));
    }

    [Fact]
    public async Task HandleAsync_LocalDateBehindUtc_OverdueUsesLocalDate()
    {
        // 2025-04-01 02:00 UTC is still 2025-03-31 at UTC-5: a filing due 2025-03-31 is not overdue yet.
        var (overdue, _) = await CaptureTodayAsync(
            new DateTimeOffset(2025, 4, 1, 2, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(-5));

        overdue.Should().Be(new DateOnly(2025, 3, 31));
    }

    [Fact]
    public async Task HandleAsync_UpcomingAndOverdue_UseTheSameToday()
    {
        var (overdue, upcoming) = await CaptureTodayAsync(
            new DateTimeOffset(2025, 3, 31, 23, 30, 0, TimeSpan.Zero), TimeSpan.FromHours(2));

        upcoming.Should().Be(overdue);
    }

    [Fact]
    public async Task HandleAsync_LocalNewYearAlreadyPassedButUtcHasNot_TodayIsJanuaryFirst()
    {
        // 2025-12-31 23:30 UTC is 2026-01-01 00:30 in Serbia (UTC+1, winter time).
        var (overdue, _) = await CaptureTodayAsync(
            new DateTimeOffset(2025, 12, 31, 23, 30, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

        overdue.Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public async Task HandleAsync_LastLocalSecondOfYear_TodayIsDecemberThirtyFirst()
    {
        var (overdue, _) = await CaptureTodayAsync(
            new DateTimeOffset(2025, 12, 31, 22, 59, 59, TimeSpan.Zero), TimeSpan.FromHours(1));

        overdue.Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_ReturnsFailure()
    {
        _filingRepo.GetUpcomingAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("DB connection lost"));

        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DASHBOARD_QUERY_FAILED");
        result.Error.Message.Should().Contain("DB connection lost");
    }

    [Fact]
    public async Task HandleAsync_Maps_FilingFieldsToDto_Correctly()
    {
        var deadline = new DateOnly(2024, 5, 15);
        var f = MakeFiling(status: FilingStatus.Init, deadline: deadline,
            entity: "Test Entity", taxPayable: 350m);
        _filingRepo.GetUpcomingAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Filing> { f }.AsReadOnly() as IReadOnlyList<Filing>);

        var result = await _sut.HandleAsync(new GetDashboardQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.UpcomingDeadlines[0];
        dto.Id.Should().Be(f.Id);
        dto.PayingEntity.Should().Be("Test Entity");
        dto.FilingDeadline.Should().Be(deadline);
        dto.TaxPayableRsd.Should().Be(350m);
        dto.Status.Should().Be(FilingStatus.Init);
        dto.IncomeType.Should().Be(IncomeType.Dividend);
    }
}
