using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReactiveUI;
using Rentier.Desktop.ViewModels;
using Rentier.Desktop.Views;

namespace Rentier.Desktop;

/// <summary>
/// Resolves the View for a ViewModel through an explicit registry.
/// Adding a new page means adding one entry to <see cref="FACTORIES"/>; a missing entry is
/// caught by <c>ViewLocatorTests</c> instead of surfacing as a runtime "View not found".
/// No reflection or name-based lookup, so it stays safe under trimming.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ViewLocator : IDataTemplate
{
    private static readonly IReadOnlyDictionary<Type, Func<Control>> FACTORIES =
        new Dictionary<Type, Func<Control>>
        {
            [typeof(AppearanceSettingsViewModel)] = static () => new AppearanceSettingsView(),
            [typeof(DashboardViewModel)] = static () => new DashboardView(),
            [typeof(FilingsViewModel)] = static () => new FilingsView(),
            [typeof(HolidaySettingsViewModel)] = static () => new HolidaySettingsView(),
            [typeof(ImporterSettingsViewModel)] = static () => new ImporterSettingsView(),
            [typeof(MailboxSettingsViewModel)] = static () => new MailboxSettingsView(),
            [typeof(ManualFilingViewModel)] = static () => new ManualFilingView(),
            [typeof(ProfileSettingsViewModel)] = static () => new ProfileSettingsView(),
            [typeof(ReportsViewModel)] = static () => new ReportsView(),
            [typeof(SyncViewModel)] = static () => new SyncView(),
        };

    /// <summary>ViewModel types that have a registered View.</summary>
    public static IReadOnlyCollection<Type> RegisteredViewModelTypes => FACTORIES.Keys.ToArray();

    public Control Build(object? param)
    {
        if (param is null)
            return new TextBlock { Text = "No ViewModel provided" };

        return FACTORIES.TryGetValue(param.GetType(), out var factory)
            ? factory()
            : new TextBlock { Text = $"View not found for {param.GetType().Name}" };
    }

    public bool Match(object? data) => data is ReactiveObject;
}
