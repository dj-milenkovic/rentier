using Avalonia.Controls;
using ReactiveUI.Avalonia.Reactive;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class SyncView : ReactiveUserControl<SyncViewModel>
{
    public SyncView()
    {
        InitializeComponent();
        this.ActivateViewModelOnLoad();

        // Accepted code-behind: Avalonia does not provide a built-in attached behavior for
        // conditional auto-scroll without significantly more infrastructure. The lambda
        // captures only the scroll viewer by closure and contains no business logic.
        var scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
        if (scrollViewer is not null)
        {
            scrollViewer.ScrollChanged += (_, _) =>
            {
                const double threshold = 50.0;
                // Only auto-scroll when the user is at or near the bottom, so a manual
                // scroll upward to review earlier log entries is not disrupted by new ones.
                if (scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - threshold)
                    scrollViewer.ScrollToEnd();
            };
        }
    }
}
