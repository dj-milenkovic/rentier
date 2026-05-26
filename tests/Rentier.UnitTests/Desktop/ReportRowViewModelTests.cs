using FluentAssertions;
using Rentier.Application.DTOs;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

public class ReportRowViewModelTests
{
    private static ReportRowDto MakeDto(
        DateOnly? importDate = null,
        DateOnly? emailDate = null,
        ReportStatus status = ReportStatus.Init) =>
        new(
            Id: Guid.NewGuid(),
            ReportName: "Test Report",
            ImportDate: importDate ?? new DateOnly(2024, 3, 15),
            EmailDate: emailDate,
            ImporterName: "Test Importer",
            Status: status,
            FilingCount: 5,
            DisplayName: "Test Report Display",
            EarliestIncomeDate: new DateOnly(2024, 1, 1));

    [Fact]
    public void ImportDateDisplay_FormatsAsYyyyMmDd()
    {
        var dto = MakeDto(importDate: new DateOnly(2025, 3, 1));
        var vm = ReportRowViewModel.From(dto);

        vm.ImportDateDisplay.Should().Be("2025-03-01");
    }

    [Fact]
    public void EmailDateDisplay_WhenPresent_FormatsAsYyyyMmDd()
    {
        var dto = MakeDto(emailDate: new DateOnly(2025, 2, 28));
        var vm = ReportRowViewModel.From(dto);

        vm.EmailDateDisplay.Should().Be("2025-02-28");
    }

    [Fact]
    public void EmailDateDisplay_WhenNull_ReturnsEmptyString()
    {
        var dto = MakeDto(emailDate: null);
        var vm = ReportRowViewModel.From(dto);

        vm.EmailDateDisplay.Should().BeEmpty();
    }

    [Fact]
    public void From_MapsAllDtoProperties()
    {
        var id = Guid.NewGuid();
        var dto = new ReportRowDto(
            Id: id,
            ReportName: "Quarterly Report",
            ImportDate: new DateOnly(2025, 4, 1),
            EmailDate: new DateOnly(2025, 3, 15),
            ImporterName: "IBKR Importer",
            Status: ReportStatus.Processed,
            FilingCount: 10,
            DisplayName: "Q1 2025 Report",
            EarliestIncomeDate: new DateOnly(2025, 1, 5));

        var vm = ReportRowViewModel.From(dto);

        vm.Id.Should().Be(id);
        vm.ReportName.Should().Be("Quarterly Report");
        vm.ImportDate.Should().Be(new DateOnly(2025, 4, 1));
        vm.EmailDate.Should().Be(new DateOnly(2025, 3, 15));
        vm.ImporterName.Should().Be("IBKR Importer");
        vm.Status.Should().Be(ReportStatus.Processed);
        vm.FilingCount.Should().Be(10);
        vm.DisplayName.Should().Be("Q1 2025 Report");
    }
}
