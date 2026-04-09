using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Entities;

namespace Rentier.Desktop.Views;

/// <summary>
/// Code-behind for FilingsView. DataGrid cell-level events (SelectionChanged, LostFocus, Click)
/// cannot be expressed as commands in AXAML without code-behind — this is the accepted exception.
/// </summary>
public partial class FilingsView : ReactiveUserControl<FilingsViewModel>
{
    public FilingsView() => InitializeComponent();

    private void StatusComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (e.AddedItems.Count == 0) return;
        if (cb.DataContext is not FilingRowViewModel row) return;
        if (e.AddedItems[0] is not FilingStatus newStatus) return;
        if (newStatus == row.Status) return;

        ViewModel?.AdvanceStatusCommand.Execute((row.Id, newStatus)).Subscribe();
    }

    private void PaymentRef_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not FilingRowViewModel row) return;

        // Skip the DB write when the text hasn't actually changed since the row was loaded.
        if (tb.Text == row.PaymentReference) return;

        ViewModel?.SavePaymentRefCommand.Execute((row.Id, tb.Text)).Subscribe();
    }

    private void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not Guid id) return;

        ViewModel?.DeleteCommand.Execute(id).Subscribe();
    }

    private void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not Guid id) return;

        ViewModel?.ExportCommand.Execute(id).Subscribe();
    }
}
