using System.Runtime.Versioning;
using System.Text;
using DBus.Services.Secrets;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Tmds.DBus.Protocol;

namespace Rentier.Infrastructure.Security;

/// <summary>
/// Linux Secret Service implementation via the D-Bus Secret Service protocol.
/// Compatible with GNOME Keyring and KDE Wallet.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxCredentialStore : ICredentialStore
{
    private readonly SecretService _service;

    public LinuxCredentialStore(SecretService service)
    {
        _service = service;
    }

    public async Task<Result<VoidResult, Error>> SaveCredentialAsync(
        string key, string secret, CancellationToken ct = default)
    {
        try
        {
            var collection = await _service.GetDefaultCollectionAsync();
            if (collection is null)
                return Result<VoidResult, Error>.Failure(
                    Error.ProviderUnavailable("The default Secret Service collection is not available."));

            var attributes = new Dictionary<string, string>
            {
                { "application", "Rentier" },
                { "key", key }
            };

            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var item = await collection.CreateItemAsync(
                "Rentier Credential",
                attributes,
                secretBytes,
                "text/plain; charset=utf8",
                replace: true);

            return item is not null
                ? Result<VoidResult, Error>.Success(VoidResult.Value)
                : Result<VoidResult, Error>.Failure(
                    Error.CredentialWriteFailed($"Failed to create credential for key '{key}' (prompt dismissed)."));
        }
        catch (DBusErrorReplyException ex)
        {
            return Result<VoidResult, Error>.Failure(
                Error.CredentialWriteFailed($"D-Bus error saving credential: {ex.Message}"));
        }
    }

    public async Task<Result<string, Error>> GetCredentialAsync(
        string key, CancellationToken ct = default)
    {
        try
        {
            var collection = await _service.GetDefaultCollectionAsync();
            if (collection is null)
                return Result<string, Error>.Failure(
                    Error.ProviderUnavailable("The default Secret Service collection is not available."));

            var attributes = new Dictionary<string, string>
            {
                { "application", "Rentier" },
                { "key", key }
            };

            var items = await collection.SearchItemsAsync(attributes);
            if (items.Length == 0)
                return Result<string, Error>.Failure(Error.CredentialNotFound(key));

            var secretBytes = await items[0].GetSecretAsync();
            return Result<string, Error>.Success(Encoding.UTF8.GetString(secretBytes));
        }
        catch (DBusErrorReplyException ex)
        {
            return Result<string, Error>.Failure(
                Error.ProviderUnavailable($"D-Bus error retrieving credential: {ex.Message}"));
        }
    }

    public async Task<Result<VoidResult, Error>> DeleteCredentialAsync(
        string key, CancellationToken ct = default)
    {
        try
        {
            var collection = await _service.GetDefaultCollectionAsync();
            if (collection is null)
                return Result<VoidResult, Error>.Success(VoidResult.Value); // idempotent — nothing to delete

            var attributes = new Dictionary<string, string>
            {
                { "application", "Rentier" },
                { "key", key }
            };

            var items = await collection.SearchItemsAsync(attributes);
            if (items.Length == 0)
                return Result<VoidResult, Error>.Success(VoidResult.Value); // idempotent

            await items[0].DeleteAsync();
            return Result<VoidResult, Error>.Success(VoidResult.Value);
        }
        catch (DBusErrorReplyException ex)
        {
            return Result<VoidResult, Error>.Failure(
                Error.CredentialDeleteFailed($"D-Bus error deleting credential: {ex.Message}"));
        }
    }
}
