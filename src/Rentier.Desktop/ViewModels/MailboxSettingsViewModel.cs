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
using Rentier.Desktop.Dialogs;
using Rentier.Desktop.Resources;

namespace Rentier.Desktop.ViewModels;

public sealed class MailboxSettingsViewModel : ReactiveObject, IActivatableViewModel
{
    private readonly IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>> _queryHandler;
    private readonly ICommandHandler<AddMailboxCommand, Result<Guid, Error>> _addHandler;
    private readonly ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>> _updateHandler;
    private readonly ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>> _deleteHandler;
    private readonly IScheduler _scheduler;
    private readonly Func<string, string, string, string, Task<bool>> _confirmAction;

    private string _host = "imap.gmail.com";
    private int _port = 993;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private MailboxItemViewModel? _selectedMailbox;
    private bool _isLoading;
    private string? _errorMessage;
    private string? _successMessage;
    private bool _isEditMode;

    public string Host
    {
        get => _host;
        set => this.RaiseAndSetIfChanged(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public MailboxItemViewModel? SelectedMailbox
    {
        get => _selectedMailbox;
        set => this.RaiseAndSetIfChanged(ref _selectedMailbox, value);
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

    public ObservableCollection<MailboxItemViewModel> Mailboxes { get; } = new();

    public ReactiveCommand<Unit, Unit> AddNewCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    public ViewModelActivator Activator { get; } = new();

    public MailboxSettingsViewModel(
        IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>> queryHandler,
        ICommandHandler<AddMailboxCommand, Result<Guid, Error>> addHandler,
        ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>> updateHandler,
        ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>> deleteHandler,
        IScheduler? scheduler = null,
        Func<string, string, string, string, Task<bool>>? confirmAction = null)
    {
        _queryHandler = queryHandler;
        _addHandler = addHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _scheduler = scheduler ?? RxApp.MainThreadScheduler;
        _confirmAction = confirmAction ?? ConfirmDialogHelper.ShowAsync;

        AddNewCommand = ReactiveCommand.Create(OnAddNew);

        SaveCommand = ReactiveCommand.CreateFromTask(OnSaveAsync);

        var canDelete = this.WhenAnyValue(x => x.SelectedMailbox).Select(m => m != null);
        DeleteCommand = ReactiveCommand.CreateFromTask(OnDeleteAsync, canDelete);

        // When selected mailbox changes, populate form fields
        this.WhenAnyValue(x => x.SelectedMailbox)
            .Subscribe(selected =>
            {
                if (selected != null)
                {
                    Host = selected.Host;
                    Port = selected.Port;
                    Username = selected.Username;
                    Password = string.Empty;
                    IsEditMode = true;
                }
                else
                {
                    IsEditMode = false;
                }
            });

        this.WhenActivated(disposables =>
        {
            Observable.FromAsync(ct => LoadAsync(ct))
                .ObserveOn(_scheduler)
                .Subscribe()
                .DisposeWith(disposables);
            AddNewCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
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
        SelectedMailbox = null;
        Host = "imap.gmail.com";
        Port = 993;
        Username = string.Empty;
        Password = string.Empty;
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
            if (IsEditMode && SelectedMailbox != null)
            {
                // Capture the ID before ReloadAsync: Clear() causes the ListBox to deselect,
                // setting SelectedMailbox to null, so we cannot read it afterwards.
                var editedId = SelectedMailbox.Id;
                var cmd = new UpdateMailboxCommand(SelectedMailbox.Id, Host, Port, Username, Password);
                var result = await _updateHandler.HandleAsync(cmd, ct);
                if (result.IsSuccess)
                {
                    await ReloadAsync(ct);
                    SelectedMailbox = Mailboxes.FirstOrDefault(m => m.Id == editedId);
                    Password = string.Empty;
                    SuccessMessage = Strings.Mailboxes_SuccessMessage_Label;
                }
                else
                {
                    ErrorMessage = result.Error.Message;
                }
            }
            else
            {
                var cmd = new AddMailboxCommand(Host, Port, Username, Password);
                var result = await _addHandler.HandleAsync(cmd, ct);
                if (result.IsSuccess)
                {
                    var newId = result.Value;
                    await ReloadAsync(ct);
                    var newItem = Mailboxes.FirstOrDefault(m => m.Id == newId);
                    SelectedMailbox = newItem;
                    IsEditMode = true;
                    Password = string.Empty;
                    SuccessMessage = Strings.Mailboxes_SuccessMessage_Label;
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
        if (SelectedMailbox is null) return;

        var confirmed = await _confirmAction(
            Strings.Mailbox_Delete_Title,
            string.Format(Strings.Mailbox_Delete_Message, SelectedMailbox.Host),
            Strings.Common_Delete,
            Strings.Common_Cancel);
        if (!confirmed) return;

        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            var cmd = new DeleteMailboxCommand(SelectedMailbox.Id);
            var result = await _deleteHandler.HandleAsync(cmd, ct);
            if (result.IsSuccess)
            {
                var toRemove = SelectedMailbox;
                SelectedMailbox = null;
                Mailboxes.Remove(toRemove);
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
            var result = await _queryHandler.HandleAsync(new GetMailboxesQuery(), ct);
            if (result.IsSuccess)
            {
                Mailboxes.Clear();
                foreach (var dto in result.Value)
                    Mailboxes.Add(MailboxItemViewModel.From(dto));
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

    private async Task ReloadAsync(CancellationToken ct = default)
    {
        var result = await _queryHandler.HandleAsync(new GetMailboxesQuery(), ct);
        if (result.IsSuccess)
        {
            Mailboxes.Clear();
            foreach (var dto in result.Value)
                Mailboxes.Add(MailboxItemViewModel.From(dto));
        }
    }
}
