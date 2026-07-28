using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Reactive;

namespace Rentier.Desktop.Extensions;

/// <summary>
/// Works around a bug in this project's ReactiveUI Primitives-native distribution: the View-side
/// <c>WhenActivated</c> extension never forwards the View's Loaded/Unloaded lifecycle to
/// <see cref="IActivatableViewModel.Activator"/>, even though the underlying Loaded event and
/// <c>IActivationForViewFetcher</c> observable both fire correctly (verified directly). Until that
/// library bug is fixed upstream, every View wires its ViewModel's activation manually via this
/// helper instead of <c>WhenActivated</c>.
/// </summary>
internal static class ViewActivationExtensions
{
    internal static void ActivateViewModelOnLoad<TViewModel>(this IViewFor<TViewModel> view)
        where TViewModel : class, IActivatableViewModel
    {
        if (view is not Control control) return;

        IDisposable? activation = null;
        control.Loaded += (_, _) => activation = view.ViewModel?.Activator.Activate();
        control.Unloaded += (_, _) =>
        {
            activation?.Dispose();
            activation = null;
        };
    }
}
