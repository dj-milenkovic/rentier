using ReactiveUI;
using Rentier.Application.DTOs;
using Rentier.Desktop.Extensions;

namespace Rentier.Desktop.ViewModels;

public sealed class ImporterItemViewModel : ReactiveObject
{
    public Guid Id { get; }
    public string DisplayName { get; }
    public string ReportTypeDisplay { get; }
    internal ImporterDto Dto { get; }

    private ImporterItemViewModel(ImporterDto dto)
    {
        Dto = dto;
        Id = dto.Id;
        DisplayName = dto.DisplayName;
        ReportTypeDisplay = dto.ReportType.ToDisplayString();
    }

    public static ImporterItemViewModel From(ImporterDto dto) => new(dto);
}
