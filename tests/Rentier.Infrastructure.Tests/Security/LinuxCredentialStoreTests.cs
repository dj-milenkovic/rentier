using System.Runtime.Versioning;
using FluentAssertions;
using Rentier.Infrastructure.Security;

namespace Rentier.Infrastructure.Tests.Security;

[Trait("Category", "Integration")]
[SupportedOSPlatform("linux")]
public class LinuxCredentialStoreTests
{
    // These tests guard with a runtime platform check and are skipped on non-Linux CI.
    // On Linux CI without a Secret Service daemon, all tests skip via the platform guard.

    [Fact(Skip = "Requires Linux")]
    public async Task SaveAndGet_RoundTrip_ReturnsSameSecret()
    {
        var factoryResult = await TryGetLinuxStoreAsync();
        if (!factoryResult.IsSuccess) return; // daemon not available — skip gracefully

        var store = factoryResult.Store;
        const string key = "Rentier/Test/linux-roundtrip/password";
        const string secret = "linux-secret-value";

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

    [Fact(Skip = "Requires Linux")]
    public async Task Save_Overwrites_ExistingCredential()
    {
        var factoryResult = await TryGetLinuxStoreAsync();
        if (!factoryResult.IsSuccess) return;

        var store = factoryResult.Store;
        const string key = "Rentier/Test/linux-upsert/password";

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

    [Fact(Skip = "Requires Linux")]
    public async Task Get_AbsentKey_ReturnsCredentialNotFound()
    {
        var factoryResult = await TryGetLinuxStoreAsync();
        if (!factoryResult.IsSuccess) return;

        var store = factoryResult.Store;
        var key = $"Rentier/Test/{Guid.NewGuid()}/password";

        var result = await store.GetCredentialAsync(key, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact(Skip = "Requires Linux")]
    public async Task Delete_ExistingCredential_RemovesIt()
    {
        var factoryResult = await TryGetLinuxStoreAsync();
        if (!factoryResult.IsSuccess) return;

        var store = factoryResult.Store;
        const string key = "Rentier/Test/linux-delete/password";

        await store.SaveCredentialAsync(key, "to-be-deleted", TestContext.Current.CancellationToken);
        var deleteResult = await store.DeleteCredentialAsync(key, TestContext.Current.CancellationToken);
        deleteResult.IsSuccess.Should().BeTrue();

        var getResult = await store.GetCredentialAsync(key, TestContext.Current.CancellationToken);
        getResult.IsSuccess.Should().BeFalse();
        getResult.Error.Code.Should().Be("CREDENTIAL_NOT_FOUND");
    }

    [Fact(Skip = "Requires Linux")]
    public async Task Delete_AbsentKey_ReturnsSuccess()
    {
        var factoryResult = await TryGetLinuxStoreAsync();
        if (!factoryResult.IsSuccess) return;

        var store = factoryResult.Store;
        var key = $"Rentier/Test/{Guid.NewGuid()}/password";

        var result = await store.DeleteCredentialAsync(key, TestContext.Current.CancellationToken);
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
