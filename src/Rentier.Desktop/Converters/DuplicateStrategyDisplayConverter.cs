using Avalonia.Data.Converters;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.Converters;

public static class DuplicateStrategyDisplayConverter
{
    public static readonly FuncValueConverter<DuplicateStrategy, string> Instance =
        new(strategy => strategy switch
        {
            DuplicateStrategy.SkipExisting => Strings.Sync_Strategy_SkipExisting,
            DuplicateStrategy.CreateNewRevision => Strings.Sync_Strategy_CreateNewRevision,
            DuplicateStrategy.ReprocessInPlace => Strings.Sync_Strategy_ReprocessInPlace,
            _ => strategy.ToString()
        });
}
