using ReactiveUI;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Single-column sort state with the three-click header cycle:
/// ascending → descending → unsorted. A null <see cref="Column"/> means unsorted.
/// </summary>
public sealed class SortState<TColumn> : ReactiveObject
    where TColumn : struct, Enum
{
    private TColumn? _column;
    private bool _descending;

    public SortState(TColumn? column = null, bool descending = false)
    {
        _column = column;
        _descending = descending;
    }

    public TColumn? Column
    {
        get => _column;
        private set => this.RaiseAndSetIfChanged(ref _column, value);
    }

    public bool Descending
    {
        get => _descending;
        private set => this.RaiseAndSetIfChanged(ref _descending, value);
    }

    /// <summary>
    /// Applies a click on <paramref name="column"/>'s header.
    /// </summary>
    /// <returns>
    /// <c>true</c> when sorting moved to a different column — the row order is unrelated to the
    /// previous one, so the caller must go back to page 1. <c>false</c> when only the direction
    /// of the current column changed (or it was cleared) and the page can be kept.
    /// </returns>
    public bool Cycle(TColumn column)
    {
        if (!EqualityComparer<TColumn?>.Default.Equals(_column, column))
        {
            Column = column;
            Descending = false;
            return true;
        }

        if (!_descending)
        {
            Descending = true;
        }
        else
        {
            Column = null;
            Descending = false;
        }

        return false;
    }
}
