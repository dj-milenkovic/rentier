using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Xunit;

namespace Rentier.UnitTests;

public class SaveTaxpayerProfileCommandHandlerTests
{
    private readonly ITaxpayerProfileRepository _repo = Substitute.For<ITaxpayerProfileRepository>();
    private readonly SaveTaxpayerProfileCommandHandler _handler;

    public SaveTaxpayerProfileCommandHandlerTests()
    {
        _handler = new SaveTaxpayerProfileCommandHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_NoExistingProfile_InsertsNewProfile()
    {
        _repo.GetAsync().Returns((TaxpayerProfile?)null);

        var command = new SaveTaxpayerProfileCommand("1234567890123", "Test User", "Test Address", "7101");
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).SaveAsync(
            Arg.Is<TaxpayerProfile>(p =>
                p.Jmbg == "1234567890123" &&
                p.FullName == "Test User"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ExistingProfile_UpdatesWithSameId()
    {
        var existingId = Guid.NewGuid();
        var existing = new TaxpayerProfile(existingId, "1234567890123", "Old Name", "Old Address", "7101");
        _repo.GetAsync().Returns(existing);

        var command = new SaveTaxpayerProfileCommand("1234567890123", "New Name", "New Address", "7102");
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).SaveAsync(
            Arg.Is<TaxpayerProfile>(p => p.Id == existingId && p.FullName == "New Name"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidJmbg_ReturnsFailure()
    {
        _repo.GetAsync().Returns((TaxpayerProfile?)null);

        var command = new SaveTaxpayerProfileCommand("INVALID_JMBG", "Test User", "Test Address", "7101");
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DOMAIN_ERROR");
        await _repo.DidNotReceive().SaveAsync(Arg.Any<TaxpayerProfile>(), Arg.Any<CancellationToken>());
    }
}
