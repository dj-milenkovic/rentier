using FluentAssertions;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests;

public class PagedQueryStateTests
{
    private static PagedQueryState OnPage(int page, int totalPages)
    {
        var state = new PagedQueryState();
        state.ApplyResult(totalCount: totalPages * PagedQueryState.DEFAULT_PAGE_SIZE, totalPages);
        for (var i = 1; i < page; i++)
            state.MoveToNextPage();
        return state;
    }

    [Fact]
    public void Constructor_Default_StartsOnFirstOfOnePage()
    {
        var state = new PagedQueryState();

        state.Should().BeEquivalentTo(new
        {
            CurrentPage = 1,
            TotalPages = 1,
            TotalCount = 0,
            PageSize = PagedQueryState.DEFAULT_PAGE_SIZE,
            HasPreviousPage = false,
            HasNextPage = false,
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositivePageSize_Throws(int pageSize)
    {
        var act = () => new PagedQueryState(pageSize);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MoveToNextPage_NotOnLastPage_AdvancesOnePage()
    {
        var state = OnPage(1, totalPages: 3);

        state.MoveToNextPage();

        state.CurrentPage.Should().Be(2);
    }

    [Fact]
    public void MoveToNextPage_OnLastPage_StaysOnLastPage()
    {
        var state = OnPage(3, totalPages: 3);

        state.MoveToNextPage();

        state.CurrentPage.Should().Be(3);
    }

    [Fact]
    public void MoveToPreviousPage_OnFirstPage_StaysOnFirstPage()
    {
        var state = OnPage(1, totalPages: 3);

        state.MoveToPreviousPage();

        state.CurrentPage.Should().Be(1);
    }

    [Fact]
    public void MoveToPreviousPage_OnLaterPage_GoesBackOnePage()
    {
        var state = OnPage(3, totalPages: 3);

        state.MoveToPreviousPage();

        state.CurrentPage.Should().Be(2);
    }

    [Fact]
    public void ResetToFirstPage_OnLaterPage_ReturnsToPageOne()
    {
        var state = OnPage(3, totalPages: 3);

        state.ResetToFirstPage();

        state.CurrentPage.Should().Be(1);
    }

    [Fact]
    public void ApplyResult_FewerPagesThanCurrent_ClampsToLastPage()
    {
        var state = OnPage(3, totalPages: 3);

        state.ApplyResult(totalCount: 31, totalPages: 2);

        state.CurrentPage.Should().Be(2);
    }

    [Fact]
    public void ApplyResult_MorePages_KeepsCurrentPageAndRecordsTotals()
    {
        var state = OnPage(2, totalPages: 2);

        state.ApplyResult(totalCount: 95, totalPages: 4);

        state.Should().BeEquivalentTo(new { CurrentPage = 2, TotalPages = 4, TotalCount = 95, HasNextPage = true });
    }

    [Fact]
    public void StepBackIfPageEmptied_AllVisibleRowsRemovedOnLaterPage_GoesBackOnePage()
    {
        var state = OnPage(2, totalPages: 2);

        state.StepBackIfPageEmptied(removedCount: 3, visibleCount: 3);

        state.CurrentPage.Should().Be(1);
    }

    [Fact]
    public void StepBackIfPageEmptied_SomeRowsRemain_KeepsPage()
    {
        var state = OnPage(2, totalPages: 2);

        state.StepBackIfPageEmptied(removedCount: 1, visibleCount: 3);

        state.CurrentPage.Should().Be(2);
    }

    [Fact]
    public void StepBackIfPageEmptied_AllRowsRemovedOnFirstPage_StaysOnFirstPage()
    {
        var state = OnPage(1, totalPages: 1);

        state.StepBackIfPageEmptied(removedCount: 2, visibleCount: 2);

        state.CurrentPage.Should().Be(1);
    }

    [Fact]
    public void CurrentPage_WhenChanged_RaisesNavigabilityNotifications()
    {
        var state = OnPage(1, totalPages: 3);
        var raised = new List<string?>();
        state.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        state.MoveToNextPage();

        raised.Should().Contain([
            nameof(PagedQueryState.CurrentPage),
            nameof(PagedQueryState.HasPreviousPage),
            nameof(PagedQueryState.HasNextPage),
        ]);
    }

    [Fact]
    public void ResetToFirstPage_AlreadyOnFirstPage_RaisesNoNotification()
    {
        var state = OnPage(1, totalPages: 3);
        var raised = new List<string?>();
        state.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        state.ResetToFirstPage();

        raised.Should().BeEmpty();
    }
}
