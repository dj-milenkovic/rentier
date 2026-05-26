using ReactiveUI;
using Rentier.Application.DTOs;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// View model row for a single report in the reports list.
/// All display formatting is done here; the view binds to these computed properties.
/// </summary>
public sealed class ReportRowViewModel : ReactiveObject
{
    public Guid Id { get; }
    public string ReportName { get; }
    public DateOnly ImportDate { get; }
    public DateOnly? EmailDate { get; }
    public string ImporterName { get; }
    public ReportStatus Status { get; }
    public int FilingCount { get; }
    /// <summary>Friendly display name: "&lt;ImporterName&gt; – &lt;EarliestIncomeDate&gt;" or import date fallback.</summary>
    public string DisplayName { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    /// <summary>Import date formatted as yyyy-MM-dd.</summary>
    public string ImportDateDisplay => ImportDate.ToString("yyyy-MM-dd");

    /// <summary>Email date formatted as yyyy-MM-dd, or empty string when absent.</summary>
    public string EmailDateDisplay => EmailDate?.ToString("yyyy-MM-dd") ?? string.Empty;

    private ReportRowViewModel(ReportRowDto dto)
    {
        Id = dto.Id;
        ReportName = dto.ReportName;
        ImportDate = dto.ImportDate;
        EmailDate = dto.EmailDate;
        ImporterName = dto.ImporterName;
        Status = dto.Status;
        FilingCount = dto.FilingCount;
        DisplayName = dto.DisplayName;
    }

    /// <summary>Creates a ReportRowViewModel from a ReportRowDto.</summary>
    public static ReportRowViewModel From(ReportRowDto dto) => new(dto);
}
