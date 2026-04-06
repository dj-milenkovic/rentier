using ReactiveUI;
using Rentier.Desktop.Resources;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Main window ViewModel with sidebar navigation.
/// For UI-initiated async work, use:
/// SomeCommand = ReactiveCommand.CreateFromTask(async ct => await _handler.HandleAsync(cmd, ct));
/// Updates scheduled via RxApp.MainThreadScheduler.
/// </summary>
public sealed class MainWindowViewModel : ReactiveObject
{
    private ReactiveObject _currentViewModel;
    private NavigationEntry _selectedEntry;

    public IReadOnlyList<NavigationEntry> NavigationEntries { get; }

    public ReactiveObject CurrentViewModel
    {
        get => _currentViewModel;
        set => this.RaiseAndSetIfChanged(ref _currentViewModel, value);
    }

    public NavigationEntry SelectedEntry
    {
        get => _selectedEntry;
        set => this.RaiseAndSetIfChanged(ref _selectedEntry, value);
    }

    public MainWindowViewModel(
        FilingsViewModel filingsVm,
        ReportsViewModel reportsVm,
        SettingsViewModel settingsVm)
    {
        NavigationEntries = new List<NavigationEntry>
        {
            new(Strings.Nav_Filings, filingsVm),
            new(Strings.Nav_Reports, reportsVm),
            new(Strings.Nav_Settings, settingsVm)
        };

        _selectedEntry = NavigationEntries[0];
        _currentViewModel = filingsVm;
    }
}
