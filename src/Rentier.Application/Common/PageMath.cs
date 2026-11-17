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

    /// <summary>
    /// True when the zero-based row offset of <paramref name="page"/> fits in an
    /// <see cref="int"/>, i.e. <see cref="Skip"/> will not overflow.
    /// </summary>
    public static bool IsOffsetInRange(int page, int pageSize)
        => page >= 1 && pageSize >= 1 && ((long)page - 1) * pageSize <= int.MaxValue;

    /// <summary>
    /// Number of rows to skip to reach the first row of <paramref name="page"/> (1-based).
    /// Checked arithmetic: callers validate with <see cref="IsOffsetInRange"/> first, so an
    /// overflow here is a programming error and throws rather than wrapping to a negative skip.
    /// </summary>
    public static int Skip(int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        return checked((page - 1) * pageSize);
    }
}
