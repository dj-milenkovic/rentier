using Rentier.Application.Queries;

namespace Rentier.Application.Common;

/// <summary>
/// Validates pagination parameters for any <see cref="IPaginatedQuery"/>.
/// Returns <c>null</c> when the query is valid (caller proceeds normally).
/// Returns a <see cref="Result{TValue,Error}"/> failure when validation fails (caller should return it immediately).
/// </summary>
public static class PaginationValidator
{
    /// <summary>
    /// Validates <paramref name="query"/> pagination constraints:
    /// <list type="bullet">
    ///   <item>Page must be ≥ 1.</item>
    ///   <item>PageSize must be between 1 and 100 (inclusive).</item>
    /// </list>
    /// </summary>
    /// <typeparam name="TValue">The success value type of the surrounding handler result.</typeparam>
    /// <param name="query">The paginated query to validate.</param>
    /// <returns><c>null</c> if valid; a failure result otherwise.</returns>
    public static Result<TValue, Error>? Validate<TValue>(IPaginatedQuery query)
    {
        if (query.Page < 1)
            return Result<TValue, Error>.Failure(
                new Error(ErrorCodes.PAGINATION_VALIDATION_FAILED, "Page must be >= 1."));

        if (query.PageSize < 1 || query.PageSize > 100)
            return Result<TValue, Error>.Failure(
                new Error(ErrorCodes.PAGINATION_VALIDATION_FAILED, "PageSize must be between 1 and 100."));

        return null;
    }
}
