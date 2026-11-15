using ReactiveUI;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Paging state of a server-side paged list: which page is shown and how many there are.
/// <para>
/// This is the single owner of the current page. Anything that changes <em>what</em> is being
/// queried (a filter, the sort column, a scope toggle) calls <see cref="ResetToFirstPage"/>;
/// anything that changes <em>how much</em> data exists reports it through
/// <see cref="ApplyResult"/> or <see cref="StepBackIfPageEmptied"/>.
/// </para>
/// </summary>
public sealed class PagedQueryState : ReactiveObject
{
    public const int DEFAULT_PAGE_SIZE = 30;
    private const int FIRST_PAGE = 1;

    private int _currentPage = FIRST_PAGE;
    private int _totalPages = 1;
    private int _totalCount;

    public PagedQueryState(int pageSize = DEFAULT_PAGE_SIZE)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        PageSize = pageSize;
    }

    public int PageSize { get; }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (value == _currentPage) return;
            this.RaiseAndSetIfChanged(ref _currentPage, value);
            this.RaisePropertyChanged(nameof(HasPreviousPage));
            this.RaisePropertyChanged(nameof(HasNextPage));
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (value == _totalPages) return;
            this.RaiseAndSetIfChanged(ref _totalPages, value);
            this.RaisePropertyChanged(nameof(HasNextPage));
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    public bool HasPreviousPage => _currentPage > FIRST_PAGE;
    public bool HasNextPage => _currentPage < _totalPages;

    /// <summary>The query changed shape (filter, sort column, scope): start again from page 1.</summary>
    public void ResetToFirstPage() => CurrentPage = FIRST_PAGE;

    /// <summary>Moves one page back; a no-op on the first page.</summary>
    public void MoveToPreviousPage()
    {
        if (HasPreviousPage)
            CurrentPage--;
    }

    /// <summary>Moves one page forward; a no-op on the last page.</summary>
    public void MoveToNextPage()
    {
        if (HasNextPage)
            CurrentPage++;
    }

    /// <summary>
    /// Records the totals returned by a query and clamps the current page when the data
    /// shrank below it (e.g. a filter left fewer pages than the one being viewed).
    /// </summary>
    public void ApplyResult(int totalCount, int totalPages)
    {
        TotalCount = totalCount;
        TotalPages = totalPages;
        CurrentPage = Math.Min(_currentPage, totalPages);
    }

    /// <summary>
    /// Call before reloading after rows were removed: when every visible row of a non-first
    /// page is gone, steps back one page so the user does not land on an empty page.
    /// </summary>
    public void StepBackIfPageEmptied(int removedCount, int visibleCount)
    {
        if (removedCount == visibleCount)
            MoveToPreviousPage();
    }
}
