# Contract: ILocalizationService

**Layer**: Desktop (UI concern — same pattern as `IThemeService`)  
**Lifetime**: Singleton  
**Injected into**: All ViewModels that display localized strings, `App.axaml.cs` (startup)

## Interface Definition

```csharp
namespace Rentier.Desktop.Services;

/// <summary>
/// Reactive localization service that wraps .NET ResourceManager.
/// Registered as singleton. Injected into ViewModels.
/// Used as a StaticResource in AXAML for {Binding [Key]} bindings.
/// Implements INotifyPropertyChanged so PropertyChanged("") triggers
/// all indexer bindings to re-evaluate when culture changes.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    /// <summary>
    /// Indexer that returns the localized string for the given key
    /// in the current culture. Used in AXAML as:
    ///   {Binding [Nav_Dashboard], Source={StaticResource Localizer}}
    /// </summary>
    string this[string key] { get; }

    /// <summary>
    /// The currently active culture code (e.g., "en" or "sr-Latn").
    /// </summary>
    string CurrentCultureCode { get; }

    /// <summary>
    /// Switches the active culture and raises PropertyChanged("")
    /// to trigger all bindings to re-evaluate.
    /// </summary>
    /// <param name="cultureCode">Culture code: "en" or "sr-Latn"</param>
    void SetCulture(string cultureCode);

    /// <summary>
    /// Observable that emits on every culture change.
    /// ViewModels subscribe to update non-AXAML strings (e.g., NavigationEntry labels).
    /// </summary>
    IObservable<string> CultureChanged { get; }
}
```

## AXAML Usage Contract

### Static Resource Registration (App.axaml)

```xml
<Application.Resources>
  <svc:LocalizationService x:Key="Localizer" />
</Application.Resources>
```

**Important**: The `LocalizationService` instance in `App.axaml` resources must be the SAME singleton instance from the DI container. This is achieved by:
1. Creating the `LocalizationService` singleton in DI.
2. After building the service provider, assigning it to `Application.Current.Resources["Localizer"]`.

### Binding Pattern (all AXAML views)

**Before** (current — static, one-shot evaluation):
```xml
<TextBlock Text="{x:Static res:Strings.Nav_Dashboard}" />
```

**After** (reactive — re-evaluates on culture change):
```xml
<TextBlock Text="{Binding [Nav_Dashboard], Source={StaticResource Localizer}}" />
```

### Binding Rules

1. Every `{x:Static res:Strings.XXX}` must be replaced with `{Binding [XXX], Source={StaticResource Localizer}}`.
2. The key inside `[...]` must exactly match the key in `Strings.resx` / `Strings.sr-Latn.resx`.
3. `Source={StaticResource Localizer}` is required on every binding (the DataContext is the ViewModel, not the localizer).
4. No `Mode=` is needed — these are one-way read bindings by default.
5. String format bindings like `StringFormat='{}{0} items'` should use the localizer key for the format string too.

## Implementation Contract (LocalizationService)

```csharp
namespace Rentier.Desktop.Services;

public sealed class LocalizationService : ILocalizationService
{
    private readonly ResourceManager _resourceManager;
    private readonly Subject<string> _cultureChanged = new();
    private CultureInfo _currentCulture;

    public LocalizationService()
    {
        _resourceManager = Strings.ResourceManager;
        _currentCulture = new CultureInfo("sr-Latn"); // Default: Serbian
    }

    public string this[string key]
    {
        get
        {
            // Try current culture
            var value = _resourceManager.GetString(key, _currentCulture);
            if (!string.IsNullOrEmpty(value))
                return value;

            // Fallback to Serbian (app default)
            value = _resourceManager.GetString(key, new CultureInfo("sr-Latn"));
            if (!string.IsNullOrEmpty(value))
                return value;

            // Last resort: neutral resource (English)
            return _resourceManager.GetString(key) ?? $"[{key}]";
        }
    }

    public string CurrentCultureCode => _currentCulture.Name;

    public IObservable<string> CultureChanged => _cultureChanged.AsObservable();

    public void SetCulture(string cultureCode)
    {
        _currentCulture = new CultureInfo(cultureCode);
        _cultureChanged.OnNext(cultureCode);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        // PropertyChanged("") causes ALL indexer bindings to re-evaluate
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

## Fallback Chain

```text
1. Try ResourceManager.GetString(key, currentCulture)
2. If null/empty → Try ResourceManager.GetString(key, "sr-Latn")
3. If null/empty → Try ResourceManager.GetString(key) [neutral/English]
4. If null/empty → Return "[key]" (debug marker, should never happen in production)
```

## ViewModel Usage Contract

### For AXAML-bound strings (automatic)

No ViewModel work needed — the `{Binding [Key], Source={StaticResource Localizer}}` pattern handles re-evaluation automatically via `INotifyPropertyChanged`.

### For code-behind strings (NavigationEntry labels, computed messages)

ViewModels subscribe to `ILocalizationService.CultureChanged` and update strings:

```csharp
// In MainWindowViewModel constructor:
localizationService.CultureChanged
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(_ => UpdateNavigationLabels())
    .DisposeWith(disposables);
```

## Thread Safety

- `ResourceManager.GetString()` is thread-safe for reads.
- `SetCulture()` is called from UI thread only (triggered by user interaction).
- `PropertyChanged` notification is raised on the calling (UI) thread.
- `CultureChanged` observable is observed on `RxApp.MainThreadScheduler`.

## Startup Sequence

```text
1. App.axaml.cs: Build DI container
2. App.axaml.cs: Resolve ILocalizationService (singleton)
3. App.axaml.cs: Read language preference from DB (via GetUserPreferenceQuery)
4. App.axaml.cs: Call localizationService.SetCulture(savedCulture ?? "sr-Latn")
5. App.axaml.cs: Assign localizer to Application.Resources["Localizer"]
6. App.axaml.cs: Create MainWindow (all views bind to localizer with correct culture)
```
