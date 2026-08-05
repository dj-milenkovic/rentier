using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ReactiveUI.Avalonia;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using Rentier.Desktop.Extensions;
using ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

/// <summary>
/// Code-behind for FilingsView. DataGrid cell-level events (KeyDown, Sorting)
/// cannot be expressed as commands in AXAML without code-behind -- this is the accepted exception.
/// </summary>
public partial class FilingsView : ReactiveUserControl<FilingsViewModel>
{
    private readonly MultipleDisposable _disposables = new();

    public FilingsView()
    {
        InitializeComponent();
        this.ActivateViewModelOnLoad();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _disposables.Dispose();
    }

    private void PaymentRef_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        SavePaymentRefIfChanged(sender);
    }

    // Clicking away (or tabbing off) is the natural way to finish editing a grid cell, so it
    // must save too -- relying on Enter alone silently discards the edit (issue #65).
    private void PaymentRef_LostFocus(object? sender, RoutedEventArgs e) => SavePaymentRefIfChanged(sender);

    private void SavePaymentRefIfChanged(object? sender)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not FilingRowViewModel row) return;

        // The Tag remembers the last value actually sent for this exact row, so Enter followed
        // by LostFocus (e.g. tabbing away right after saving, before the page reload refreshes
        // the row) doesn't submit the same edit twice. A virtualized cell reused for a different
        // row won't match on Id, so it falls back to comparing against the freshly loaded row.
        var lastSaved = tb.Tag is (Guid savedRowId, string savedText) && savedRowId == row.Id
            ? savedText
            : row.PaymentReference;
        if (tb.Text == lastSaved) return;

        tb.Tag = (row.Id, tb.Text);
        ViewModel?.SavePaymentRefCommand.Execute((row.Id, tb.Text)).Subscribe().DisposeWith(_disposables);
    }

    /// <summary>
    /// Routes column-sort clicks to <see cref="FilingsViewModel.ApplySortCommand"/> for server-side sorting.
    /// Setting <c>e.Handled = true</c> suppresses Avalonia's client-side sort, which would only reorder
    /// the current page and not the full dataset.
    /// </summary>
    private void DataGrid_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        var tag = e.Column.Tag as string;
        if (string.IsNullOrEmpty(tag)) return;

        e.Handled = true;
        ViewModel?.ApplySortCommand.Execute((tag, (bool?)null)).Subscribe().DisposeWith(_disposables);
    }
}
