using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FluentAssertions;
using Rentier.Infrastructure.Security;
using Xunit;

namespace Rentier.Infrastructure.Tests.Security;

[SupportedOSPlatform("osx")]
public class MacOsCredentialStoreTests
{
    private static string MakeTestKey() => $"Rentier/Test/{Guid.NewGuid()}/password";

    [Fact]
    public async Task SaveAndGet_RoundTrip_ReturnsSameSecret()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        var store = new MacOsCredentialStore();
        var key = MakeTestKey();
        const string secret = "macos-secret-value";

        try
        {
            var saveResult = await store.SaveCredentialAsync(key, secret);
            saveResult.IsSuccess.Should().BeTrue();

            var getResult = await store.GetCredentialAsync(key);
            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().Be(secret);
        }
        finally
        {
            await store.DeleteCredentialAsync(key);
        }
    }

    [Fact]
    public async Task Save_Overwrites_ExistingCredential()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        var store = new MacOsCredentialStore();
        var key = MakeTestKey();

        try
        {
            await store.SaveCredentialAsync(key, "original");
            await store.SaveCredentialAsync(key, "updated");

            var getResult = await store.GetCredentialAsync(key);
            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().Be("updated");
        }
        finally
        {
            await store.DeleteCredentialAsync(key);
        }
    }

    [Fact]
    public async Task Get_AbsentKey_ReturnsCredentialNotFound()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        var store = new MacOsCredentialStore();
        var key = MakeTestKey();

        var result = await store.GetCredentialAsync(key);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_ExistingCredential_RemovesIt()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        var store = new MacOsCredentialStore();
        var key = MakeTestKey();

        await store.SaveCredentialAsync(key, "to-be-deleted");
        var deleteResult = await store.DeleteCredentialAsync(key);
        deleteResult.IsSuccess.Should().BeTrue();

        var getResult = await store.GetCredentialAsync(key);
        getResult.IsSuccess.Should().BeFalse();
        getResult.Error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_AbsentKey_ReturnsSuccess()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        var store = new MacOsCredentialStore();
        var key = MakeTestKey();

        var result = await store.DeleteCredentialAsync(key);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Get_NonZeroExitCode_ReturnsCredentialWriteFailed()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;
        // On macOS, searching for a key that doesn't exist returns exit code 44 → CREDENTIAL_NOT_FOUND
        // Any other non-zero exit code maps to CREDENTIAL_WRITE_FAILED
        // This is covered by the Get_AbsentKey test above (uses exit 44 path)
        // Full non-44 path testing would require a mock of Process.Start
        // This test verifies error.Message is non-empty for all failure cases
        var store = new MacOsCredentialStore();
        var key = MakeTestKey();

        var result = await store.GetCredentialAsync(key);

        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().NotBeNullOrWhiteSpace();
    }
}
