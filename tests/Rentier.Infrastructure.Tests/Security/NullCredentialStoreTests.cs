using FluentAssertions;
using Rentier.Application.Common;
using Rentier.Infrastructure.Security;

namespace Rentier.Infrastructure.Tests.Security;

[Trait("Category", "Integration")]
public class NullCredentialStoreTests
{
    private static readonly Error ProviderError =
        new("PROVIDER_UNAVAILABLE", "No credential store provider available on this platform.");

    private static NullCredentialStore MakeSut() => new(ProviderError);

    [Fact]
    public async Task SaveCredentialAsync_AlwaysReturnsInjectedError()
    {
        var sut = MakeSut();

        var result = await sut.SaveCredentialAsync("Rentier/Test/key", "secret", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ProviderError);
    }

    [Fact]
    public async Task GetCredentialAsync_AlwaysReturnsInjectedError()
    {
        var sut = MakeSut();

        var result = await sut.GetCredentialAsync("Rentier/Test/key", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ProviderError);
    }

    [Fact]
    public async Task DeleteCredentialAsync_AlwaysReturnsInjectedError()
    {
        var sut = MakeSut();

        var result = await sut.DeleteCredentialAsync("Rentier/Test/key", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ProviderError);
    }

    [Fact]
    public async Task SaveCredentialAsync_ReturnsExactErrorCode()
    {
        var customError = new Error("CUSTOM_CODE", "custom message");
        var sut = new NullCredentialStore(customError);

        var result = await sut.SaveCredentialAsync("key", "secret", TestContext.Current.CancellationToken);

        result.Error.Code.Should().Be("CUSTOM_CODE");
        result.Error.Message.Should().Be("custom message");
    }
}
