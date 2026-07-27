using ReactiveUI.Primitives.Disposables;

namespace Rentier.Desktop;

/// <summary>
/// Bridges <see cref="MultipleDisposable"/> -- the activation container ReactiveUI 24 hands to
/// <c>WhenActivated</c> -- to the fluent <c>DisposeWith</c> style used throughout the ViewModels.
/// </summary>
/// <remarks>
/// ReactiveUI's own <c>DisposeWith</c> lives in <c>ReactiveUI.Primitives</c>, whose
/// <c>LinqExtensions</c>/<c>SubscribeExtensions</c> collide with <c>System.Reactive.Linq</c> and
/// <c>System.ObservableExtensions</c> when both namespaces are imported. Declaring the single
/// overload we need here keeps the Rx operators unambiguously System.Reactive's.
/// </remarks>
internal static class MultipleDisposableExtensions
{
    /// <summary>
    /// Adds <paramref name="disposable"/> to <paramref name="container"/> so it is disposed when
    /// the ViewModel or View deactivates.
    /// </summary>
    internal static T DisposeWith<T>(this T disposable, MultipleDisposable container)
        where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(disposable);
        return disposable;
    }
}
