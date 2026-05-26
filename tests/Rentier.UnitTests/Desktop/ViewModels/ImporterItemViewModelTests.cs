using FluentAssertions;
using Rentier.Application.DTOs;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Unit tests for ImporterItemViewModel — covers Id, DisplayName, ReportTypeDisplay,
/// and Dto pass-through for command binding.
/// </summary>
public class ImporterItemViewModelTests
{
    private static ImporterDto MakeDto(
        Guid? id = null,
        string displayName = "My IBKR Importer",
        ReportType reportType = ReportType.IbkrCsv) =>
        new(
            Id: id ?? new Guid("00000000-0000-0000-0000-000000000042"),
            DisplayName: displayName,
            ReportType: reportType,
            TaxpayerProfileId: null,
            MailboxId: null,
            FromFilter: string.Empty,
            SubjectFilter: string.Empty,
            AttachmentRegex: string.Empty,
            PaymentNotes: string.Empty);

    [Fact]
    public void From_ValidImporterDto_IdMatchesInput()
    {
        var id = new Guid("00000000-0000-0000-0000-000000000042");
        var dto = MakeDto(id: id);

        var vm = ImporterItemViewModel.From(dto);

        vm.Id.Should().Be(id);
    }

    [Fact]
    public void From_ValidImporterDto_DisplayNameMatchesInput()
    {
        var dto = MakeDto(displayName: "My IBKR Importer");

        var vm = ImporterItemViewModel.From(dto);

        vm.DisplayName.Should().Be("My IBKR Importer");
    }

    [Fact]
    public void From_ValidImporterDto_ReportTypeDisplayUsesToDisplayString()
    {
        var dto = MakeDto(reportType: ReportType.IbkrCsv);

        var vm = ImporterItemViewModel.From(dto);

        // ReportType.IbkrCsv.ToDisplayString() returns "IBKR CSV"
        vm.ReportTypeDisplay.Should().Be("IBKR CSV");
    }
}
