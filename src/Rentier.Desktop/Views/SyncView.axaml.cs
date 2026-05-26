using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class SyncView : ReactiveUserControl<SyncViewModel>
{
    public SyncView()
    {
        InitializeComponent();

        // Auto-scroll the log to the bottom as new entries are appended
        var scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
        if (scrollViewer is not null)
        {
            scrollViewer.ScrollChanged += (_, _) =>
            {
                // Only auto-scroll if the user is already near the bottom
                scrollViewer.ScrollToEnd();
            };
        }
    }
}
