using Rentier.Application.DTOs;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Read-only view model row for a single report in the reports list.
/// All display formatting is done here; the view binds to these computed properties.
/// </summary>
public sealed class ReportRowViewModel
{
    public Guid         Id           { get; }
    public string       ReportName   { get; }
    public DateOnly     ImportDate   { get; }
    public string       ImporterName { get; }
    public ReportStatus Status       { get; }
    public int          FilingCount  { get; }

    /// <summary>Import date formatted as yyyy-MM-dd.</summary>
    public string ImportDateDisplay => ImportDate.ToString("yyyy-MM-dd");

    private ReportRowViewModel(ReportRowDto dto)
    {
        Id           = dto.Id;
        ReportName   = dto.ReportName;
        ImportDate   = dto.ImportDate;
        ImporterName = dto.ImporterName;
        Status       = dto.Status;
        FilingCount  = dto.FilingCount;
    }

    /// <summary>Creates a ReportRowViewModel from a ReportRowDto.</summary>
    public static ReportRowViewModel From(ReportRowDto dto) => new(dto);
}
