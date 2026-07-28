using System.Globalization;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI;
using Rentier.Application.DTOs;
using Rentier.Desktop.Extensions;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// View model row for a single filing in the filings list.
/// All display formatting is done here; the view binds to these computed properties.
/// </summary>
public sealed class FilingRowViewModel : ReactiveObject, ISelectableRow
{
    public Guid Id { get; }
    public FilingStatus Status { get; }
    public IncomeType IncomeType { get; }
    public string PayingEntity { get; }
    public DateOnly FilingDeadline { get; }
    public decimal TaxPayable { get; }
    public string? PaymentReference { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

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

    /// <summary>True when there is at least one valid next status to advance to.</summary>
    public bool HasNextStatus => AvailableNextStatuses.Count > 0;

    /// <summary>Tooltip text for the advance-status button.</summary>
    public string AdvanceStatusTooltip => HasNextStatus
        ? string.Format(Strings.Filings_Tooltip_AdvanceStatus, AvailableNextStatuses[0].ToDisplayString())
        : Strings.Filings_Tooltip_AdvanceStatus_None;

    /// <summary>Per-row command to advance this filing to its next status.</summary>
    public ReactiveCommand<RxVoid, RxVoid> AdvanceStatusCommand { get; }

    /// <summary>Per-row command to export this filing as PP-OPO XML.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ExportCommand { get; }

    /// <summary>Per-row command to delete this filing.</summary>
    public ReactiveCommand<RxVoid, RxVoid> DeleteCommand { get; }

    private FilingRowViewModel(
        FilingRowDto dto,
        Action<(Guid Id, FilingStatus NewStatus)> advanceStatus,
        Action<Guid> export,
        Action<Guid> delete)
    {
        Id = dto.Id;
        Status = dto.Status;
        IncomeType = dto.IncomeType;
        PayingEntity = dto.PayingEntity;
        FilingDeadline = dto.FilingDeadline;
        TaxPayable = dto.TaxPayable;
        PaymentReference = dto.PaymentReference;

        AdvanceStatusCommand = ReactiveCommand.Create(
            () => advanceStatus((Id, AvailableNextStatuses[0])),
            Observables.Return(HasNextStatus));
        ExportCommand = ReactiveCommand.Create(() => export(Id));
        DeleteCommand = ReactiveCommand.Create(() => delete(Id));
    }

    /// <summary>Creates a FilingRowViewModel from a FilingRowDto with action delegates for row-level commands.</summary>
    public static FilingRowViewModel From(
        FilingRowDto dto,
        Action<(Guid Id, FilingStatus NewStatus)> advanceStatus,
        Action<Guid> export,
        Action<Guid> delete) => new(dto, advanceStatus, export, delete);
}
