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
[Trait("Category", "Integration")]
public sealed class TaxpayerProfileScenario : IAsyncLifetime
{
    private readonly ScenarioFixture _fixture = new();
    private ITaxpayerProfileRepository _repository = null!;

    public async ValueTask InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _repository = _fixture.Get<ITaxpayerProfileRepository>();
    }

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task SaveProfile_NewProfile_CanBeRetrievedById()
    {
        var saveHandler = new SaveTaxpayerProfileCommandHandler(_repository);
        var getHandler = new GetTaxpayerProfileQueryHandler(_repository);

        var command = new SaveTaxpayerProfileCommand(
            Jmbg: "1234567890123",
            FullName: "Petar Petrovic",
            Address: "Bulevar Kralja Aleksandra 1",
            OpstinaCode: "049",
            PhoneNumber: "+381641234567",
            Email: "petar@example.com");

        var saveResult = await saveHandler.HandleAsync(command, TestContext.Current.CancellationToken);
        var getResult = await getHandler.HandleAsync(new GetTaxpayerProfileQuery(), TestContext.Current.CancellationToken);

        saveResult.IsSuccess.Should().BeTrue("save should succeed");
        getResult.IsSuccess.Should().BeTrue("get should succeed");

        var dto = getResult.Value;
        dto.Should().NotBeNull("profile should exist after save");
        dto!.Jmbg.Should().Be("1234567890123");
        dto.FullName.Should().Be("Petar Petrovic");
        dto.Address.Should().Be("Bulevar Kralja Aleksandra 1");
        dto.OpstinaCode.Should().Be("049");
        dto.PhoneNumber.Should().Be("+381641234567");
        dto.Email.Should().Be("petar@example.com");

        var profileFromRepo = await _repository.GetAsync(TestContext.Current.CancellationToken);
        profileFromRepo.Should().NotBeNull();
        profileFromRepo!.Id.Should().Be(dto.Id);
    }

    [Fact]
    public async Task SaveProfile_WhenUpdated_ReturnsLatestData()
    {
        var saveHandler = new SaveTaxpayerProfileCommandHandler(_repository);
        var getHandler = new GetTaxpayerProfileQueryHandler(_repository);

        var initialCommand = new SaveTaxpayerProfileCommand(
            Jmbg: "1234567890123",
            FullName: "Petar Petrovic",
            Address: "Bulevar Kralja Aleksandra 1",
            OpstinaCode: "049",
            PhoneNumber: "+381641234567",
            Email: "petar@example.com");

        var updatedCommand = new SaveTaxpayerProfileCommand(
            Jmbg: "1234567890123",
            FullName: "Petar Petrovic Novi",
            Address: "Knez Mihailova 15",
            OpstinaCode: "050",
            PhoneNumber: "+381649999999",
            Email: "petar.novi@example.com");

        var initialSaveResult = await saveHandler.HandleAsync(initialCommand, TestContext.Current.CancellationToken);
        var initialGetResult = await getHandler.HandleAsync(new GetTaxpayerProfileQuery(), TestContext.Current.CancellationToken);
        var initialId = initialGetResult.Value!.Id;

        var updateSaveResult = await saveHandler.HandleAsync(updatedCommand, TestContext.Current.CancellationToken);
        var finalGetResult = await getHandler.HandleAsync(new GetTaxpayerProfileQuery(), TestContext.Current.CancellationToken);

        initialSaveResult.IsSuccess.Should().BeTrue();
        updateSaveResult.IsSuccess.Should().BeTrue();
        finalGetResult.IsSuccess.Should().BeTrue();

        var dto = finalGetResult.Value;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(initialId, "ID should remain the same after update");
        dto.Jmbg.Should().Be("1234567890123");
        dto.FullName.Should().Be("Petar Petrovic Novi");
        dto.Address.Should().Be("Knez Mihailova 15");
        dto.OpstinaCode.Should().Be("050");
        dto.PhoneNumber.Should().Be("+381649999999");
        dto.Email.Should().Be("petar.novi@example.com");

        var profileFromRepo = await _repository.GetAsync(TestContext.Current.CancellationToken);
        profileFromRepo.Should().NotBeNull();
        profileFromRepo!.FullName.Should().Be("Petar Petrovic Novi");
        profileFromRepo.Address.Should().Be("Knez Mihailova 15");
    }

    [Fact]
    public async Task SaveProfile_InvalidJmbg_ReturnsDomainError()
    {
        var saveHandler = new SaveTaxpayerProfileCommandHandler(_repository);

        var command = new SaveTaxpayerProfileCommand(
            Jmbg: "invalid",
            FullName: "Test User",
            Address: "Test Address",
            OpstinaCode: "049");

        var result = await saveHandler.HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse("save should fail with invalid JMBG");
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        result.Error.Message.Should().Contain("JMBG");

        var profile = await _repository.GetAsync(TestContext.Current.CancellationToken);
        profile.Should().BeNull("invalid profile should not be persisted");
    }
}
