using System.Collections.ObjectModel;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using Rentier.Desktop.Extensions;
using ReactiveUI;
using Rentier.Desktop.Resources;

namespace Rentier.Desktop.ViewModels;

public abstract class PagedSelectionViewModelBase<TRow> : ReactiveObject, IActivatableViewModel
    where TRow : ReactiveObject, ISelectableRow
{
    public ViewModelActivator Activator { get; } = new();

    protected readonly ISequencer _scheduler;
    private int _selectedCount;
    private bool _isUpdatingSelection;
    protected readonly MultipleDisposable _rowSubscriptions = new();

    private readonly ObservableAsPropertyHelper<bool> _hasSelection;
    private readonly ObservableAsPropertyHelper<string> _deleteSelectedLabel;

    public ObservableCollection<TRow> Rows { get; } = new();

    public int SelectedCount
    {
        get => _selectedCount;
        protected set => this.RaiseAndSetIfChanged(ref _selectedCount, value);
    }

    public bool HasSelection => _hasSelection.Value;
    public string DeleteSelectedLabel => _deleteSelectedLabel.Value;
    public bool HasItems => Rows.Count > 0;

    public ReactiveCommand<RxVoid, RxVoid> SelectAllCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ClearSelectionCommand { get; }

    public bool? IsAllSelected
    {
        get
        {
            if (Rows.Count == 0 || _selectedCount == 0) return false;
            if (_selectedCount == Rows.Count) return true;
            return null; // indeterminate
        }
        set
        {
            if (_isUpdatingSelection) return;
            _isUpdatingSelection = true;
            try
            {
                if (value == true)
                    SelectAllCommand.Execute().Subscribe();
                else if (value == false)
                    ClearSelectionCommand.Execute().Subscribe();
            }
            finally
            {
                _isUpdatingSelection = false;
                this.RaisePropertyChanged(nameof(IsAllSelected));
            }
        }
    }

    protected PagedSelectionViewModelBase(ISequencer scheduler)
    {
        _scheduler = scheduler;

        // Unsafe (reflection) overloads throughout this type: it is generic in TRow, which the
        // ReactiveUI.Binding source generator cannot intercept (RXUIBIND016).
        _hasSelection = this.WhenAnyValueUnsafe(x => x.SelectedCount)
            .Select(c => c > 0)
            .ToPropertyUnsafe(this, x => x.HasSelection, scheduler: _scheduler);

        _deleteSelectedLabel = this.WhenAnyValueUnsafe(x => x.SelectedCount)
            .Select(c => string.Format(Strings.BulkDelete_Button_Template, c))
            .ToPropertyUnsafe(this, x => x.DeleteSelectedLabel,
                initialValue: string.Format(Strings.BulkDelete_Button_Template, 0),
                scheduler: _scheduler);

        var hasItemsObservable = this.WhenAnyValueUnsafe(x => x.HasItems);
        var hasSelectionObservable = this.WhenAnyValueUnsafe(x => x.HasSelection);

        SelectAllCommand = ReactiveCommand.Create(
            () =>
            {
                foreach (var row in Rows)
                    row.IsSelected = true;
            },
            hasItemsObservable,
            outputScheduler: _scheduler);

        ClearSelectionCommand = ReactiveCommand.Create(
            () =>
            {
                foreach (var row in Rows)
                    row.IsSelected = false;
            },
            hasSelectionObservable,
            outputScheduler: _scheduler);
    }

    protected void RebuildRowSubscriptions()
    {
        _rowSubscriptions.Clear();
        foreach (var row in Rows)
        {
            // Reflection-based overload: ReactiveUI.Binding 9.1's source generator emits an
            // invalid `class, ReactiveObject` constraint (CS0450) when the sender is the
            // generic TRow, so the generated WhenAnyValue can't be used here.
            row.WhenAnyValueUnsafe(r => r.IsSelected)
                .Skip(1)
                .Subscribe(isNowSelected =>
                {
                    SelectedCount += isNowSelected ? 1 : -1;
                    this.RaisePropertyChanged(nameof(IsAllSelected));
                })
                .DisposeWith(_rowSubscriptions);
        }
        SelectedCount = Rows.Count(r => r.IsSelected);
        this.RaisePropertyChanged(nameof(IsAllSelected));
    }
}
