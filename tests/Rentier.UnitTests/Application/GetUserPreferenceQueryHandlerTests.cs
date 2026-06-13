using FluentAssertions;
using NSubstitute;
using Rentier.Application.Handlers;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Xunit;

namespace Rentier.UnitTests.Application;

public class GetUserPreferenceQueryHandlerTests
{
    private readonly IUserPreferenceRepository _repo;
    private readonly GetUserPreferenceQueryHandler _handler;

    public GetUserPreferenceQueryHandlerTests()
    {
        _repo = Substitute.For<IUserPreferenceRepository>();
        _handler = new GetUserPreferenceQueryHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_KeyExists_ReturnsValue()
    {
        var pref = new UserPreference("Language", "en");
        _repo.GetAsync("Language", Arg.Any<CancellationToken>()).Returns(pref);

        var result = await _handler.HandleAsync(new GetUserPreferenceQuery("Language"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("en");
    }

    [Fact]
    public async Task HandleAsync_KeyNotFound_ReturnsNullSuccess()
    {
        _repo.GetAsync("Language", Arg.Any<CancellationToken>()).Returns((UserPreference?)null);

        var result = await _handler.HandleAsync(new GetUserPreferenceQuery("Language"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_ReturnsFailure()
    {
        _repo.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<UserPreference?>(_ => throw new InvalidOperationException("DB error"));

        var result = await _handler.HandleAsync(new GetUserPreferenceQuery("Language"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("INFRASTRUCTURE_ERROR");
    }
}
