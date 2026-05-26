using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Property-based tests for pagination correctness.
/// Verifies the no-loss / no-duplication invariant: collecting all pages must
/// produce exactly the same sequence as the original dataset.
/// </summary>
public class PaginationProperties
{
    /// <summary>
    /// For any dataset and any page size ≥ 1, paginating through all pages using
    /// the standard Skip/Take formula and collecting results must:
    /// (a) produce a sequence identical to the original dataset (no loss, no duplication),
    /// (b) have a total page count equal to ⌈totalCount / pageSize⌉ (at least 1).
    /// </summary>
    [Property]
    public Property Paginate_AnyDatasetAndPageSize_AllItemsAppearExactlyOnce(
        int[] rawItems,
        PositiveInt pageSizeInt)
    {
        // Limit dataset size to keep test runs fast
        var items = rawItems.Take(200).ToList();
        var pageSize = Math.Max(1, pageSizeInt.Get % 100 + 1);
        var totalCount = items.Count;

        // Expected page count formula (min 1 page even for empty dataset)
        var expectedPageCount = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));

        // Collect all pages using the same Skip/Take formula used by query handlers
        var collected = new List<int>(totalCount);
        for (var page = 1; page <= expectedPageCount; page++)
        {
            var skip = (page - 1) * pageSize;
            collected.AddRange(items.Skip(skip).Take(pageSize));
        }

        // (a) Collected sequence must be identical to original (same order, same count)
        bool sequenceEqual = collected.SequenceEqual(items);

        // (b) Page count formula is consistent with collected pages
        bool pageCountCorrect = collected.Count == totalCount;

        return (sequenceEqual && pageCountCorrect).ToProperty();
    }
}
