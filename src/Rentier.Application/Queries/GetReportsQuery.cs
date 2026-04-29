using Rentier.Application.DTOs;

namespace Rentier.Application.Queries;

/// <summary>
/// Returns a paged list of Report records as display rows with resolved importer name and filing count.
/// </summary>
public sealed record GetReportsQuery(
    int Page = 1,
    int PageSize = 30,
    bool SortDescending = true,
    ReportColumnFilter? Filter = null) : IPaginatedQuery;

