using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Xunit;

namespace Rentier.UnitTests.Application;

public class SetUserPreferenceCommandHandlerTests
{
    private readonly IUserPreferenceRepository _repo;
    private readonly SetUserPreferenceCommandHandler _handler;

    public SetUserPreferenceCommandHandlerTests()
    {
        _repo = Substitute.For<IUserPreferenceRepository>();
        _handler = new SetUserPreferenceCommandHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_NewKey_InsertsNewPreference()
    {
        _repo.GetAsync("Language", Arg.Any<CancellationToken>()).Returns((UserPreference?)null);

        var result = await _handler.HandleAsync(new SetUserPreferenceCommand("Language", "en"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).SaveAsync(
            Arg.Is<UserPreference>(p => p!.Key == "Language" && p.Value == "en"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ExistingKey_UpdatesValue()
    {
        var existing = new UserPreference("Language", "sr-Latn");
        _repo.GetAsync("Language", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.HandleAsync(new SetUserPreferenceCommand("Language", "en"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        existing.Value.Should().Be("en");
        await _repo.Received(1).SaveAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValueExceeds500Chars_ReturnsDomainFailure()
    {
        _repo.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((UserPreference?)null);
        var longValue = new string('x', 501);

        var result = await _handler.HandleAsync(new SetUserPreferenceCommand("Language", longValue), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DOMAIN_ERROR");
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_ReturnsInfrastructureFailure()
    {
        _repo.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<UserPreference?>(_ => throw new InvalidOperationException("DB error"));

        var result = await _handler.HandleAsync(new SetUserPreferenceCommand("Language", "en"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("INFRASTRUCTURE_ERROR");
    }
}
