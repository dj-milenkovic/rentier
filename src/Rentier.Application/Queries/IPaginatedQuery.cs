namespace Rentier.Application.Queries;

/// <summary>
/// Contract for queries that support pagination.
/// Implementors must supply a page number (≥ 1) and a page size (1–100).
/// </summary>
public interface IPaginatedQuery
{
    int Page { get; }
    int PageSize { get; }
}
