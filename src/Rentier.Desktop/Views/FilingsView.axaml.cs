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

    public FilingsView() => InitializeComponent();

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _disposables.Dispose();
    }

    private void PaymentRef_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not FilingRowViewModel row) return;

        // Skip the DB write when the text hasn't actually changed since the row was loaded.
        if (tb.Text == row.PaymentReference) return;

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
