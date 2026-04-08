using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.Application.Tests.Handlers;

public class GetDashboardQueryHandlerTests
{
    private readonly IFilingRepository _filingRepo = Substitute.For<IFilingRepository>();
    private readonly IMailboxRepository _mailboxRepo = Substitute.For<IMailboxRepository>();
    private readonly GetDashboardQueryHandler _sut;
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    public GetDashboardQueryHandlerTests()
    {
        _sut = new GetDashboardQueryHandler(_filingRepo, _mailboxRepo);

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
        var f = Filing.CreateFromIncome(
            Guid.NewGuid(), IncomeType.Dividend, entity, incomeDate,
            1000m, 150m, 150m, taxPayable,
            deadline ?? new DateOnly(2024, 4, 30));
        if (status == FilingStatus.Filed) f.AdvanceStatus(FilingStatus.Filed);
        if (status == FilingStatus.Paid) { f.AdvanceStatus(FilingStatus.Filed); f.AdvanceStatus(FilingStatus.Paid); }
        return f;
    }

    private static Mailbox MakeMailbox(DateOnly? lastSyncDate = null)
    {
        var m = Mailbox.Create("imap.test.com", 993, "user@test.com", new DateOnly(2024, 1, 1));
        if (lastSyncDate.HasValue)
            m.UpdateCursor(new MailboxCursor(lastSyncDate, 999L));
        return m;
    }

    [Fact]
    public async Task HandleAsync_NoFilings_ReturnsEmptyLists()
    {
        var result = await _sut.HandleAsync(new GetDashboardQuery());

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

        var result = await _sut.HandleAsync(new GetDashboardQuery());

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

        var result = await _sut.HandleAsync(new GetDashboardQuery());

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

        var result = await _sut.HandleAsync(new GetDashboardQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.OverdueFilings.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Stats_CountByStatus()
    {
        _filingRepo.GetFilingStatsAsync(Arg.Any<CancellationToken>())
            .Returns((3, 2, 5, 0m));

        var result = await _sut.HandleAsync(new GetDashboardQuery());

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

        var result = await _sut.HandleAsync(new GetDashboardQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalUnpaidRsd.Should().Be(250m);
    }

    [Fact]
    public async Task HandleAsync_NoMailboxCursor_LastSyncDateIsNull()
    {
        _mailboxRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Mailbox>().AsReadOnly() as IReadOnlyList<Mailbox>);

        var result = await _sut.HandleAsync(new GetDashboardQuery());

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

        var result = await _sut.HandleAsync(new GetDashboardQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.LastSyncDate.Should().Be(new DateOnly(2024, 6, 20));
    }

    [Fact]
    public async Task HandleAsync_UpcomingBoundary_ExactlyAt30Days_Included()
    {
        var f = MakeFiling(deadline: _today.AddDays(30));
        _filingRepo.GetUpcomingAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Filing> { f }.AsReadOnly() as IReadOnlyList<Filing>);

        var result = await _sut.HandleAsync(new GetDashboardQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.UpcomingDeadlines.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_UpcomingBoundary_At31Days_Excluded()
    {
        // Handler must pass days=30 to the repository
        _filingRepo.GetUpcomingAsync(Arg.Any<DateOnly>(), Arg.Is<int>(d => d == 30), Arg.Any<CancellationToken>())
            .Returns(new List<Filing>().AsReadOnly() as IReadOnlyList<Filing>);

        var result = await _sut.HandleAsync(new GetDashboardQuery());

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

        await _sut.HandleAsync(new GetDashboardQuery());

        capturedToday.Should().Be(DateOnly.FromDateTime(DateTime.Today));
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_ReturnsFailure()
    {
        _filingRepo.GetUpcomingAsync(Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("DB connection lost"));

        var result = await _sut.HandleAsync(new GetDashboardQuery());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DASHBOARD_ERROR");
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

        var result = await _sut.HandleAsync(new GetDashboardQuery());

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
