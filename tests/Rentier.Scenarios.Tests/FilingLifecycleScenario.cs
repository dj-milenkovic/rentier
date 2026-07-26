using FluentAssertions;
using Rentier.Application.Commands;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.Scenarios.Tests;

/// <summary>
/// Scenario tests for Filing lifecycle state transitions using real infrastructure.
/// Tests the Init → Filed → Paid state machine via UpdateFilingStatusCommandHandler.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FilingLifecycleScenario : IAsyncLifetime
{
    private readonly ScenarioFixture _fixture = new();
    private IFilingRepository _filingRepository = null!;
    private ITaxpayerProfileRepository _profileRepository = null!;

    public async ValueTask InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _filingRepository = _fixture.Get<IFilingRepository>();
        _profileRepository = _fixture.Get<ITaxpayerProfileRepository>();
    }

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task FilingLifecycle_FromInitToFiled_UpdatesStatusCorrectly()
    {
        var taxpayerProfile = new TaxpayerProfile(
            id: Guid.NewGuid(),
            jmbg: "1234567890123",
            fullName: "Test User",
            address: "Test Address",
            opstinaCode: "049");

        await _profileRepository.SaveAsync(taxpayerProfile, TestContext.Current.CancellationToken);

        var filing = Filing.CreateFromIncome(
            new FilingInfo(
                incomeType: IncomeType.Dividend,
                payingEntity: "Test Corp",
                incomeDate: new DateOnly(2024, 6, 15),
                grossIncomeRsd: 100_000m,
                whtPaidRsd: 15_000m,
                grossTaxPayableRsd: 15_000m,
                taxPayableRsd: 0m),
            taxpayerProfileId: taxpayerProfile.Id,
            filingDeadline: new DateOnly(2024, 7, 30),
            provenance: new FilingProvenance());

        await _filingRepository.AddAsync(filing, TestContext.Current.CancellationToken);

        var handler = new UpdateFilingStatusCommandHandler(_filingRepository);
        var command = new UpdateFilingStatusCommand(filing.Id, FilingStatus.Filed);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue("transition from Init to Filed should succeed");

        var updatedFiling = await _filingRepository.GetByIdAsync(filing.Id, TestContext.Current.CancellationToken);
        updatedFiling.Should().NotBeNull();
        updatedFiling!.Status.Should().Be(FilingStatus.Filed, "status should be Filed after transition");
    }

    [Fact]
    public async Task FilingLifecycle_FromFiledToPaid_UpdatesStatusCorrectly()
    {
        var taxpayerProfile = new TaxpayerProfile(
            id: Guid.NewGuid(),
            jmbg: "1234567890123",
            fullName: "Test User",
            address: "Test Address",
            opstinaCode: "049");

        await _profileRepository.SaveAsync(taxpayerProfile, TestContext.Current.CancellationToken);

        var filing = Filing.CreateFromIncome(
            new FilingInfo(
                incomeType: IncomeType.Interest,
                payingEntity: "Bank XYZ",
                incomeDate: new DateOnly(2024, 3, 10),
                grossIncomeRsd: 50_000m,
                whtPaidRsd: 7_500m,
                grossTaxPayableRsd: 7_500m,
                taxPayableRsd: 0m),
            taxpayerProfileId: taxpayerProfile.Id,
            filingDeadline: new DateOnly(2024, 4, 30),
            provenance: new FilingProvenance());

        await _filingRepository.AddAsync(filing, TestContext.Current.CancellationToken);

        var handler = new UpdateFilingStatusCommandHandler(_filingRepository);
        var toFiledResult = await handler.HandleAsync(
            new UpdateFilingStatusCommand(filing.Id, FilingStatus.Filed),
            TestContext.Current.CancellationToken);
        toFiledResult.IsSuccess.Should().BeTrue();

        var toPaidResult = await handler.HandleAsync(
            new UpdateFilingStatusCommand(filing.Id, FilingStatus.Paid),
            TestContext.Current.CancellationToken);

        toPaidResult.IsSuccess.Should().BeTrue("transition from Filed to Paid should succeed");

        var updatedFiling = await _filingRepository.GetByIdAsync(filing.Id, TestContext.Current.CancellationToken);
        updatedFiling.Should().NotBeNull();
        updatedFiling!.Status.Should().Be(FilingStatus.Paid, "status should be Paid after transition");
    }

    [Fact]
    public async Task FilingLifecycle_InvalidTransition_ReturnsDomainError()
    {
        var taxpayerProfile = new TaxpayerProfile(
            id: Guid.NewGuid(),
            jmbg: "9876543210987",
            fullName: "Another User",
            address: "Another Address",
            opstinaCode: "050");

        await _profileRepository.SaveAsync(taxpayerProfile, TestContext.Current.CancellationToken);

        var filing = Filing.CreateFromIncome(
            new FilingInfo(
                incomeType: IncomeType.Dividend,
                payingEntity: "Invalid Corp",
                incomeDate: new DateOnly(2024, 5, 20),
                grossIncomeRsd: 75_000m,
                whtPaidRsd: 11_250m,
                grossTaxPayableRsd: 11_250m,
                taxPayableRsd: 0m),
            taxpayerProfileId: taxpayerProfile.Id,
            filingDeadline: new DateOnly(2024, 6, 30),
            provenance: new FilingProvenance());

        await _filingRepository.AddAsync(filing, TestContext.Current.CancellationToken);

        var handler = new UpdateFilingStatusCommandHandler(_filingRepository);

        var result = await handler.HandleAsync(
            new UpdateFilingStatusCommand(filing.Id, FilingStatus.Paid),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse("Init → Paid is not a valid transition");
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        result.Error.Message.Should().Contain("Invalid Filing status transition");

        var unchangedFiling = await _filingRepository.GetByIdAsync(filing.Id, TestContext.Current.CancellationToken);
        unchangedFiling.Should().NotBeNull();
        unchangedFiling!.Status.Should().Be(FilingStatus.Init, "status should remain Init after failed transition");
    }

    [Fact]
    public async Task FilingLifecycle_NonExistentFiling_ReturnsNotFoundError()
    {
        var handler = new UpdateFilingStatusCommandHandler(_filingRepository);
        var nonExistentId = Guid.NewGuid();

        var result = await handler.HandleAsync(
            new UpdateFilingStatusCommand(nonExistentId, FilingStatus.Filed),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse("updating non-existent filing should fail");
        result.Error.Code.Should().Be("NOT_FOUND");
        result.Error.Message.Should().Contain(nonExistentId.ToString());
    }
}
