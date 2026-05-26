using Rentier.Application.DTOs;

namespace Rentier.Desktop.ViewModels;

public sealed class SyncProgressEntryViewModel
{
    public string Icon { get; }
    public string Message { get; }
    public string Timestamp { get; }
    public SyncProgressSeverity Severity { get; }

    public SyncProgressEntryViewModel(SyncProgressEntry entry)
    {
        Icon = entry.Severity switch
        {
            SyncProgressSeverity.Error => "✕",
            SyncProgressSeverity.Warning => "⚠",
            _ => "•"
        };
        Message = entry.Message;
        Timestamp = entry.Timestamp.ToString("HH:mm:ss");
        Severity = entry.Severity;
    }
}
