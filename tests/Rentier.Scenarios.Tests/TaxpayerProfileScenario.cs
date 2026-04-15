using FluentAssertions;
using Rentier.Application.Commands;
using Rentier.Application.Handlers;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.Scenarios.Tests;

/// <summary>
/// Scenario tests for TaxpayerProfile lifecycle operations using real infrastructure.
/// </summary>
[Trait("Category", "Scenario")]
public sealed class TaxpayerProfileScenario : IDisposable
{
    private readonly ScenarioFixture _fixture;
    private readonly ITaxpayerProfileRepository _repository;

    public TaxpayerProfileScenario()
    {
        _fixture = new ScenarioFixture();
        _repository = _fixture.GetService<ITaxpayerProfileRepository>();
    }

    [Fact]
    public async Task SaveProfile_NewProfile_CanBeRetrievedById()
    {
        // Arrange
        var saveHandler = new SaveTaxpayerProfileCommandHandler(_repository);
        var getHandler = new GetTaxpayerProfileQueryHandler(_repository);

        var command = new SaveTaxpayerProfileCommand(
            Jmbg: "1234567890123",
            FullName: "Petar Petrovic",
            Address: "Bulevar Kralja Aleksandra 1",
            OpstinaCode: "70101",
            PhoneNumber: "+381641234567",
            Email: "petar@example.com");

        // Act
        var saveResult = await saveHandler.HandleAsync(command);
        var getResult = await getHandler.HandleAsync(new GetTaxpayerProfileQuery());

        // Assert - handler results
        saveResult.IsSuccess.Should().BeTrue("save should succeed");
        getResult.IsSuccess.Should().BeTrue("get should succeed");

        // Assert - retrieved data matches saved data
        var dto = getResult.Value;
        dto.Should().NotBeNull("profile should exist after save");
        dto!.Jmbg.Should().Be("1234567890123");
        dto.FullName.Should().Be("Petar Petrovic");
        dto.Address.Should().Be("Bulevar Kralja Aleksandra 1");
        dto.OpstinaCode.Should().Be("70101");
        dto.PhoneNumber.Should().Be("+381641234567");
        dto.Email.Should().Be("petar@example.com");

        // Assert - verify via repository query (end state assertion per testing-strategy.md)
        var profileFromRepo = await _repository.GetAsync();
        profileFromRepo.Should().NotBeNull();
        profileFromRepo!.Id.Should().Be(dto.Id);
    }

    [Fact]
    public async Task SaveProfile_WhenUpdated_ReturnsLatestData()
    {
        // Arrange
        var saveHandler = new SaveTaxpayerProfileCommandHandler(_repository);
        var getHandler = new GetTaxpayerProfileQueryHandler(_repository);

        var initialCommand = new SaveTaxpayerProfileCommand(
            Jmbg: "1234567890123",
            FullName: "Petar Petrovic",
            Address: "Bulevar Kralja Aleksandra 1",
            OpstinaCode: "70101",
            PhoneNumber: "+381641234567",
            Email: "petar@example.com");

        var updatedCommand = new SaveTaxpayerProfileCommand(
            Jmbg: "1234567890123",
            FullName: "Petar Petrovic Novi",
            Address: "Knez Mihailova 15",
            OpstinaCode: "70102",
            PhoneNumber: "+381649999999",
            Email: "petar.novi@example.com");

        // Act
        var initialSaveResult = await saveHandler.HandleAsync(initialCommand);
        var initialGetResult = await getHandler.HandleAsync(new GetTaxpayerProfileQuery());
        var initialId = initialGetResult.Value!.Id;

        var updateSaveResult = await saveHandler.HandleAsync(updatedCommand);
        var finalGetResult = await getHandler.HandleAsync(new GetTaxpayerProfileQuery());

        // Assert - all operations succeeded
        initialSaveResult.IsSuccess.Should().BeTrue();
        updateSaveResult.IsSuccess.Should().BeTrue();
        finalGetResult.IsSuccess.Should().BeTrue();

        // Assert - updated data is returned
        var dto = finalGetResult.Value;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(initialId, "ID should remain the same after update");
        dto.Jmbg.Should().Be("1234567890123");
        dto.FullName.Should().Be("Petar Petrovic Novi");
        dto.Address.Should().Be("Knez Mihailova 15");
        dto.OpstinaCode.Should().Be("70102");
        dto.PhoneNumber.Should().Be("+381649999999");
        dto.Email.Should().Be("petar.novi@example.com");

        // Assert - verify via repository query
        var profileFromRepo = await _repository.GetAsync();
        profileFromRepo.Should().NotBeNull();
        profileFromRepo!.FullName.Should().Be("Petar Petrovic Novi");
        profileFromRepo.Address.Should().Be("Knez Mihailova 15");
    }

    [Fact]
    public async Task SaveProfile_InvalidJmbg_ReturnsDomainError()
    {
        // Arrange
        var saveHandler = new SaveTaxpayerProfileCommandHandler(_repository);

        var command = new SaveTaxpayerProfileCommand(
            Jmbg: "invalid", // Too short, should fail domain validation
            FullName: "Test User",
            Address: "Test Address",
            OpstinaCode: "70101");

        // Act
        var result = await saveHandler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse("save should fail with invalid JMBG");
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        result.Error.Message.Should().Contain("JMBG");

        // Assert - no profile was persisted
        var profile = await _repository.GetAsync();
        profile.Should().BeNull("invalid profile should not be persisted");
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
