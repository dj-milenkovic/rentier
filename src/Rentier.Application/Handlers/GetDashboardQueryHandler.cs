using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

public sealed class GetDashboardQueryHandler
    : IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>
{
    private readonly IFilingRepository _filingRepo;
    private readonly IMailboxRepository _mailboxRepo;

    public GetDashboardQueryHandler(IFilingRepository filingRepo, IMailboxRepository mailboxRepo)
    {
        _filingRepo = filingRepo;
        _mailboxRepo = mailboxRepo;
    }

    public Task<Result<DashboardDto, Error>> HandleAsync(
        GetDashboardQuery query, CancellationToken ct = default) =>
        HandlerHelper.ExecuteAsync<DashboardDto>(
            async () =>
            {
                var today = DateOnly.FromDateTime(DateTime.Today);

                // Run sequentially — SQLite is not thread-safe with EF Core
                var upcoming = await _filingRepo.GetUpcomingAsync(today, 30, ct);
                var overdue = await _filingRepo.GetOverdueAsync(today, ct);
                var stats = await _filingRepo.GetFilingStatsAsync(ct);
                var mailboxes = await _mailboxRepo.GetAllAsync(ct);

                DateOnly? lastSync = mailboxes
                    .Select(m => m.Cursor is Domain.ValueObjects.MailboxCursor.SyncedTo s ? s.Date : (DateOnly?)null)
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .OrderByDescending(d => d)
                    .Cast<DateOnly?>()
                    .FirstOrDefault();

                var upcomingDtos = upcoming.Select(f => new UpcomingDeadlineDto(
                    f.Id, f.PayingEntity, f.FilingDeadline, f.TaxPayableRsd, f.Status, f.IncomeType)).ToList();

                var overdueDtos = overdue.Select(f => new OverdueFilingDto(
                    f.Id, f.PayingEntity, f.FilingDeadline, f.TaxPayableRsd, f.Status)).ToList();

                return Result<DashboardDto, Error>.Success(new DashboardDto(
                    upcomingDtos, overdueDtos,
                    stats.InitCount, stats.FiledCount, stats.PaidCount, stats.TotalUnpaidRsd,
                    lastSync));
            },
            ErrorCodes.DASHBOARD_QUERY_FAILED);
}
