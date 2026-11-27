using Avalonia.Data.Converters;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

/// <summary>Maps <see cref="SyncMode"/> to its display text in the current UI language.</summary>
public static class SyncModeDisplayConverter
{
    public static readonly FuncValueConverter<SyncMode, string> Instance =
        new(mode => mode switch
        {
            SyncMode.Incremental => LocalizedText.Get(nameof(Strings.Sync_Mode_Incremental)),
            SyncMode.ReplayFromDate => LocalizedText.Get(nameof(Strings.Sync_Mode_ReplayFromDate)),
            SyncMode.FullReplay => LocalizedText.Get(nameof(Strings.Sync_Mode_FullReplay)),
            _ => mode.ToString()
        });
}
