using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

public sealed class ManualFilingViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    // ── Static lists ─────────────────────────────────────────────────────────

    public static IReadOnlyList<string> AvailableCurrencies { get; } =
        ["USD", "EUR", "GBP", "CHF", "AUD", "CAD", "CZK", "DKK", "HUF", "JPY", "NOK", "PLN", "SEK", "TRY", "AED"];

    // ── Backing fields ───────────────────────────────────────────────────────

    private IncomeType       _selectedIncomeType = IncomeType.Dividend;
    private string           _ticker             = "";
    private DateTimeOffset?  _incomeDate         = null;
    private string           _selectedCurrency   = "USD";
    private string           _grossAmountText    = "";
    private string           _netReceivedText    = "";
    private ManualFilingPreviewDto? _preview      = null;
    private string?          _errorMessage       = null;
    private bool             _isLoading          = false;
    private Guid?            _taxpayerProfileId  = null;

    private readonly IScheduler _scheduler;
    private readonly ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>> _calculateHandler;
    private readonly ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>> _createHandler;
    private readonly ITaxpayerProfileRepository _profileRepository;
    private readonly Action _navigateBackToFilings;

    // ── Properties ───────────────────────────────────────────────────────────

    public IncomeType SelectedIncomeType
    {
        get => _selectedIncomeType;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedIncomeType, value);
            this.RaisePropertyChanged(nameof(IsDividend));
            this.RaisePropertyChanged(nameof(IsInterest));
        }
    }

    public string Ticker
    {
        get => _ticker;
        set => this.RaiseAndSetIfChanged(ref _ticker, value);
    }

    public DateTimeOffset? IncomeDate
    {
        get => _incomeDate;
        set => this.RaiseAndSetIfChanged(ref _incomeDate, value);
    }

    public string SelectedCurrency
    {
        get => _selectedCurrency;
        set => this.RaiseAndSetIfChanged(ref _selectedCurrency, value);
    }

    public string GrossAmountText
    {
        get => _grossAmountText;
        set => this.RaiseAndSetIfChanged(ref _grossAmountText, value);
    }

    public string NetReceivedText
    {
        get => _netReceivedText;
        set => this.RaiseAndSetIfChanged(ref _netReceivedText, value);
    }

    public ManualFilingPreviewDto? Preview
    {
        get => _preview;
        private set => this.RaiseAndSetIfChanged(ref _preview, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    // Helper properties for RadioButton binding
    public bool IsDividend
    {
        get => SelectedIncomeType == IncomeType.Dividend;
        set { if (value) SelectedIncomeType = IncomeType.Dividend; }
    }

    public bool IsInterest
    {
        get => SelectedIncomeType == IncomeType.Interest;
        set { if (value) SelectedIncomeType = IncomeType.Interest; }
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    public ReactiveCommand<Unit, Unit> CalculateCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand      { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand    { get; }
    public ReactiveCommand<Unit, Unit> ClearErrorCommand { get; }

    // ── Constructor ──────────────────────────────────────────────────────────

    public ManualFilingViewModel(
        ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>> calculateHandler,
        ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>> createHandler,
        ITaxpayerProfileRepository profileRepository,
        Action navigateBackToFilings,
        IScheduler? scheduler = null)
    {
        _calculateHandler       = calculateHandler;
        _createHandler          = createHandler;
        _profileRepository      = profileRepository;
        _navigateBackToFilings  = navigateBackToFilings;
        _scheduler              = scheduler ?? RxApp.MainThreadScheduler;

        // CalculateCommand canExecute: ticker not blank, gross > 0, date set, not loading
        var canCalculate = this.WhenAnyValue(
            x => x.Ticker,
            x => x.GrossAmountText,
            x => x.IncomeDate,
            x => x.IsLoading,
            (ticker, grossText, date, loading) =>
                !string.IsNullOrWhiteSpace(ticker) &&
                decimal.TryParse(grossText, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.CurrentCulture, out var g) && g > 0 &&
                date != null &&
                !loading);

        // SaveCommand canExecute: preview is set, not loading
        var canSave = this.WhenAnyValue(
            x => x.Preview,
            x => x.IsLoading,
            (preview, loading) => preview != null && !loading);

        CalculateCommand = ReactiveCommand.CreateFromTask(
            CalculateAsync,
            canCalculate,
            outputScheduler: _scheduler);

        SaveCommand = ReactiveCommand.CreateFromTask(
            SaveAsync,
            canSave,
            outputScheduler: _scheduler);

        CancelCommand = ReactiveCommand.Create(
            () => _navigateBackToFilings(),
            outputScheduler: _scheduler);

        ClearErrorCommand = ReactiveCommand.Create(
            () => { ErrorMessage = null; },
            outputScheduler: _scheduler);

        this.WhenActivated(disposables =>
        {
            CalculateCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            SaveCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
        });

        // Clear preview when any input changes (skip initial emission, set up in constructor so tests work)
        this.WhenAnyValue(
                x => x.Ticker,
                x => x.IncomeDate,
                x => x.SelectedCurrency,
                x => x.GrossAmountText,
                x => x.NetReceivedText,
                x => x.SelectedIncomeType)
            .Skip(1)
            .Subscribe(_ => Preview = null);

        // Load taxpayer profile (fire-and-forget; works synchronously when tests use ImmediateScheduler)
        _ = LoadProfileAsync();
    }

    // ── Command implementations ───────────────────────────────────────────────

    private async Task LoadProfileAsync()
    {
        try
        {
            var profile = await _profileRepository.GetAsync();
            if (profile == null)
                ErrorMessage = Strings.ManualFiling_Error_NoProfile;
            else
                _taxpayerProfileId = profile.Id;
        }
        catch
        {
            // Non-critical — will surface as validation error when user tries to save
        }
    }

    private async Task CalculateAsync(CancellationToken ct)
    {
        IsLoading    = true;
        ErrorMessage = null;
        try
        {
            var profileId = _taxpayerProfileId ?? Guid.Empty;
            var incomeDate = IncomeDate.HasValue
                ? DateOnly.FromDateTime(IncomeDate.Value.Date)
                : default;

            decimal? netReceived = null;
            if (!string.IsNullOrWhiteSpace(NetReceivedText) &&
                decimal.TryParse(NetReceivedText,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.CurrentCulture, out var nr))
                netReceived = nr;

            decimal.TryParse(GrossAmountText,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.CurrentCulture, out var grossAmount);

            var command = new CalculateManualFilingCommand(
                profileId,
                SelectedIncomeType,
                Ticker.Trim(),
                incomeDate,
                SelectedCurrency,
                grossAmount,
                netReceived);

            var result = await _calculateHandler.HandleAsync(command, ct);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            Preview = result.Value;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        IsLoading    = true;
        ErrorMessage = null;
        try
        {
            var profileId = _taxpayerProfileId ?? Guid.Empty;
            var incomeDate = IncomeDate.HasValue
                ? DateOnly.FromDateTime(IncomeDate.Value.Date)
                : default;

            decimal? netReceived = null;
            if (!string.IsNullOrWhiteSpace(NetReceivedText) &&
                decimal.TryParse(NetReceivedText,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.CurrentCulture, out var nr))
                netReceived = nr;

            decimal.TryParse(GrossAmountText,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.CurrentCulture, out var grossAmount);

            var command = new CreateManualFilingCommand(
                profileId,
                SelectedIncomeType,
                Ticker.Trim(),
                incomeDate,
                SelectedCurrency,
                grossAmount,
                netReceived);

            var result = await _createHandler.HandleAsync(command, ct);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            _navigateBackToFilings();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
