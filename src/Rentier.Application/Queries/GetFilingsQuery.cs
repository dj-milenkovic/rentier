using Rentier.Application.Enums;

namespace Rentier.Application.Queries;

public sealed record GetFilingsQuery(
    FilingFilterMode Filter = FilingFilterMode.Unpaid,
    int Page = 1,
    int PageSize = 30,
    Guid? ReportIdFilter = null,
    FilingSortColumn SortColumn = FilingSortColumn.FilingDeadline,
    bool SortDescending = true);
