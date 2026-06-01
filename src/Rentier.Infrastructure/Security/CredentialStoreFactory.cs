using System.Runtime.InteropServices;
using DBus.Services.Secrets;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Tmds.DBus.Protocol;

namespace Rentier.Infrastructure.Security;

/// <summary>
/// Selects the appropriate credential store provider at application startup
/// based on the detected operating system.
/// </summary>
public static class CredentialStoreFactory
{
    /// <summary>
    /// Creates the credential store for the current platform.
    /// </summary>
    /// <returns>
    /// A <see cref="Result{TValue, TError}"/> containing the selected <see cref="ICredentialStore"/>
    /// and <see cref="ProviderInfo"/> on success, or an <see cref="Error"/> on failure.
    /// </returns>
    public static async Task<Result<(ICredentialStore Store, ProviderInfo Info), Error>> CreateAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var store = new WindowsCredentialStore();
            var info = new ProviderInfo("Windows Credential Manager", "Windows");
            return Result<(ICredentialStore, ProviderInfo), Error>.Success((store, info));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var store = new MacOsCredentialStore();
            var info = new ProviderInfo("macOS Keychain", "macOS");
            return Result<(ICredentialStore, ProviderInfo), Error>.Success((store, info));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                var service = await SecretService.ConnectAsync(EncryptionType.Dh);
                var store = new LinuxCredentialStore(service);
                var info = new ProviderInfo("Linux Secret Service", "Linux");
                return Result<(ICredentialStore, ProviderInfo), Error>.Success((store, info));
            }
            catch (DBusErrorReplyException ex)
            {
                return Result<(ICredentialStore, ProviderInfo), Error>.Failure(
                    Error.ProviderUnavailable(
                        $"Could not connect to the D-Bus Secret Service: {ex.Message}"));
            }
        }

        return Result<(ICredentialStore, ProviderInfo), Error>.Failure(
            Error.UnsupportedPlatform(RuntimeInformation.OSDescription));
    }
}
