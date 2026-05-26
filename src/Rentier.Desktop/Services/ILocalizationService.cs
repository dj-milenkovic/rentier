using System.ComponentModel;

namespace Rentier.Desktop.Services;

public interface ILocalizationService : INotifyPropertyChanged
{
    string this[string key] { get; }
    string CurrentCultureCode { get; }
    void SetCulture(string cultureCode);
    IObservable<string> CultureChanged { get; }
}
