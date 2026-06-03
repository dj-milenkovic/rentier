using System.Runtime.Versioning;
using FluentAssertions;
using Rentier.Infrastructure.Security;

namespace Rentier.Infrastructure.Tests.Security;

[Trait("Category", "Integration")]
[SupportedOSPlatform("windows")]
public class WindowsCredentialStoreTests
{
    private static string MakeTestKey() => $"Rentier/Test/{Guid.NewGuid()}/password";

    [Fact(Skip = "Requires Windows")]
    public async Task SaveAndGet_RoundTrip_ReturnsSameSecret()
    {
        var store = new WindowsCredentialStore();
        var key = MakeTestKey();
        const string secret = "my-s3cr3t-value";

        try
        {
            var saveResult = await store.SaveCredentialAsync(key, secret, TestContext.Current.CancellationToken);
            saveResult.IsSuccess.Should().BeTrue();

            var getResult = await store.GetCredentialAsync(key, TestContext.Current.CancellationToken);
            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().Be(secret);
        }
        finally
        {
            await store.DeleteCredentialAsync(key, TestContext.Current.CancellationToken);
        }
    }

    [Fact(Skip = "Requires Windows")]
    public async Task Save_Overwrites_ExistingCredential()
    {
        var store = new WindowsCredentialStore();
        var key = MakeTestKey();

        try
        {
            await store.SaveCredentialAsync(key, "original", TestContext.Current.CancellationToken);
            await store.SaveCredentialAsync(key, "updated", TestContext.Current.CancellationToken);

            var getResult = await store.GetCredentialAsync(key, TestContext.Current.CancellationToken);
            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().Be("updated");
        }
        finally
        {
            await store.DeleteCredentialAsync(key, TestContext.Current.CancellationToken);
        }
    }

    [Fact(Skip = "Requires Windows")]
    public async Task Get_AbsentKey_ReturnsCredentialNotFound()
    {
        var store = new WindowsCredentialStore();
        var key = MakeTestKey();

        var result = await store.GetCredentialAsync(key, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact(Skip = "Requires Windows")]
    public async Task Delete_ExistingCredential_RemovesIt()
    {
        var store = new WindowsCredentialStore();
        var key = MakeTestKey();

        await store.SaveCredentialAsync(key, "to-be-deleted", TestContext.Current.CancellationToken);
        var deleteResult = await store.DeleteCredentialAsync(key, TestContext.Current.CancellationToken);
        deleteResult.IsSuccess.Should().BeTrue();

        var getResult = await store.GetCredentialAsync(key, TestContext.Current.CancellationToken);
        getResult.IsSuccess.Should().BeFalse();
        getResult.Error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact(Skip = "Requires Windows")]
    public async Task Delete_AbsentKey_ReturnsSuccess()
    {
        var store = new WindowsCredentialStore();
        var key = MakeTestKey();

        var result = await store.DeleteCredentialAsync(key, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact(Skip = "Requires Windows")]
    public async Task Save_EmptyKey_ReturnsCredentialWriteFailed()
    {
        var store = new WindowsCredentialStore();

        var result = await store.SaveCredentialAsync("", "someValue", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_WRITE_FAILED");
        result.Error.Message.Should().Be("Key must not be empty");
    }

    [Fact(Skip = "Requires Windows")]
    public async Task Save_EmptySecret_ReturnsCredentialWriteFailed()
    {
        var store = new WindowsCredentialStore();

        var result = await store.SaveCredentialAsync("Rentier/Test/key", "", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_WRITE_FAILED");
        result.Error.Message.Should().Be("Secret must not be empty");
    }

    [Fact(Skip = "Requires Windows")]
    public async Task Save_UnicodeSecret_RoundTripsCorrectly()
    {
        var store = new WindowsCredentialStore();
        var key = MakeTestKey();
        const string secret = "pässwörd-日本語-🔐";

        try
        {
            await store.SaveCredentialAsync(key, secret, TestContext.Current.CancellationToken);
            var getResult = await store.GetCredentialAsync(key, TestContext.Current.CancellationToken);

            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().Be(secret);
        }
        finally
        {
            await store.DeleteCredentialAsync(key, TestContext.Current.CancellationToken);
        }
    }
}
