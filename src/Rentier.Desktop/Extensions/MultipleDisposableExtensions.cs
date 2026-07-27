using ReactiveUI.Primitives.Disposables;

namespace Rentier.Desktop.Extensions;

/// <summary>
/// Bridges <see cref="MultipleDisposable"/> -- the activation container ReactiveUI 24 hands to
/// <c>WhenActivated</c> in both distributions -- to the fluent <c>DisposeWith</c> style used
/// throughout the ViewModels.
/// </summary>
/// <remarks>
/// ReactiveUI's own <c>DisposeWith</c> lives in <c>ReactiveUI.Primitives.LinqExtensions</c>, which
/// also redefines <c>Select</c>/<c>Where</c>/<c>Skip</c>/<c>Do</c>/<c>Subscribe</c> over
/// <see cref="IObservable{T}"/>. Importing that namespace collides with <c>System.Reactive.Linq</c>,
/// which the project keeps by being on the <c>ReactiveUI.Reactive</c> compat distribution rather
/// than migrating fully to Primitives-native types (RxVoid/ISequencer/Signal). Declaring the single
/// overload we need here keeps the Rx operators unambiguously System.Reactive's; this type becomes
/// unnecessary and can be deleted if the project ever migrates to the Primitives-native distribution.
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
