using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Enums;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Resources;
using Rentier.Domain.Entities;

namespace Rentier.Desktop.ViewModels;

public sealed class FilingsViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private readonly IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>> _getFilings;
    private readonly ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>> _updateStatus;
    private readonly ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>> _updateReference;
    private readonly ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>> _deleteFiling;
    private readonly Func<string, Task<bool>> _confirmDelete;
    private readonly IScheduler _scheduler;

    private bool _isLoading;
    private string? _errorMessage;
    private bool _showAll;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _totalCount;

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

    public bool ShowAll
    {
        get => _showAll;
        set
        {
            this.RaiseAndSetIfChanged(ref _showAll, value);
            _currentPage = 1;
            this.RaisePropertyChanged(nameof(CurrentPage));
            LoadPageCommand.Execute().Subscribe();
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public int TotalPages
    {
        get => _totalPages;
        private set => this.RaiseAndSetIfChanged(ref _totalPages, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    public bool HasItems => Rows.Count > 0;
    public bool IsEmpty => Rows.Count == 0 && !IsLoading;
    public bool HasPreviousPage => _currentPage > 1 && !IsLoading;
    public bool HasNextPage => _currentPage < _totalPages && !IsLoading;
    public string PageIndicator => string.Format(Strings.Filings_Page_Indicator, _currentPage, _totalPages);

    public ObservableCollection<FilingRowViewModel> Rows { get; } = new();

    public ReactiveCommand<Unit, Unit> LoadPageCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousPageCommand { get; }
    public ReactiveCommand<Unit, Unit> NextPageCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearErrorCommand { get; }
    public ReactiveCommand<(Guid Id, FilingStatus NewStatus), Unit> AdvanceStatusCommand { get; }
    public ReactiveCommand<(Guid Id, string? Reference), Unit> SavePaymentRefCommand { get; }
    public ReactiveCommand<Guid, Unit> DeleteCommand { get; }

    public FilingsViewModel(
        IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>> getFilings,
        ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>> updateStatus,
        ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>> updateReference,
        ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>> deleteFiling,
        Func<string, Task<bool>> confirmDelete,
        IScheduler? scheduler = null)
    {
        _getFilings = getFilings;
        _updateStatus = updateStatus;
        _updateReference = updateReference;
        _deleteFiling = deleteFiling;
        _confirmDelete = confirmDelete;
        _scheduler = scheduler ?? RxApp.MainThreadScheduler;

        LoadPageCommand = ReactiveCommand.CreateFromTask(
            LoadPageAsync, outputScheduler: _scheduler);

        PreviousPageCommand = ReactiveCommand.CreateFromTask(
            async (CancellationToken ct) =>
            {
                _currentPage--;
                await LoadPageAsync(ct);
            },
            this.WhenAnyValue(x => x.HasPreviousPage),
            outputScheduler: _scheduler);

        NextPageCommand = ReactiveCommand.CreateFromTask(
            async (CancellationToken ct) =>
            {
                _currentPage++;
                await LoadPageAsync(ct);
            },
            this.WhenAnyValue(x => x.HasNextPage),
            outputScheduler: _scheduler);

        ClearErrorCommand = ReactiveCommand.Create(
            () => { ErrorMessage = null; },
            outputScheduler: _scheduler);

        AdvanceStatusCommand = ReactiveCommand.CreateFromTask<(Guid Id, FilingStatus NewStatus)>(
            async (args, ct) =>
            {
                var result = await _updateStatus.HandleAsync(
                    new UpdateFilingStatusCommand(args.Id, args.NewStatus), ct);
                // Preserve error across reload so the user sees it even after the page refreshes
                var errorToPreserve = result.IsSuccess ? null : result.Error.Message;
                await LoadPageAsync(ct);
                if (errorToPreserve is not null)
                    ErrorMessage = errorToPreserve;
            },
            outputScheduler: _scheduler);

        SavePaymentRefCommand = ReactiveCommand.CreateFromTask<(Guid Id, string? Reference)>(
            async (args, ct) =>
            {
                var result = await _updateReference.HandleAsync(
                    new UpdatePaymentReferenceCommand(args.Id, args.Reference), ct);
                var errorToPreserve = result.IsSuccess ? null : result.Error.Message;
                await LoadPageAsync(ct);
                if (errorToPreserve is not null)
                    ErrorMessage = errorToPreserve;
            },
            outputScheduler: _scheduler);

        DeleteCommand = ReactiveCommand.CreateFromTask<Guid>(
            async (id, ct) =>
            {
                var confirmed = await _confirmDelete(Strings.Filings_Delete_Confirmation_Message);
                if (!confirmed) return;

                var result = await _deleteFiling.HandleAsync(new DeleteFilingCommand(id), ct);
                if (!result.IsSuccess)
                {
                    ErrorMessage = result.Error.Message;
                    return;
                }

                // Decrement page when last item on a non-first page is deleted
                if (Rows.Count == 1 && _currentPage > 1)
                    _currentPage--;

                await LoadPageAsync(ct);
            },
            outputScheduler: _scheduler);

        this.WhenActivated(disposables =>
        {
            LoadPageCommand.Execute().Subscribe().DisposeWith(disposables);
        });
    }

    private async Task LoadPageAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var filter = _showAll ? FilingFilterMode.All : FilingFilterMode.Unpaid;
            var result = await _getFilings.HandleAsync(
                new GetFilingsQuery(filter, _currentPage, 20), ct);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            var page = result.Value;
            Rows.Clear();
            foreach (var dto in page.Rows)
                Rows.Add(FilingRowViewModel.From(dto));

            TotalCount = page.TotalCount;
            TotalPages = page.TotalPages;

            // Clamp current page if filter reduced total pages
            var clampedPage = Math.Min(_currentPage, page.TotalPages);
            if (clampedPage != _currentPage)
            {
                _currentPage = clampedPage;
                this.RaisePropertyChanged(nameof(CurrentPage));
            }

            this.RaisePropertyChanged(nameof(IsEmpty));
            this.RaisePropertyChanged(nameof(HasItems));
            this.RaisePropertyChanged(nameof(HasPreviousPage));
            this.RaisePropertyChanged(nameof(HasNextPage));
            this.RaisePropertyChanged(nameof(PageIndicator));
        }
        finally
        {
            IsLoading = false;
        }
    }
}
