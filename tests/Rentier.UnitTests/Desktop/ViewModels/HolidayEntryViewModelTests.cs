using FluentAssertions;
using Rentier.Application.DTOs;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Unit tests for HolidayEntryViewModel — covers FromDto round-trip,
/// property accessors, and the RaiseAndSetIfChanged setter.
/// </summary>
public class HolidayEntryViewModelTests
{
    private static readonly HolidayEntryDto SampleDto =
        new(new DateOnly(2025, 6, 15), "Vidovdan");

    [Fact]
    public void FromDto_ValidHolidayEntry_DatePropertyMatchesInput()
    {
        var vm = HolidayEntryViewModel.FromDto(SampleDto);

        vm.Date.Should().Be(new DateOnly(2025, 6, 15));
    }

    [Fact]
    public void FromDto_ValidHolidayEntry_NamePropertyMatchesInput()
    {
        var vm = HolidayEntryViewModel.FromDto(SampleDto);

        vm.Name.Should().Be("Vidovdan");
    }

    [Fact]
    public void ToDto_AfterFromDto_ProducesEquivalentDto()
    {
        var vm = HolidayEntryViewModel.FromDto(SampleDto);

        var dto = vm.ToDto();

        dto.Date.Should().Be(SampleDto.Date);
        dto.Name.Should().Be(SampleDto.Name);
    }

    [Fact]
    public void FromDto_EmptyName_DefaultsToEmptyString()
    {
        var dto = new HolidayEntryDto(new DateOnly(2025, 1, 1), "");

        var vm = HolidayEntryViewModel.FromDto(dto);

        vm.Name.Should().Be("");
    }

    [Fact]
    public void Name_WhenSetViaSetter_PropertyChanges()
    {
        var vm = HolidayEntryViewModel.FromDto(SampleDto);

        vm.Name = "NewName";

        vm.Name.Should().Be("NewName");
    }
}
