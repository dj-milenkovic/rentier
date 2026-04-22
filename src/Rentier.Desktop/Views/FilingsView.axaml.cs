using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

/// <summary>
/// Code-behind for FilingsView. DataGrid cell-level events (LostFocus, Sorting)
/// cannot be expressed as commands in AXAML without code-behind -- this is the accepted exception.
/// </summary>
public partial class FilingsView : ReactiveUserControl<FilingsViewModel>
{
    public FilingsView() => InitializeComponent();

    private void PaymentRef_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not FilingRowViewModel row) return;

        // Skip the DB write when the text hasn't actually changed since the row was loaded.
        if (tb.Text == row.PaymentReference) return;

        ViewModel?.SavePaymentRefCommand.Execute((row.Id, tb.Text)).Subscribe();
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
        ViewModel?.ApplySortCommand.Execute((tag, (bool?)null)).Subscribe();
    }
}
