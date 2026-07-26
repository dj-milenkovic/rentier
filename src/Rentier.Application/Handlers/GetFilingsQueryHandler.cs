using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

/// <summary>Returns a paged, filtered list of filings projected to FilingRowDto.</summary>
public sealed class GetFilingsQueryHandler
    : IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>
{
    private readonly IFilingRepository _filings;

    public GetFilingsQueryHandler(IFilingRepository filings) => _filings = filings;

    public async Task<Result<FilingsPageResult, Error>> HandleAsync(
        GetFilingsQuery query, CancellationToken ct = default)
    {
        // When ReportIdFilter is set, bypass pagination and return all linked filings
        if (query.ReportIdFilter.HasValue)
        {
            var linked = await _filings.GetByReportIdAsync(query.ReportIdFilter.Value, ct);
            var linkedRows = linked
                .Select(f => new FilingRowDto(
                    f.Id, f.Status, f.IncomeType, f.PayingEntity,
                    f.FilingDeadline, f.TaxPayableRsd, f.PaymentReference))
                .ToList()
                .AsReadOnly();
            return Result<FilingsPageResult, Error>.Success(
                new FilingsPageResult(linkedRows, linkedRows.Count, 1));
        }

        var paginationFailure = PaginationValidator.Validate<FilingsPageResult>(query);
        if (paginationFailure is not null)
            return paginationFailure;

        if (!Enum.IsDefined<Enums.FilingSortColumn>(query.SortColumn))
            return Result<FilingsPageResult, Error>.Failure(
                new Error(ErrorCodes.PAGINATION_VALIDATION_FAILED, "Invalid sort column."));

        var skip = (query.Page - 1) * query.PageSize;
        var (items, totalCount) = await _filings.GetPagedAsync(
            query.Filter, skip, query.PageSize, query.SortColumn, query.SortDescending,
            columnFilter: query.ColumnFilter, ct: ct);

        var dtos = items
            .Select(f => new FilingRowDto(
                f.Id,
                f.Status,
                f.IncomeType,
                f.PayingEntity,
                f.FilingDeadline,
                f.TaxPayableRsd,
                f.PaymentReference))
            .ToList()
            .AsReadOnly();

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / query.PageSize));

        return Result<FilingsPageResult, Error>.Success(
            new FilingsPageResult(dtos, totalCount, totalPages));
    }
}
