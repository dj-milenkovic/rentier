using ReactiveUI;
using Rentier.Application.DTOs;

namespace Rentier.Desktop.ViewModels;

public sealed class MailboxItemViewModel : ReactiveObject
{
    private Guid _id;
    private string _host = string.Empty;
    private int _port;
    private string _username = string.Empty;
    private DateOnly? _lastSyncDate;
    private long? _lastUid;

    public Guid Id
    {
        get => _id;
        private set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public string Host
    {
        get => _host;
        private set => this.RaiseAndSetIfChanged(ref _host, value);
    }

    public int Port
    {
        get => _port;
        private set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    public string Username
    {
        get => _username;
        private set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    public DateOnly? LastSyncDate
    {
        get => _lastSyncDate;
        private set => this.RaiseAndSetIfChanged(ref _lastSyncDate, value);
    }

    public long? LastUid
    {
        get => _lastUid;
        private set => this.RaiseAndSetIfChanged(ref _lastUid, value);
    }

    public string DisplayName => $"{Username} @ {Host}:{Port}";

    public static MailboxItemViewModel From(MailboxDto dto)
    {
        var vm = new MailboxItemViewModel();
        vm.Id = dto.Id;
        vm.Host = dto.Host;
        vm.Port = dto.Port;
        vm.Username = dto.Username;
        vm.LastSyncDate = dto.LastSyncDate;
        vm.LastUid = dto.LastUid;
        return vm;
    }

    public void UpdateFrom(MailboxDto dto)
    {
        Id = dto.Id;
        Host = dto.Host;
        Port = dto.Port;
        Username = dto.Username;
        LastSyncDate = dto.LastSyncDate;
        LastUid = dto.LastUid;
        this.RaisePropertyChanged(nameof(DisplayName));
    }
}
