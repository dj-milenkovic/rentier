using ReactiveUI.Reactive;
using Rentier.Application.DTOs;

namespace Rentier.Desktop.ViewModels;

public sealed class HolidayEntryViewModel : ReactiveObject
{
    private DateOnly _date;
    private string _name = string.Empty;

    public DateOnly Date
    {
        get => _date;
        set => this.RaiseAndSetIfChanged(ref _date, value);
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public static HolidayEntryViewModel FromDto(HolidayEntryDto dto) =>
        new() { Date = dto.Date, Name = dto.Name };

    public HolidayEntryDto ToDto() => new(Date, Name);
}
