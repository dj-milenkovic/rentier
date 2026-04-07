using Rentier.Application.Enums;

namespace Rentier.Application.Queries;

public sealed record GetFilingsQuery(
    FilingFilterMode Filter = FilingFilterMode.Unpaid,
    int Page = 1,
    int PageSize = 20,
    Guid? ReportIdFilter = null);
