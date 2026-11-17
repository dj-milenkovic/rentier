using System.ComponentModel;
using ReactiveUI.Primitives;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// What a paged list needs to know about a column filter flyout, regardless of whether it
/// filters by text or by enum values.
/// </summary>
public interface IColumnFilter : INotifyPropertyChanged
{
    /// <summary>Fires each time the user commits the filter (clicks Apply).</summary>
    IObservable<RxVoid> Applied { get; }

    /// <summary>True when a filter value is committed.</summary>
    bool IsActive { get; }

    /// <summary>Clears the committed filter without firing <see cref="Applied"/>.</summary>
    void Clear();
}
