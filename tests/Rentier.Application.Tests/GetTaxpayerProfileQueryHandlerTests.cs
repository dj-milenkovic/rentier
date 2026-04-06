using FluentAssertions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Xunit;

namespace Rentier.Application.Tests;

public class GetTaxpayerProfileQueryHandlerTests
{
    private readonly ITaxpayerProfileRepository _repo = Substitute.For<ITaxpayerProfileRepository>();
    private readonly GetTaxpayerProfileQueryHandler _handler;

    public GetTaxpayerProfileQueryHandlerTests()
    {
        _handler = new GetTaxpayerProfileQueryHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_ExistingProfile_ReturnsMappedDto()
    {
        var id = Guid.NewGuid();
        var profile = new TaxpayerProfile(id, "1234567890123", "Marko", "Knez 1", "7101",
            phoneNumber: "+381641234567", email: "m@test.com");
        _repo.GetAsync().Returns(profile);

        var result = await _handler.HandleAsync(new GetTaxpayerProfileQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(id);
        result.Value.Jmbg.Should().Be("1234567890123");
        result.Value.PhoneNumber.Should().Be("+381641234567");
        result.Value.Email.Should().Be("m@test.com");
    }

    [Fact]
    public async Task HandleAsync_NoProfile_ReturnsSuccessWithNullDto()
    {
        _repo.GetAsync().Returns((TaxpayerProfile?)null);

        var result = await _handler.HandleAsync(new GetTaxpayerProfileQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
