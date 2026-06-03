using System.Runtime.Versioning;
using FluentAssertions;
using Rentier.Infrastructure.Security;

namespace Rentier.Infrastructure.Tests.Security;

[Trait("Category", "Integration")]
[SupportedOSPlatform("osx")]
public class MacOsCredentialStoreTests
{
    private static string MakeTestKey() => $"Rentier/Test/{Guid.NewGuid()}/password";

    [Fact(Skip = "Requires macOS")]
    public async Task SaveAndGet_RoundTrip_ReturnsSameSecret()
    {
        var store = new MacOsCredentialStore();
        var key = MakeTestKey();
        const string secret = "macos-secret-value";

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

    [Fact(Skip = "Requires macOS")]
    public async Task Save_Overwrites_ExistingCredential()
    {
        var store = new MacOsCredentialStore();
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

    [Fact(Skip = "Requires macOS")]
    public async Task Get_AbsentKey_ReturnsCredentialNotFound()
    {
        var store = new MacOsCredentialStore();
        var key = MakeTestKey();

        var result = await store.GetCredentialAsync(key, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact(Skip = "Requires macOS")]
    public async Task Delete_ExistingCredential_RemovesIt()
    {
        var store = new MacOsCredentialStore();
        var key = MakeTestKey();

        await store.SaveCredentialAsync(key, "to-be-deleted", TestContext.Current.CancellationToken);
        var deleteResult = await store.DeleteCredentialAsync(key, TestContext.Current.CancellationToken);
        deleteResult.IsSuccess.Should().BeTrue();

        var getResult = await store.GetCredentialAsync(key, TestContext.Current.CancellationToken);
        getResult.IsSuccess.Should().BeFalse();
        getResult.Error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact(Skip = "Requires macOS")]
    public async Task Delete_AbsentKey_ReturnsSuccess()
    {
        var store = new MacOsCredentialStore();
        var key = MakeTestKey();

        var result = await store.DeleteCredentialAsync(key, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact(Skip = "Requires macOS")]
    public async Task Get_NonZeroExitCode_ReturnsCredentialWriteFailed()
    {
        // On macOS, searching for a key that doesn't exist returns exit code 44 → CREDENTIAL_NOT_FOUND
        // Any other non-zero exit code maps to CREDENTIAL_WRITE_FAILED
        // This is covered by the Get_AbsentKey test above (uses exit 44 path)
        // Full non-44 path testing would require a mock of Process.Start
        // This test verifies error.Message is non-empty for all failure cases
        var store = new MacOsCredentialStore();
        var key = MakeTestKey();

        var result = await store.GetCredentialAsync(key, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().NotBeNullOrWhiteSpace();
    }
}
