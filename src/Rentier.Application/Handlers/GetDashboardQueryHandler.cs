using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Handlers;

public sealed class GetDashboardQueryHandler(IFilingRepository filingRepo, IMailboxRepository mailboxRepo)
    : IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>
{
    public Task<Result<DashboardDto, Error>> HandleAsync(
        GetDashboardQuery query, CancellationToken ct = default) =>
        HandlerHelper.ExecuteAsync(
            async () =>
            {
                var today = DateOnly.FromDateTime(DateTime.Today);

                // Run sequentially — SQLite is not thread-safe with EF Core
                var upcoming = await filingRepo.GetUpcomingAsync(today, 30, ct);
                var overdue = await filingRepo.GetOverdueAsync(today, ct);
                var stats = await filingRepo.GetFilingStatsAsync(ct);
                var mailboxes = await mailboxRepo.GetAllAsync(ct);

                DateOnly? lastSync = mailboxes
                    .Select(m => m.Cursor switch
                    {
                        Domain.ValueObjects.MailboxCursor.SyncedTo s => (DateOnly?)s.Date,
                        Domain.ValueObjects.MailboxCursor.NeverSynced => (DateOnly?)null,
                        _ => throw new InvalidOperationException(
                            $"Unknown MailboxCursor subtype: {m.Cursor?.GetType().Name ?? "null"}")
                    })
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
