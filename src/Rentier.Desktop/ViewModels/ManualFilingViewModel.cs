using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Dialogs;
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

    private IncomeType _selectedIncomeType = IncomeType.Dividend;
    private string _ticker = "";
    private DateTimeOffset? _incomeDate = null;
    private string _selectedCurrency = "USD";
    private string _grossAmountText = "";
    private string _netReceivedText = "";
    private ManualFilingPreviewDto? _preview = null;
    private string? _errorMessage = null;
    private bool _isLoading = false;
    private bool _hasNoProfile = false;
    private bool _isDirty = false;
    private Guid? _taxpayerProfileId = null;

    private readonly IScheduler _scheduler;
    private readonly ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>> _calculateHandler;
    private readonly ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>> _createHandler;
    private readonly IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> _profileQueryHandler;
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
        private set
        {
            this.RaiseAndSetIfChanged(ref _preview, value);
            this.RaisePropertyChanged(nameof(RateSourceDisplay));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            this.RaisePropertyChanged(nameof(HasActualError));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    /// <summary>True when no taxpayer profile is configured; shown as an informational callout.</summary>
    public bool HasNoProfile
    {
        get => _hasNoProfile;
        private set
        {
            this.RaiseAndSetIfChanged(ref _hasNoProfile, value);
            this.RaisePropertyChanged(nameof(HasActualError));
        }
    }

    /// <summary>True only when there is an error that is not the no-profile info state.</summary>
    public bool HasActualError => ErrorMessage != null && !HasNoProfile;

    /// <summary>True once the user has modified any input field.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    /// <summary>Formatted exchange rate source string combining type (Exact/Fallback) and date.</summary>
    public string? RateSourceDisplay => Preview is null ? null :
        Preview.ExchangeRateSourceType == ExchangeRateSourceType.Exact
            ? string.Format(Strings.ManualFiling_RateSource_Exact, Preview.ExchangeRateSourceDate.ToString("yyyy-MM-dd"))
            : string.Format(Strings.ManualFiling_RateSource_Fallback, Preview.ExchangeRateSourceDate.ToString("yyyy-MM-dd"));

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
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearErrorCommand { get; }

    // ── Constructor ──────────────────────────────────────────────────────────

    public ManualFilingViewModel(
        ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>> calculateHandler,
        ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>> createHandler,
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> profileQueryHandler,
        Action navigateBackToFilings,
        IScheduler? scheduler = null)
    {
        _calculateHandler = calculateHandler;
        _createHandler = createHandler;
        _profileQueryHandler = profileQueryHandler;
        _navigateBackToFilings = navigateBackToFilings;
        _scheduler = scheduler ?? RxSchedulers.MainThreadScheduler;

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

        CancelCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (IsDirty)
            {
                var confirmed = await ConfirmDialogHelper.ShowAsync(
                    Strings.ManualFiling_Cancel_Discard_Title,
                    Strings.ManualFiling_Cancel_Discard_Message,
                    Strings.ManualFiling_Cancel_Discard_Confirm,
                    Strings.ManualFiling_Cancel_Discard_Keep);
                if (!confirmed) return;
            }
            _navigateBackToFilings();
        }, outputScheduler: _scheduler);

        ClearErrorCommand = ReactiveCommand.Create(
            () => { ErrorMessage = null; },
            outputScheduler: _scheduler);

        this.WhenActivated(disposables =>
        {
            // C3: profile load moved out of constructor to avoid fire-and-forget task
            Observable.FromAsync(ct => LoadProfileAsync(ct))
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe()
                .DisposeWith(disposables);
            CalculateCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            SaveCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
        });

        // Clear preview and mark form dirty when any input changes (skip initial emission)
        this.WhenAnyValue(
                x => x.Ticker,
                x => x.IncomeDate,
                x => x.SelectedCurrency,
                x => x.GrossAmountText,
                x => x.NetReceivedText,
                x => x.SelectedIncomeType)
            .Skip(1)
            .Subscribe(_ => { Preview = null; IsDirty = true; });
    }

    // ── Command implementations ───────────────────────────────────────────────

    private async Task LoadProfileAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _profileQueryHandler.HandleAsync(new GetTaxpayerProfileQuery(), ct);
            if (!result.IsSuccess)
                ErrorMessage = result.Error.Message;
            else if (result.Value is null)
            {
                // Show as an informational callout rather than a red error
                HasNoProfile = true;
                ErrorMessage = Strings.ManualFiling_Error_NoProfile; // kept for test compatibility
            }
            else
                _taxpayerProfileId = result.Value.Id;
        }
        catch
        {
            // Non-critical — will surface as validation error when user tries to save
        }
    }

    private async Task CalculateAsync(CancellationToken ct)
    {
        IsLoading = true;
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
        IsLoading = true;
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
