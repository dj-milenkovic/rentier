using System.Reactive.Linq;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Reactive;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Resources;

namespace Rentier.Desktop.ViewModels;

public sealed class ProfileSettingsViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();
    private readonly ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>> _saveHandler;
    private readonly IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> _getHandler;

    private string _jmbg = string.Empty;
    private string _fullName = string.Empty;
    private string _address = string.Empty;
    private string _opstinaCode = string.Empty;
    private string _phoneNumber = string.Empty;
    private string _email = string.Empty;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;

    public string Jmbg
    {
        get => _jmbg;
        set => this.RaiseAndSetIfChanged(ref _jmbg, value);
    }

    public string FullName
    {
        get => _fullName;
        set => this.RaiseAndSetIfChanged(ref _fullName, value);
    }

    public string Address
    {
        get => _address;
        set => this.RaiseAndSetIfChanged(ref _address, value);
    }

    public string OpstinaCode
    {
        get => _opstinaCode;
        set => this.RaiseAndSetIfChanged(ref _opstinaCode, value);
    }

    public string PhoneNumber
    {
        get => _phoneNumber;
        set => this.RaiseAndSetIfChanged(ref _phoneNumber, value);
    }

    public string Email
    {
        get => _email;
        set => this.RaiseAndSetIfChanged(ref _email, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public string SuccessMessage
    {
        get => _successMessage;
        private set => this.RaiseAndSetIfChanged(ref _successMessage, value);
    }

    /// <summary>Returns a validation error when JMBG is non-empty but not exactly 13 digits.</summary>
    public string? JmbgValidationMessage =>
        !string.IsNullOrEmpty(Jmbg) && (Jmbg.Length != 13 || !Jmbg.All(char.IsDigit))
            ? Strings.Profile_JmbgValidation_Error
            : null;

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveCommand { get; }

    public ProfileSettingsViewModel(
        ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>> saveHandler,
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> getHandler)
    {
        _saveHandler = saveHandler;
        _getHandler = getHandler;

        // canExecute: Jmbg must be exactly 13 digits and required fields non-empty
        var canSave = this.WhenAnyValue(
                x => x.Jmbg,
                x => x.FullName,
                x => x.Address,
                x => x.OpstinaCode,
                x => x.IsLoading,
                (jmbg, fullName, address, opstina, loading) =>
                    !loading &&
                    !string.IsNullOrWhiteSpace(jmbg) && jmbg.Length == 13 && jmbg.All(char.IsDigit) &&
                    !string.IsNullOrWhiteSpace(fullName) &&
                    !string.IsNullOrWhiteSpace(address) &&
                    !string.IsNullOrWhiteSpace(opstina))
            .DistinctUntilChanged();

        SaveCommand = ReactiveCommand.CreateFromTask(ExecuteSaveAsync, canSave);

        // Notify view of inline JMBG validation as the user types
        this.WhenAnyValue(x => x.Jmbg)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(JmbgValidationMessage)));

        // Clear the save-confirmation banner whenever the user edits any field
        this.WhenAnyValue(x => x.Jmbg, x => x.FullName, x => x.Address, x => x.OpstinaCode, x => x.PhoneNumber)
            .Skip(1)
            .Subscribe(_ => SuccessMessage = string.Empty);

        this.WhenActivated((MultipleDisposable disposables) =>
        {
            Observable.FromAsync(ct => LoadAsync(ct))
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe()
                .DisposeWith(disposables);
            SaveCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
        });
    }

    private async Task ExecuteSaveAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var command = new SaveTaxpayerProfileCommand(
                Jmbg,
                FullName,
                Address,
                OpstinaCode,
                string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber,
                string.IsNullOrWhiteSpace(Email) ? null : Email);

            var result = await _saveHandler.HandleAsync(command);

            if (result.IsSuccess)
                SuccessMessage = Strings.Profile_Saved_Confirmation;
            else
                ErrorMessage = result.Error.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var result = await _getHandler.HandleAsync(new GetTaxpayerProfileQuery(), ct);
            if (result.IsSuccess && result.Value is { } dto)
            {
                Jmbg = dto.Jmbg;
                FullName = dto.FullName;
                Address = dto.Address;
                OpstinaCode = dto.OpstinaCode;
                PhoneNumber = dto.PhoneNumber ?? string.Empty;
                Email = dto.Email ?? string.Empty;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
