namespace Rentier.Application.Common;

/// <summary>Integer-only pagination arithmetic shared by the paged query handlers.</summary>
public static class PageMath
{
    /// <summary>
    /// Number of pages needed to show <paramref name="totalCount"/> items at
    /// <paramref name="pageSize"/> items per page — the ceiling of the division, computed
    /// without floating point. Never less than 1, so an empty result still has one page.
    /// </summary>
    /// <param name="totalCount">Total number of items; zero or negative yields one page.</param>
    /// <param name="pageSize">Items per page; must be at least 1.</param>
    public static int TotalPages(int totalCount, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        // (totalCount - 1) / pageSize + 1 cannot overflow, unlike (totalCount + pageSize - 1) / pageSize.
        return totalCount <= 0 ? 1 : ((totalCount - 1) / pageSize) + 1;
    }
}
