using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Reactive.Concurrency;
using ReactiveUI;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Resources;
using System.Reactive.Disposables.Fluent;

namespace Rentier.Desktop.ViewModels;

public sealed class DashboardViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private readonly IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>> _handler;
    private readonly Action _navigateToFilings;
    private readonly IScheduler _scheduler;

    private bool _isLoading;
    private string? _errorMessage;
    private bool _hasData;
    private bool _hasUpcomingDeadlines;
    private ObservableCollection<UpcomingDeadlineDto> _upcomingDeadlines = [];
    private ObservableCollection<OverdueFilingDto> _overdueFilings = [];
    private int _initCount;
    private int _filedCount;
    private int _paidCount;
    private string _totalUnpaidDisplay = string.Empty;
    private string _lastSyncDisplay = string.Empty;

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool HasData
    {
        get => _hasData;
        private set => this.RaiseAndSetIfChanged(ref _hasData, value);
    }

    public bool HasUpcomingDeadlines
    {
        get => _hasUpcomingDeadlines;
        private set => this.RaiseAndSetIfChanged(ref _hasUpcomingDeadlines, value);
    }

    public ObservableCollection<UpcomingDeadlineDto> UpcomingDeadlines
    {
        get => _upcomingDeadlines;
        private set => this.RaiseAndSetIfChanged(ref _upcomingDeadlines, value);
    }

    public ObservableCollection<OverdueFilingDto> OverdueFilings
    {
        get => _overdueFilings;
        private set => this.RaiseAndSetIfChanged(ref _overdueFilings, value);
    }

    public int InitCount
    {
        get => _initCount;
        private set => this.RaiseAndSetIfChanged(ref _initCount, value);
    }

    public int FiledCount
    {
        get => _filedCount;
        private set => this.RaiseAndSetIfChanged(ref _filedCount, value);
    }

    public int PaidCount
    {
        get => _paidCount;
        private set => this.RaiseAndSetIfChanged(ref _paidCount, value);
    }

    public string TotalUnpaidDisplay
    {
        get => _totalUnpaidDisplay;
        private set => this.RaiseAndSetIfChanged(ref _totalUnpaidDisplay, value);
    }

    public string LastSyncDisplay
    {
        get => _lastSyncDisplay;
        private set => this.RaiseAndSetIfChanged(ref _lastSyncDisplay, value);
    }

    public bool HasOverdueFilings => _overdueFilings.Count > 0;

    public ReactiveCommand<Unit, Unit> LoadCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToFilingsCommand { get; }

    public DashboardViewModel(
        IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>> handler,
        Action navigateToFilings,
        IScheduler? scheduler = null)
    {
        _handler = handler;
        _navigateToFilings = navigateToFilings;
        _scheduler = scheduler ?? RxSchedulers.MainThreadScheduler;

        LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync, outputScheduler: _scheduler);
        NavigateToFilingsCommand = ReactiveCommand.Create(
            () => _navigateToFilings(), outputScheduler: _scheduler);

        this.WhenActivated(disposables =>
        {
            LoadCommand.Execute().Subscribe().DisposeWith(disposables);
            LoadCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
        });
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;

        var result = await _handler.HandleAsync(new GetDashboardQuery(), ct);

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error.Message;
            HasData = false;
        }
        else
        {
            var dto = result.Value;
            UpcomingDeadlines = new ObservableCollection<UpcomingDeadlineDto>(dto.UpcomingDeadlines);
            OverdueFilings = new ObservableCollection<OverdueFilingDto>(dto.OverdueFilings);
            HasUpcomingDeadlines = dto.UpcomingDeadlines.Count > 0;
            InitCount = dto.InitCount;
            FiledCount = dto.FiledCount;
            PaidCount = dto.PaidCount;
            TotalUnpaidDisplay = dto.TotalUnpaidRsd.ToString("N2", CultureInfo.InvariantCulture) + " RSD";
            LastSyncDisplay = dto.LastSyncDate.HasValue
                ? dto.LastSyncDate.Value.ToString("yyyy-MM-dd")
                : Strings.Dashboard_LastSyncNever;
            HasData = true;
            this.RaisePropertyChanged(nameof(HasOverdueFilings));
        }

        IsLoading = false;
    }
}
