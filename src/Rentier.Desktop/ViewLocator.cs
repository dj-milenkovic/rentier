using System.Collections.Frozen;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Rentier.Desktop.ViewModels;
using Rentier.Desktop.Views;

namespace Rentier.Desktop;

/// <summary>
/// Resolves the View for a page ViewModel through an explicit registry.
/// Adding a new page means adding one entry to <see cref="FACTORIES"/>; a missing or wrong
/// entry is caught by <c>ViewLocatorTests</c> instead of surfacing as a runtime "View not found".
/// No reflection or name-based lookup, so it stays safe under trimming.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private static readonly FrozenDictionary<Type, Func<Control>> FACTORIES =
        new Dictionary<Type, Func<Control>>
        {
            [typeof(AppearanceSettingsViewModel)] = static () => new AppearanceSettingsView(),
            [typeof(DashboardViewModel)] = static () => new DashboardView(),
            [typeof(DiagnosticsSettingsViewModel)] = static () => new DiagnosticsSettingsView(),
            [typeof(FilingsViewModel)] = static () => new FilingsView(),
            [typeof(HolidaySettingsViewModel)] = static () => new HolidaySettingsView(),
            [typeof(ImporterSettingsViewModel)] = static () => new ImporterSettingsView(),
            [typeof(MailboxSettingsViewModel)] = static () => new MailboxSettingsView(),
            [typeof(ManualFilingViewModel)] = static () => new ManualFilingView(),
            [typeof(ProfileSettingsViewModel)] = static () => new ProfileSettingsView(),
            [typeof(ReportsViewModel)] = static () => new ReportsView(),
            [typeof(SyncViewModel)] = static () => new SyncView(),
        }.ToFrozenDictionary();

    /// <summary>ViewModel types that have a registered View.</summary>
    public static IEnumerable<Type> RegisteredViewModelTypes => FACTORIES.Keys;

    public Control Build(object? param)
    {
        if (param is null)
            return new TextBlock { Text = "No ViewModel provided" };

        var viewModelType = param.GetType();
        return FACTORIES.TryGetValue(viewModelType, out var factory)
            ? factory()
            : new TextBlock { Text = $"View not found for {viewModelType.FullName}" };
    }

    /// <summary>
    /// Claims only registered page ViewModels, so row/item ViewModels shown without a local
    /// template fall through to Avalonia's default templates instead of "View not found".
    /// </summary>
    public bool Match(object? data) => data is not null && FACTORIES.ContainsKey(data.GetType());
}
