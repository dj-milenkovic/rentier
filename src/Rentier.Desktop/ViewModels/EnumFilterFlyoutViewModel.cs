using System.Collections.ObjectModel;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using ReactiveUI;
using Rentier.Desktop.Models;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Working-copy flyout ViewModel for enum (checkbox-list) column filters.
/// <para>
/// Pattern: on open → copy committed state into working items; Apply → commit working state;
/// dismiss without Apply → working state is discarded on next open.
/// </para>
/// </summary>
/// <typeparam name="T">The enum type being filtered.</typeparam>
public sealed class EnumFilterFlyoutViewModel<T> : ReactiveObject
    where T : struct, Enum
{
    private readonly Signal<RxVoid> _applied = new();
    private bool _isOpen;
    private bool _isActive;
    private HashSet<T>? _committed; // null = all selected (no filter active)

    /// <summary>Fires each time the user clicks Apply.</summary>
    public IObservable<RxVoid> Applied => _applied.AsObservable();

    /// <summary>Whether the flyout popup is currently open.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            // Sync working copy from committed state when the flyout is opened.
            if (value && !_isOpen)
                SyncWorkingFromCommitted();
            this.RaiseAndSetIfChanged(ref _isOpen, value);
        }
    }

    /// <summary>True when a non-trivial filter is committed (not "all selected").</summary>
    public bool IsActive
    {
        get => _isActive;
        private set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    /// <summary>Working-copy checkbox items shown inside the flyout.</summary>
    public ObservableCollection<CheckableItem<T>> WorkingItems { get; }

    public ReactiveCommand<RxVoid, RxVoid> ToggleOpenCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> SelectAllCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ClearAllCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ApplyCommand { get; }

    public EnumFilterFlyoutViewModel(IEnumerable<CheckableItem<T>> items)
    {
        WorkingItems = new ObservableCollection<CheckableItem<T>>(items);

        ToggleOpenCommand = ReactiveCommand.Create(() => { IsOpen = !IsOpen; });

        SelectAllCommand = ReactiveCommand.Create(() =>
        {
            foreach (var item in WorkingItems)
                item.IsChecked = true;
        });

        ClearAllCommand = ReactiveCommand.Create(() =>
        {
            foreach (var item in WorkingItems)
                item.IsChecked = false;
        });

        ApplyCommand = ReactiveCommand.Create(() =>
        {
            var checkedValues = WorkingItems
                .Where(i => i.IsChecked)
                .Select(i => i.Value)
                .ToHashSet();

            if (checkedValues.Count == WorkingItems.Count)
            {
                // All selected → treat as "no filter"
                _committed = null;
                IsActive = false;
            }
            else
            {
                _committed = checkedValues;
                IsActive = checkedValues.Count > 0;
            }

            IsOpen = false;
            _applied.OnNext(RxVoid.Default);
        });
    }

    /// <summary>
    /// Returns the committed filter set, or <c>null</c> if all values are selected (no filter).
    /// </summary>
    public IReadOnlySet<T>? GetCommittedValues() => _committed;

    /// <summary>
    /// Clears the committed filter (resets to "all selected") and resets working items.
    /// Does NOT fire <see cref="Applied"/>; the caller is responsible for triggering a reload.
    /// </summary>
    public void Clear()
    {
        _committed = null;
        IsActive = false;
        SyncWorkingFromCommitted();
    }

    private void SyncWorkingFromCommitted()
    {
        if (_committed is null)
        {
            foreach (var item in WorkingItems)
                item.IsChecked = true;
        }
        else
        {
            foreach (var item in WorkingItems)
                item.IsChecked = _committed.Contains(item.Value);
        }
    }
}
