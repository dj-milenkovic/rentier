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
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

public sealed class ImporterSettingsViewModel : ReactiveObject, IActivatableViewModel
{
    private readonly IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>> _getImporters;
    private readonly IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> _getProfile;
    private readonly IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>> _getMailboxes;
    private readonly ICommandHandler<AddImporterCommand, Result<Guid, Error>> _addImporter;
    private readonly ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>> _updateImporter;
    private readonly ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>> _deleteImporter;
    private readonly IScheduler _scheduler;

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
            {
                DisplayName = value.Dto.DisplayName;
                ReportType = value.Dto.ReportType;
                SelectedProfile = AvailableProfiles.FirstOrDefault(p => p.Id == value.Dto.TaxpayerProfileId);
                SelectedMailbox = AvailableMailboxes.FirstOrDefault(m => m.Id == value.Dto.MailboxId);
                FromFilter = value.Dto.FromFilter;
                SubjectFilter = value.Dto.SubjectFilter;
                AttachmentRegex = value.Dto.AttachmentRegex;
                PaymentNotes = value.Dto.PaymentNotes;
                IsEditMode = true;
            }
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

    public ReactiveCommand<Unit, Unit> AddNewCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    public ViewModelActivator Activator { get; } = new();

    public ImporterSettingsViewModel(
        IQueryHandler<GetImportersQuery, Result<IReadOnlyList<ImporterDto>, Error>> getImporters,
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>> getProfile,
        IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>> getMailboxes,
        ICommandHandler<AddImporterCommand, Result<Guid, Error>> addImporter,
        ICommandHandler<UpdateImporterCommand, Result<VoidResult, Error>> updateImporter,
        ICommandHandler<DeleteImporterCommand, Result<VoidResult, Error>> deleteImporter,
        IScheduler? scheduler = null)
    {
        _getImporters = getImporters;
        _getProfile = getProfile;
        _getMailboxes = getMailboxes;
        _addImporter = addImporter;
        _updateImporter = updateImporter;
        _deleteImporter = deleteImporter;
        _scheduler = scheduler ?? RxApp.MainThreadScheduler;

        AddNewCommand = ReactiveCommand.Create(OnAddNew);
        SaveCommand = ReactiveCommand.CreateFromTask(OnSaveAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask(
            OnDeleteAsync,
            this.WhenAnyValue(x => x.SelectedImporter, x => x.IsEditMode,
                (s, e) => s != null && e));

        this.WhenActivated(disposables =>
        {
            Observable.FromAsync(ct => LoadAsync(ct))
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

    private void OnAddNew()
    {
        _selectedImporter = null;
        this.RaisePropertyChanged(nameof(SelectedImporter));
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

                var result = await _updateImporter.HandleAsync(cmd, ct);
                if (result.IsSuccess)
                {
                    var savedId = SelectedImporter.Id;
                    await ReloadImportersAsync(ct);
                    _selectedImporter = ImporterItems.FirstOrDefault(i => i.Id == savedId);
                    this.RaisePropertyChanged(nameof(SelectedImporter));
                    SuccessMessage = Strings.Importers_Saved_Confirmation;
                }
                else
                {
                    ErrorMessage = result.Error.Message;
                }
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

                var result = await _addImporter.HandleAsync(cmd, ct);
                if (result.IsSuccess)
                {
                    var newId = result.Value;
                    await ReloadImportersAsync(ct);
                    _selectedImporter = ImporterItems.FirstOrDefault(i => i.Id == newId);
                    this.RaisePropertyChanged(nameof(SelectedImporter));
                    IsEditMode = true;
                    SuccessMessage = Strings.Importers_Saved_Confirmation;
                }
                else
                {
                    ErrorMessage = result.Error.Message;
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task OnDeleteAsync(CancellationToken ct)
    {
        if (SelectedImporter is null) return;

        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            var cmd = new DeleteImporterCommand(SelectedImporter.Id);
            var result = await _deleteImporter.HandleAsync(cmd, ct);
            if (result.IsSuccess)
            {
                await ReloadImportersAsync(ct);
                OnAddNew();
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
            var importersResult = await _getImporters.HandleAsync(new GetImportersQuery(), ct);
            if (importersResult.IsSuccess)
            {
                ImporterItems.Clear();
                foreach (var dto in importersResult.Value)
                    ImporterItems.Add(ImporterItemViewModel.From(dto));
            }
            else
            {
                ErrorMessage = importersResult.Error.Message;
            }

            // (2) Load profile
            var profileResult = await _getProfile.HandleAsync(new GetTaxpayerProfileQuery(), ct);
            if (profileResult.IsSuccess && profileResult.Value is not null)
            {
                AvailableProfiles.Clear();
                AvailableProfiles.Add(profileResult.Value);
            }

            // (3) Load mailboxes
            var mailboxesResult = await _getMailboxes.HandleAsync(new GetMailboxesQuery(), ct);
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

    private async Task ReloadImportersAsync(CancellationToken ct = default)
    {
        var result = await _getImporters.HandleAsync(new GetImportersQuery(), ct);
        if (result.IsSuccess)
        {
            ImporterItems.Clear();
            foreach (var dto in result.Value)
                ImporterItems.Add(ImporterItemViewModel.From(dto));
        }
    }
}
