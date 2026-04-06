using ReactiveUI;
using Rentier.Desktop.Resources;

namespace Rentier.Desktop.ViewModels;

public sealed class FilingsViewModel : ReactiveObject
{
    private string _placeholder = Strings.ComingSoon;

    public string Placeholder
    {
        get => _placeholder;
        set => this.RaiseAndSetIfChanged(ref _placeholder, value);
    }
}
