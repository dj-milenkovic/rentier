using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FluentAssertions;
using Rentier.Infrastructure.Security;
using Xunit;

namespace Rentier.Infrastructure.Tests.Security;

[SupportedOSPlatform("linux")]
public class LinuxCredentialStoreTests
{
    // These tests guard with a runtime platform check and are skipped on non-Linux CI.
    // On Linux CI without a Secret Service daemon, all tests skip via the platform guard.

    [Fact]
    public async Task SaveAndGet_RoundTrip_ReturnsSameSecret()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        var factoryResult = await TryGetLinuxStoreAsync();
        if (!factoryResult.IsSuccess) return; // daemon not available — skip gracefully

        var store = factoryResult.Store;
        const string key = "Rentier/Test/linux-roundtrip/password";
        const string secret = "linux-secret-value";

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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        var factoryResult = await TryGetLinuxStoreAsync();
        if (!factoryResult.IsSuccess) return;

        var store = factoryResult.Store;
        const string key = "Rentier/Test/linux-upsert/password";

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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        var factoryResult = await TryGetLinuxStoreAsync();
        if (!factoryResult.IsSuccess) return;

        var store = factoryResult.Store;
        var key = $"Rentier/Test/{Guid.NewGuid()}/password";

        var result = await store.GetCredentialAsync(key);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_ExistingCredential_RemovesIt()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        var factoryResult = await TryGetLinuxStoreAsync();
        if (!factoryResult.IsSuccess) return;

        var store = factoryResult.Store;
        const string key = "Rentier/Test/linux-delete/password";

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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        var factoryResult = await TryGetLinuxStoreAsync();
        if (!factoryResult.IsSuccess) return;

        var store = factoryResult.Store;
        var key = $"Rentier/Test/{Guid.NewGuid()}/password";

        var result = await store.DeleteCredentialAsync(key);
        result.IsSuccess.Should().BeTrue();
    }

    // Helper: attempt to create a real Linux store via the factory
    private static async Task<(bool IsSuccess, LinuxCredentialStore Store)> TryGetLinuxStoreAsync()
    {
        var factoryResult = await CredentialStoreFactory.CreateAsync();
        if (!factoryResult.IsSuccess || factoryResult.Value.Store is not LinuxCredentialStore linuxStore)
            return (false, null!);
        return (true, linuxStore);
    }
}
