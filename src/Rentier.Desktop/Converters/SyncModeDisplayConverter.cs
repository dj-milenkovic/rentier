using Avalonia.Data.Converters;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

public static class SyncModeDisplayConverter
{
    public static readonly FuncValueConverter<SyncMode, string> Instance =
        new(mode => mode switch
        {
            SyncMode.Incremental => Strings.Sync_Mode_Incremental,
            SyncMode.ReplayFromDate => Strings.Sync_Mode_ReplayFromDate,
            SyncMode.FullReplay => Strings.Sync_Mode_FullReplay,
            _ => mode.ToString()
        });
}
