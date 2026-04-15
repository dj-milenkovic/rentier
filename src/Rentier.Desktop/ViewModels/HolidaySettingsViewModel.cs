using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
    private readonly IScheduler _scheduler;

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

    /// <summary>Returns true when the Entries collection is non-empty.</summary>
    public bool HasItems => Entries.Count > 0;

    public ObservableCollection<HolidayEntryViewModel> Entries { get; } = new();

    /// <summary>Entries filtered to the current StartYear–EndYear range. The DataGrid binds to this.</summary>
    public ObservableCollection<HolidayEntryViewModel> FilteredEntries { get; } = new();

    /// <summary>True when FilteredEntries is empty (no holidays in the selected range).</summary>
    public bool IsFilteredEmpty => FilteredEntries.Count == 0;

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddRowCommand { get; }
    public ReactiveCommand<HolidayEntryViewModel, System.Reactive.Unit> DeleteRowCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> FetchFromWebCommand { get; }

    public ViewModelActivator Activator { get; } = new();

    public HolidaySettingsViewModel(
        IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>> queryHandler,
        ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>> saveHandler,
        ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>> fetchHandler,
        IScheduler? scheduler = null)
    {
        _queryHandler = queryHandler;
        _saveHandler = saveHandler;
        _fetchHandler = fetchHandler;
        _scheduler = scheduler ?? RxApp.MainThreadScheduler;

        // Raise HasItems when collection size changes; also rebuild the filtered view
        Entries.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(HasItems));
            RebuildFilteredEntries();
        };

        // Keep IsFilteredEmpty in sync with FilteredEntries
        FilteredEntries.CollectionChanged += (_, _) =>
            this.RaisePropertyChanged(nameof(IsFilteredEmpty));

        // Rebuild when year range changes
        this.WhenAnyValue(x => x.StartYear, x => x.EndYear)
            .Subscribe(_ => RebuildFilteredEntries());

        var notLoading = this.WhenAnyValue(x => x.IsLoading).Select(l => !l);

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

        SaveCommand = ReactiveCommand.CreateFromTask(async (CancellationToken ct) =>
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
        }, notLoading);

        FetchFromWebCommand = ReactiveCommand.CreateFromTask(async (CancellationToken ct) =>
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
                    int added = 0;
                    foreach (var dto in result.Value.OrderBy(d => d.Date))
                    {
                        if (existingDates.Add(dto.Date))
                        {
                            Entries.Add(HolidayEntryViewModel.FromDto(dto));
                            added++;
                        }
                    }
                    if (added > 0) HasUnsavedChanges = true;
                    SuccessMessage = string.Format(Strings.Holidays_FetchFromWeb_Success, added, EndYear - StartYear + 1);
                }
                else { ErrorMessage = result.Error.Message; }
            }
            finally { IsLoading = false; }
        }, notLoading);

        this.WhenActivated(disposables =>
        {
            Observable.FromAsync(ct => LoadAsync(ct))
                .ObserveOn(_scheduler)
                .Subscribe()
                .DisposeWith(disposables);
            SaveCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            FetchFromWebCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
        });
    }

    private void RebuildFilteredEntries()
    {
        FilteredEntries.Clear();
        foreach (var entry in Entries)
        {
            // Always include newly-added rows (DateOnly.MinValue = not yet set)
            if (entry.Date == DateOnly.MinValue ||
                (entry.Date.Year >= StartYear && entry.Date.Year <= EndYear))
                FilteredEntries.Add(entry);
        }
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
