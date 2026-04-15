using FluentAssertions;
using Rentier.Application.Commands;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.Scenarios.Tests;

/// <summary>
/// Scenario tests for Filing lifecycle state transitions using real infrastructure.
/// Tests the Init → Filed → Paid state machine via UpdateFilingStatusCommandHandler.
/// </summary>
[Trait("Category", "Scenario")]
public sealed class FilingLifecycleScenario : IDisposable
{
    private readonly ScenarioFixture _fixture;
    private readonly IFilingRepository _filingRepository;
    private readonly ITaxpayerProfileRepository _profileRepository;

    public FilingLifecycleScenario()
    {
        _fixture = new ScenarioFixture();
        _filingRepository = _fixture.GetService<IFilingRepository>();
        _profileRepository = _fixture.GetService<ITaxpayerProfileRepository>();
    }

    [Fact]
    public async Task FilingLifecycle_FromInitToFiled_UpdatesStatusCorrectly()
    {
        // Arrange - create taxpayer profile and filing directly in DB
        var taxpayerProfile = new TaxpayerProfile(
            id: Guid.NewGuid(),
            jmbg: "1234567890123",
            fullName: "Test User",
            address: "Test Address",
            opstinaCode: "70101");

        await _profileRepository.SaveAsync(taxpayerProfile);

        var filing = Filing.CreateFromIncome(
            taxpayerProfileId: taxpayerProfile.Id,
            incomeType: IncomeType.Dividend,
            payingEntity: "Test Corp",
            incomeDate: new DateOnly(2024, 6, 15),
            grossIncomeRsd: 100_000m,
            whtPaidRsd: 15_000m,
            grossTaxPayableRsd: 15_000m,
            taxPayableRsd: 0m,
            filingDeadline: new DateOnly(2024, 7, 30));

        await _filingRepository.AddAsync(filing);

        var handler = new UpdateFilingStatusCommandHandler(_filingRepository);
        var command = new UpdateFilingStatusCommand(filing.Id, FilingStatus.Filed);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert - handler succeeded
        result.IsSuccess.Should().BeTrue("transition from Init to Filed should succeed");

        // Assert - verify DB state via repository query
        var updatedFiling = await _filingRepository.GetByIdAsync(filing.Id);
        updatedFiling.Should().NotBeNull();
        updatedFiling!.Status.Should().Be(FilingStatus.Filed, "status should be Filed after transition");
    }

    [Fact]
    public async Task FilingLifecycle_FromFiledToPaid_UpdatesStatusCorrectly()
    {
        // Arrange - create taxpayer profile and filing already in Filed status
        var taxpayerProfile = new TaxpayerProfile(
            id: Guid.NewGuid(),
            jmbg: "1234567890123",
            fullName: "Test User",
            address: "Test Address",
            opstinaCode: "70101");

        await _profileRepository.SaveAsync(taxpayerProfile);

        var filing = Filing.CreateFromIncome(
            taxpayerProfileId: taxpayerProfile.Id,
            incomeType: IncomeType.Interest,
            payingEntity: "Bank XYZ",
            incomeDate: new DateOnly(2024, 3, 10),
            grossIncomeRsd: 50_000m,
            whtPaidRsd: 7_500m,
            grossTaxPayableRsd: 7_500m,
            taxPayableRsd: 0m,
            filingDeadline: new DateOnly(2024, 4, 30));

        await _filingRepository.AddAsync(filing);

        // First transition: Init → Filed
        var handler = new UpdateFilingStatusCommandHandler(_filingRepository);
        var toFiledResult = await handler.HandleAsync(new UpdateFilingStatusCommand(filing.Id, FilingStatus.Filed));
        toFiledResult.IsSuccess.Should().BeTrue();

        // Act - Second transition: Filed → Paid
        var toPaidResult = await handler.HandleAsync(new UpdateFilingStatusCommand(filing.Id, FilingStatus.Paid));

        // Assert
        toPaidResult.IsSuccess.Should().BeTrue("transition from Filed to Paid should succeed");

        // Assert - verify DB state
        var updatedFiling = await _filingRepository.GetByIdAsync(filing.Id);
        updatedFiling.Should().NotBeNull();
        updatedFiling!.Status.Should().Be(FilingStatus.Paid, "status should be Paid after transition");
    }

    [Fact]
    public async Task FilingLifecycle_InvalidTransition_ReturnsDomainError()
    {
        // Arrange - create filing in Init status and try to skip to Paid
        var taxpayerProfile = new TaxpayerProfile(
            id: Guid.NewGuid(),
            jmbg: "9876543210987",
            fullName: "Another User",
            address: "Another Address",
            opstinaCode: "70102");

        await _profileRepository.SaveAsync(taxpayerProfile);

        var filing = Filing.CreateFromIncome(
            taxpayerProfileId: taxpayerProfile.Id,
            incomeType: IncomeType.Dividend,
            payingEntity: "Invalid Corp",
            incomeDate: new DateOnly(2024, 5, 20),
            grossIncomeRsd: 75_000m,
            whtPaidRsd: 11_250m,
            grossTaxPayableRsd: 11_250m,
            taxPayableRsd: 0m,
            filingDeadline: new DateOnly(2024, 6, 30));

        await _filingRepository.AddAsync(filing);

        var handler = new UpdateFilingStatusCommandHandler(_filingRepository);

        // Act - try invalid transition Init → Paid (should fail)
        var result = await handler.HandleAsync(new UpdateFilingStatusCommand(filing.Id, FilingStatus.Paid));

        // Assert
        result.IsSuccess.Should().BeFalse("Init → Paid is not a valid transition");
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        result.Error.Message.Should().Contain("Invalid Filing status transition");

        // Assert - verify DB state unchanged
        var unchangedFiling = await _filingRepository.GetByIdAsync(filing.Id);
        unchangedFiling.Should().NotBeNull();
        unchangedFiling!.Status.Should().Be(FilingStatus.Init, "status should remain Init after failed transition");
    }

    [Fact]
    public async Task FilingLifecycle_NonExistentFiling_ReturnsNotFoundError()
    {
        // Arrange
        var handler = new UpdateFilingStatusCommandHandler(_filingRepository);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await handler.HandleAsync(new UpdateFilingStatusCommand(nonExistentId, FilingStatus.Filed));

        // Assert
        result.IsSuccess.Should().BeFalse("updating non-existent filing should fail");
        result.Error.Code.Should().Be("NOT_FOUND");
        result.Error.Message.Should().Contain(nonExistentId.ToString());
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
