using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FluentAssertions;
using Rentier.Application.Common;
using Rentier.Infrastructure.Security;
using Xunit;

namespace Rentier.Infrastructure.Tests.Security;

[SupportedOSPlatform("windows")]
public class WindowsCredentialStoreTests
{
    private static string MakeTestKey() => $"Rentier/Test/{Guid.NewGuid()}/password";

    [Fact]
    public async Task SaveAndGet_RoundTrip_ReturnsSameSecret()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var store = new WindowsCredentialStore();
        var key = MakeTestKey();
        const string secret = "my-s3cr3t-value";

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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var store = new WindowsCredentialStore();
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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var store = new WindowsCredentialStore();
        var key = MakeTestKey();

        var result = await store.GetCredentialAsync(key);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_ExistingCredential_RemovesIt()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var store = new WindowsCredentialStore();
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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var store = new WindowsCredentialStore();
        var key = MakeTestKey();

        var result = await store.DeleteCredentialAsync(key);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Save_EmptyKey_ReturnsCredentialWriteFailed()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var store = new WindowsCredentialStore();

        var result = await store.SaveCredentialAsync("", "someValue");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_WRITE_FAILED");
        result.Error.Message.Should().Be("Key must not be empty");
    }

    [Fact]
    public async Task Save_EmptySecret_ReturnsCredentialWriteFailed()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var store = new WindowsCredentialStore();

        var result = await store.SaveCredentialAsync("Rentier/Test/key", "");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_WRITE_FAILED");
        result.Error.Message.Should().Be("Secret must not be empty");
    }

    [Fact]
    public async Task Save_UnicodeSecret_RoundTripsCorrectly()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var store = new WindowsCredentialStore();
        var key = MakeTestKey();
        const string secret = "pässwörd-日本語-🔐";

        try
        {
            await store.SaveCredentialAsync(key, secret);
            var getResult = await store.GetCredentialAsync(key);

            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().Be(secret);
        }
        finally
        {
            await store.DeleteCredentialAsync(key);
        }
    }
}
