using System.Globalization;
using Rentier.Application.DTOs;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.Resources;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Read-only view model row for a single filing in the filings list.
/// All display formatting is done here; the view binds to these computed properties.
/// </summary>
public sealed class FilingRowViewModel
{
    public Guid Id { get; }
    public FilingStatus Status { get; }
    public IncomeType IncomeType { get; }
    public string PayingEntity { get; }
    public DateOnly FilingDeadline { get; }
    public decimal TaxPayable { get; }
    public string? PaymentReference { get; }

    /// <summary>Filing deadline formatted as yyyy-MM-dd.</summary>
    public string DeadlineDisplay => FilingDeadline.ToString("yyyy-MM-dd");

    /// <summary>
    /// Tax payable formatted using InvariantCulture to avoid locale-dependent separators.
    /// Serbian locale uses period as thousands separator which conflicts with the N2 format.
    /// </summary>
    public string TaxPayableDisplay =>
        TaxPayable.ToString("N2", CultureInfo.InvariantCulture) + " RSD";

    /// <summary>Localised display label for the current status (used by the read-only badge).</summary>
    public string StatusDisplayText => Status.ToDisplayString();

    /// <summary>Payment reference is only editable when the filing has been Filed.</summary>
    public bool IsPaymentReferenceEditable => Status == FilingStatus.Filed;

    /// <summary>Valid status options a user can advance to from the current status.</summary>
    public IReadOnlyList<FilingStatus> AvailableNextStatuses => Status switch
    {
        FilingStatus.Init => [FilingStatus.Filed],
        FilingStatus.Filed => [FilingStatus.Paid],
        _ => []
    };

    private FilingRowViewModel(FilingRowDto dto)
    {
        Id = dto.Id;
        Status = dto.Status;
        IncomeType = dto.IncomeType;
        PayingEntity = dto.PayingEntity;
        FilingDeadline = dto.FilingDeadline;
        TaxPayable = dto.TaxPayable;
        PaymentReference = dto.PaymentReference;
    }

    /// <summary>Creates a FilingRowViewModel from a FilingRowDto.</summary>
    public static FilingRowViewModel From(FilingRowDto dto) => new(dto);
}
