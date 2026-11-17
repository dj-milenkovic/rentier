using System.Collections.ObjectModel;
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

public sealed class ImporterSettingsViewModel : ReactiveObject, IActivatableViewModel
{
    private readonly ImporterSettingsHandlers _handlers;
    private readonly ISequencer _scheduler;
    private readonly IConfirmationService _confirmation;

    // Form fields
    private string _displayName = string.Empty;
    private ReportType _reportType = ReportType.IbkrCsv;
    private TaxpayerProfileDto? _selectedProfile;
    private MailboxDto? _selectedMailbox;
    private string _fromFilter = string.Empty;
    private string _subjectFilter = string.Empty;
    private string _attachmentRegex = string.Empty;
    private string _paymentNotes = string.Empty;

    // State fields
    private ImporterItemViewModel? _selectedImporter;
    private bool _isLoading;
    private string? _errorMessage;
    private string? _successMessage;
    private bool _isEditMode;

    public string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }

    public ReportType ReportType
    {
        get => _reportType;
        set => this.RaiseAndSetIfChanged(ref _reportType, value);
    }

    public TaxpayerProfileDto? SelectedProfile
    {
        get => _selectedProfile;
        set => this.RaiseAndSetIfChanged(ref _selectedProfile, value);
    }

    public MailboxDto? SelectedMailbox
    {
        get => _selectedMailbox;
        set => this.RaiseAndSetIfChanged(ref _selectedMailbox, value);
    }

    public string FromFilter
    {
        get => _fromFilter;
        set => this.RaiseAndSetIfChanged(ref _fromFilter, value);
    }

    public string SubjectFilter
    {
        get => _subjectFilter;
        set => this.RaiseAndSetIfChanged(ref _subjectFilter, value);
    }

    public string AttachmentRegex
    {
        get => _attachmentRegex;
        set => this.RaiseAndSetIfChanged(ref _attachmentRegex, value);
    }

    public string PaymentNotes
    {
        get => _paymentNotes;
        set => this.RaiseAndSetIfChanged(ref _paymentNotes, value);
    }

    public ImporterItemViewModel? SelectedImporter
    {
        get => _selectedImporter;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedImporter, value);
            if (value != null)
                PopulateFormFromDto(value.Dto);
            else
                ClearForm();
        }
    }

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

    public string? SuccessMessage
    {
        get => _successMessage;
        private set => this.RaiseAndSetIfChanged(ref _successMessage, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set => this.RaiseAndSetIfChanged(ref _isEditMode, value);
    }

    public ObservableCollection<ImporterItemViewModel> ImporterItems { get; } = new();
    public ObservableCollection<TaxpayerProfileDto> AvailableProfiles { get; } = new();
    public ObservableCollection<MailboxDto> AvailableMailboxes { get; } = new();
    public IEnumerable<ReportType> AvailableReportTypes { get; } = Enum.GetValues<ReportType>();

    public ReactiveCommand<RxVoid, RxVoid> AddNewCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> SaveCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> DeleteCommand { get; }

    public ViewModelActivator Activator { get; } = new();

    public ImporterSettingsViewModel(
        ImporterSettingsHandlers handlers,
        IConfirmationService confirmation,
        ISequencer? scheduler = null)
    {
        _handlers = handlers;
        _scheduler = scheduler ?? RxSchedulers.MainThreadScheduler;
        _confirmation = confirmation;

        AddNewCommand = ReactiveCommand.Create(OnAddNew);
        SaveCommand = ReactiveCommand.CreateFromTask(OnSaveAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask(
            OnDeleteAsync,
            this.WhenAnyValue(x => x.SelectedImporter, x => x.IsEditMode,
                (s, e) => s != null && e));

        this.WhenActivated((MultipleDisposable disposables) =>
        {
            Signal.FromAsync(async ct => { await LoadAsync(ct); return RxVoid.Default; })
                .ObserveOn(_scheduler)
                .Subscribe()
                .DisposeWith(disposables);
            SaveCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            DeleteCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
        });
    }

    private void PopulateFormFromDto(ImporterDto dto)
    {
        DisplayName = dto.DisplayName;
        ReportType = dto.ReportType;
        SelectedProfile = AvailableProfiles.FirstOrDefault(p => p.Id == dto.TaxpayerProfileId);
        SelectedMailbox = AvailableMailboxes.FirstOrDefault(m => m.Id == dto.MailboxId);
        FromFilter = dto.FromFilter;
        SubjectFilter = dto.SubjectFilter;
        AttachmentRegex = dto.AttachmentRegex;
        PaymentNotes = dto.PaymentNotes;
        IsEditMode = true;
    }

    private void ClearForm()
    {
        DisplayName = string.Empty;
        ReportType = ReportType.IbkrCsv;
        SelectedProfile = null;
        SelectedMailbox = null;
        FromFilter = string.Empty;
        SubjectFilter = string.Empty;
        AttachmentRegex = string.Empty;
        PaymentNotes = string.Empty;
        IsEditMode = false;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    private void OnAddNew()
    {
        _selectedImporter = null;
        this.RaisePropertyChanged(nameof(SelectedImporter));
        ClearForm();
    }

    private async Task OnSaveAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            if (IsEditMode && SelectedImporter != null)
            {
                var cmd = new UpdateImporterCommand(
                    SelectedImporter.Id,
                    DisplayName,
                    ReportType,
                    SelectedProfile?.Id,
                    SelectedMailbox?.Id,
                    FromFilter,
                    SubjectFilter,
                    AttachmentRegex,
                    PaymentNotes);

                var result = await _handlers.UpdateImporter.HandleAsync(cmd, ct);
                if (result.IsSuccess)
                    await ReloadReselectAndPopulateAsync(SelectedImporter.Id, ct);
                else
                    ErrorMessage = result.Error.Message;
            }
            else
            {
                var cmd = new AddImporterCommand(
                    DisplayName,
                    ReportType,
                    SelectedProfile?.Id,
                    SelectedMailbox?.Id,
                    FromFilter,
                    SubjectFilter,
                    AttachmentRegex,
                    PaymentNotes);

                var result = await _handlers.AddImporter.HandleAsync(cmd, ct);
                if (result.IsSuccess)
                    await ReloadReselectAndPopulateAsync(result.Value, ct);
                else
                    ErrorMessage = result.Error.Message;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Shared post-save flow for both add and edit: reload the importer list,
    /// reselect the saved item, and repopulate the form (or clear it if it vanished).</summary>
    private async Task ReloadReselectAndPopulateAsync(Guid savedId, CancellationToken ct)
    {
        var reloadError = await ReloadImportersAsync(ct);
        _selectedImporter = ImporterItems.FirstOrDefault(i => i.Id == savedId);
        this.RaisePropertyChanged(nameof(SelectedImporter));
        if (_selectedImporter != null)
            PopulateFormFromDto(_selectedImporter.Dto);
        else
            ClearForm();
        // The save itself succeeded; a failed refresh is reported alongside it (after ClearForm,
        // which resets both messages) so a stale list is never shown silently.
        SuccessMessage = Strings.Importers_Saved_Confirmation;
        if (reloadError is not null)
            ErrorMessage = reloadError.Message;
    }

    private async Task OnDeleteAsync(CancellationToken ct)
    {
        if (SelectedImporter is null) return;

        var confirmed = await _confirmation.ConfirmAsync(new ConfirmationRequest(
            Strings.Importer_Delete_Title,
            string.Format(Strings.Importer_Delete_Message, SelectedImporter.DisplayName),
            Strings.Common_Delete,
            Strings.Common_Cancel));
        if (!confirmed) return;

        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            var cmd = new DeleteImporterCommand(SelectedImporter.Id);
            var result = await _handlers.DeleteImporter.HandleAsync(cmd, ct);
            if (result.IsSuccess)
            {
                var reloadError = await ReloadImportersAsync(ct);
                OnAddNew();
                // Set after OnAddNew, which clears the messages; the list may still show the deleted row.
                if (reloadError is not null)
                    ErrorMessage = reloadError.Message;
            }
            else
            {
                ErrorMessage = result.Error.Message;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            // (1) Load importers
            var importersError = await ReloadImportersAsync(ct);
            if (importersError is not null)
                ErrorMessage = importersError.Message;

            // (2) Load profile
            var profileResult = await _handlers.GetProfile.HandleAsync(new GetTaxpayerProfileQuery(), ct);
            if (profileResult.IsSuccess && profileResult.Value is not null)
            {
                AvailableProfiles.Clear();
                AvailableProfiles.Add(profileResult.Value);
            }

            // (3) Load mailboxes
            var mailboxesResult = await _handlers.GetMailboxes.HandleAsync(new GetMailboxesQuery(), ct);
            if (mailboxesResult.IsSuccess)
            {
                AvailableMailboxes.Clear();
                foreach (var dto in mailboxesResult.Value)
                    AvailableMailboxes.Add(dto);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Refreshes <see cref="ImporterItems"/>; returns the error when the query failed.</summary>
    private async Task<Error?> ReloadImportersAsync(CancellationToken ct = default)
    {
        var result = await _handlers.GetImporters.HandleAsync(new GetImportersQuery(), ct);
        if (!result.IsSuccess)
            return result.Error;

        ImporterItems.Clear();
        foreach (var dto in result.Value)
            ImporterItems.Add(ImporterItemViewModel.From(dto));
        return null;
    }
}
