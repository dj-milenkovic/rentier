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
using Rentier.Desktop.Services;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

public sealed class ManualFilingViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    // ── Static lists ─────────────────────────────────────────────────────────

    public static IReadOnlyList<string> AvailableCurrencies => NbsCurrencies.All;

    // ── Backing fields ───────────────────────────────────────────────────────

    private IncomeType _selectedIncomeType = IncomeType.Dividend;
    private string _ticker = "";
    private DateTimeOffset? _incomeDate = null;
    private string _selectedCurrency = "USD";
    private string _grossAmountText = "";
    private string _netReceivedText = "";
    private string _paymentNotesText = PpOpoDefaults.DEFAULT_PAYMENT_NOTES;
    private ManualFilingPreviewDto? _preview = null;
    private string? _errorMessage = null;
    private bool _isLoading = false;
    private bool _hasNoProfile = false;
    private bool _isDirty = false;
    private Guid? _taxpayerProfileId = null;

    private readonly ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>> _calculateHandler;
    private readonly ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>> _createHandler;
    private readonly IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> _profileQueryHandler;
    private readonly IConfirmationService _confirmation;
    private readonly INavigationService _navigation;

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

    /// <summary>
    /// Free-text payment notes stored on the filing and exported into PP-OPO field 3.3 ("Ostalo").
    /// Pre-filled with <see cref="PpOpoDefaults.DEFAULT_PAYMENT_NOTES"/>.
    /// </summary>
    public string PaymentNotesText
    {
        get => _paymentNotesText;
        set
        {
            if (_paymentNotesText == value) return;
            this.RaiseAndSetIfChanged(ref _paymentNotesText, value);
            IsDirty = true;
        }
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
    public string? RateSourceDisplay
    {
        get
        {
            if (Preview is null) return null;

            var dateText = Preview.ExchangeRateSourceDate.ToString("yyyy-MM-dd");
            return Preview.ExchangeRateSourceType switch
            {
                ExchangeRateSourceType.Exact => string.Format(T(nameof(Strings.ManualFiling_RateSource_Exact)), dateText),
                _ => string.Format(T(nameof(Strings.ManualFiling_RateSource_Fallback)), dateText),
            };
        }
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

    public ReactiveCommand<RxVoid, RxVoid> CalculateCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> SaveCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> CancelCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ClearErrorCommand { get; }

    // ── Constructor ──────────────────────────────────────────────────────────

    private readonly ILocalizationService _loc;

    /// <summary>Resolves UI text in the user's language (neutral English without DI).</summary>
    private string T(string key) => _loc[key];

    public ManualFilingViewModel(
        ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>> calculateHandler,
        ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>> createHandler,
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> profileQueryHandler,
        IConfirmationService confirmation,
        INavigationService navigation,
        ISequencer? scheduler = null,
        ILocalizationService? localization = null)
    {
        _loc = localization ?? LocalizationService.CreateEnglish();
        _calculateHandler = calculateHandler;
        _createHandler = createHandler;
        _profileQueryHandler = profileQueryHandler;
        _confirmation = confirmation;
        _navigation = navigation;
        scheduler ??= RxSchedulers.MainThreadScheduler;

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
            outputScheduler: scheduler);

        SaveCommand = ReactiveCommand.CreateFromTask(
            SaveAsync,
            canSave,
            outputScheduler: scheduler);

        CancelCommand = ReactiveCommand.CreateFromTask(CancelAsync, outputScheduler: scheduler);

        ClearErrorCommand = ReactiveCommand.Create(
            () => { ErrorMessage = null; },
            outputScheduler: scheduler);

        this.WhenActivated((MultipleDisposable disposables) =>
        {
            // C3: profile load moved out of constructor to avoid fire-and-forget task
            Signal.FromAsync(async ct => { await LoadProfileAsync(ct); return RxVoid.Default; })
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe()
                .DisposeWith(disposables);
            CalculateCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            SaveCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            CancelCommand.ThrownExceptions
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
                ErrorMessage = T(nameof(Strings.ManualFiling_Error_NoProfile)); // kept for test compatibility
            }
            else
                _taxpayerProfileId = result.Value.Id;
        }
        catch
        {
            // Non-critical — will surface as validation error when user tries to save
        }
    }

    private async Task CancelAsync()
    {
        if (IsDirty)
        {
            var confirmed = await _confirmation.ConfirmAsync(new ConfirmationRequest(
                T(nameof(Strings.ManualFiling_Cancel_Discard_Title)),
                T(nameof(Strings.ManualFiling_Cancel_Discard_Message)),
                T(nameof(Strings.ManualFiling_Cancel_Discard_Confirm)),
                T(nameof(Strings.ManualFiling_Cancel_Discard_Keep))));
            if (!confirmed) return;
        }
        _navigation.NavigateToFilings();
    }

    /// <summary>The form's inputs parsed into the values both Calculate and Save send to Application.</summary>
    private sealed record FilingInput(Guid ProfileId, DateOnly IncomeDate, decimal GrossAmount, decimal? NetReceived);

    private FilingInput ReadInput()
    {
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

        return new FilingInput(_taxpayerProfileId ?? Guid.Empty, incomeDate, grossAmount, netReceived);
    }

    /// <summary>Shared loading/error envelope for Calculate and Save.</summary>
    private async Task RunAsync<T>(
        Func<FilingInput, Task<Result<T, Error>>> send, Action<T> onSuccess)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await send(ReadInput());
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            onSuccess(result.Value);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private Task CalculateAsync(CancellationToken ct) =>
        RunAsync(
            input => _calculateHandler.HandleAsync(
                new CalculateManualFilingCommand(
                    input.ProfileId,
                    SelectedIncomeType,
                    Ticker.Trim(),
                    input.IncomeDate,
                    SelectedCurrency,
                    input.GrossAmount,
                    input.NetReceived),
                ct),
            preview => Preview = preview);

    private Task SaveAsync(CancellationToken ct) =>
        RunAsync(
            input => _createHandler.HandleAsync(
                new CreateManualFilingCommand(
                    input.ProfileId,
                    SelectedIncomeType,
                    Ticker.Trim(),
                    input.IncomeDate,
                    SelectedCurrency,
                    input.GrossAmount,
                    input.NetReceived,
                    PaymentNotes: string.IsNullOrWhiteSpace(PaymentNotesText) ? null : PaymentNotesText.Trim()),
                ct),
            _ => _navigation.NavigateToFilings());
}
