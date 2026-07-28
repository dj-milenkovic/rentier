using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.Primitives.Disposables;
using Rentier.Desktop.Extensions;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Resources;

namespace Rentier.Desktop.ViewModels;

public sealed class HolidaySettingsViewModel : ReactiveObject, IActivatableViewModel
{
    private readonly IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>> _queryHandler;
    private readonly ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>> _saveHandler;
    private readonly ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>> _fetchHandler;
    private readonly ISequencer _scheduler;

    private int _startYear;
    private int _endYear;
    private bool _isLoading;
    private string? _errorMessage;
    private string? _successMessage;
    private HolidayEntryViewModel? _selectedEntry;
    private bool _hasUnsavedChanges;

    public int StartYear { get => _startYear; set => this.RaiseAndSetIfChanged(ref _startYear, value); }
    public int EndYear { get => _endYear; set => this.RaiseAndSetIfChanged(ref _endYear, value); }
    public bool IsLoading { get => _isLoading; private set => this.RaiseAndSetIfChanged(ref _isLoading, value); }
    public string? ErrorMessage { get => _errorMessage; private set => this.RaiseAndSetIfChanged(ref _errorMessage, value); }
    public string? SuccessMessage { get => _successMessage; private set => this.RaiseAndSetIfChanged(ref _successMessage, value); }
    public HolidayEntryViewModel? SelectedEntry { get => _selectedEntry; set => this.RaiseAndSetIfChanged(ref _selectedEntry, value); }
    public bool HasUnsavedChanges { get => _hasUnsavedChanges; private set => this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value); }

    /// <summary>Returns a validation message when EndYear is less than StartYear; null otherwise.</summary>
    public string? YearRangeValidationMessage =>
        EndYear < StartYear ? Strings.Holidays_YearRange_Error : null;

    /// <summary>Returns true when the Entries collection is non-empty.</summary>
    public bool HasItems => Entries.Count > 0;

    public ObservableCollection<HolidayEntryViewModel> Entries { get; } = new();

    /// <summary>Entries filtered to the current StartYear–EndYear range. The DataGrid binds to this.</summary>
    public ObservableCollection<HolidayEntryViewModel> FilteredEntries { get; } = new();

    /// <summary>True when FilteredEntries is empty (no holidays in the selected range).</summary>
    public bool IsFilteredEmpty => FilteredEntries.Count == 0;

    public ReactiveCommand<RxVoid, RxVoid> AddRowCommand { get; }
    public ReactiveCommand<HolidayEntryViewModel, RxVoid> DeleteRowCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> SaveCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> FetchFromWebCommand { get; }

    public ViewModelActivator Activator { get; } = new();

    public HolidaySettingsViewModel(
        IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>> queryHandler,
        ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>> saveHandler,
        ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>> fetchHandler,
        ISequencer? scheduler = null)
    {
        _queryHandler = queryHandler;
        _saveHandler = saveHandler;
        _fetchHandler = fetchHandler;
        _scheduler = scheduler ?? RxSchedulers.MainThreadScheduler;

        // Keep IsFilteredEmpty in sync with FilteredEntries
        FilteredEntries.CollectionChanged += (_, _) =>
            this.RaisePropertyChanged(nameof(IsFilteredEmpty));

        // Rebuild when year range changes; also refresh year-range validation message
        this.WhenAnyValue(x => x.StartYear, x => x.EndYear)
            .Subscribe(_ =>
            {
                RebuildFilteredEntries();
                this.RaisePropertyChanged(nameof(YearRangeValidationMessage));
            });

        var notLoading = this.WhenAnyValue(x => x.IsLoading).Select(l => !l);

        // Disable Save and Fetch when the year range is invalid
        var yearRangeValid = this.WhenAnyValue(x => x.StartYear, x => x.EndYear, (s, e) => e >= s);
        var canSaveOrFetch = notLoading.CombineLatest(yearRangeValid, (nl, rv) => nl && rv);

        AddRowCommand = ReactiveCommand.Create(() =>
        {
            Entries.Add(new HolidayEntryViewModel());
            HasUnsavedChanges = true;
        }, notLoading);

        var canDelete = this.WhenAnyValue(x => x.SelectedEntry)
            .Select(e => e != null)
            .CombineLatest(notLoading, (hasEntry, nl) => hasEntry && nl);
        DeleteRowCommand = ReactiveCommand.Create<HolidayEntryViewModel>(entry =>
        {
            Entries.Remove(entry);
            HasUnsavedChanges = true;
        }, canDelete);

        SaveCommand = ReactiveCommand.CreateFromTask(OnSaveAsync, canSaveOrFetch);

        FetchFromWebCommand = ReactiveCommand.CreateFromTask(OnFetchAsync, canSaveOrFetch);

        this.WhenActivated(RegisterActivation);
    }

    private async Task OnSaveAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            var cmd = new SaveHolidayConfCommand(Entries.Select(e => e.ToDto()).ToList(), StartYear, EndYear);
            var result = await _saveHandler.HandleAsync(cmd, ct);
            if (result.IsSuccess) { SuccessMessage = Strings.Holidays_Saved_Confirmation; HasUnsavedChanges = false; }
            else { ErrorMessage = result.Error.Message; }
        }
        finally { IsLoading = false; }
    }

    private async Task OnFetchAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            var cmd = new FetchHolidaysFromWebCommand(StartYear, EndYear);
            var result = await _fetchHandler.HandleAsync(cmd, ct);
            if (result.IsSuccess)
            {
                var existingDates = Entries.Select(e => e.Date).ToHashSet();
                // Filter before ordering: sorting the discarded duplicates is wasted work (S6607).
                var newHolidays = result.Value
                    .Where(d => !existingDates.Contains(d.Date))
                    .DistinctBy(d => d.Date)
                    .OrderBy(d => d.Date)
                    .ToList();

                foreach (var dto in newHolidays)
                    Entries.Add(HolidayEntryViewModel.FromDto(dto));

                var added = newHolidays.Count;
                if (added > 0) HasUnsavedChanges = true;
                SuccessMessage = string.Format(Strings.Holidays_FetchFromWeb_Success, added, EndYear - StartYear + 1);
            }
            else { ErrorMessage = result.Error.Message; }
        }
        finally { IsLoading = false; }
    }

    private void RegisterActivation(MultipleDisposable disposables)
    {
        // Raise HasItems when collection size changes and rebuild the filtered view.
        // Managed here so the subscription is tied to the activation lifecycle.
        NotifyCollectionChangedEventHandler onEntriesChanged = (_, _) =>
        {
            this.RaisePropertyChanged(nameof(HasItems));
            RebuildFilteredEntries();
        };
        Entries.CollectionChanged += onEntriesChanged;
        new ActionDisposable(() => Entries.CollectionChanged -= onEntriesChanged)
            .DisposeWith(disposables);

        Signal.FromAsync(async ct => { await LoadAsync(ct); return RxVoid.Default; })
            .ObserveOn(_scheduler)
            .Subscribe()
            .DisposeWith(disposables);
        SaveCommand.ThrownExceptions
            .Subscribe(ex => ErrorMessage = ex.Message)
            .DisposeWith(disposables);
        FetchFromWebCommand.ThrownExceptions
            .Subscribe(ex => ErrorMessage = ex.Message)
            .DisposeWith(disposables);
    }

    private void RebuildFilteredEntries()
    {
        FilteredEntries.Clear();

        // Always include newly-added rows (DateOnly.MinValue = not yet set)
        var visible = Entries.Where(entry =>
            entry.Date == DateOnly.MinValue ||
            (entry.Date.Year >= StartYear && entry.Date.Year <= EndYear));

        foreach (var entry in visible)
            FilteredEntries.Add(entry);
    }

    private async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await _queryHandler.HandleAsync(new GetHolidayConfQuery(), ct);
            if (result.IsSuccess)
            {
                Entries.Clear();
                foreach (var dto in result.Value.Holidays) Entries.Add(HolidayEntryViewModel.FromDto(dto));
                StartYear = result.Value.StartYear;
                EndYear = result.Value.EndYear;
                HasUnsavedChanges = false;
            }
            else { ErrorMessage = result.Error.Message; }
        }
        finally { IsLoading = false; }
    }
}
