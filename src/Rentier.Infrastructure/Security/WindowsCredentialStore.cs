using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Meziantou.Framework.Win32;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure.Security;

/// <summary>
/// Windows Credential Manager implementation for secure credential storage.
/// </summary>
[SupportedOSPlatform("windows")]
[ExcludeFromCodeCoverage]
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const int ERROR_NOT_FOUND = 1168;

    public Task<Result<VoidResult, Error>> SaveCredentialAsync(
        string key, string secret, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(key))
            return Task.FromResult(
                Result<VoidResult, Error>.Failure(
                    Error.CredentialWriteFailed("Key must not be empty")));

        if (string.IsNullOrEmpty(secret))
            return Task.FromResult(
                Result<VoidResult, Error>.Failure(
                    Error.CredentialWriteFailed("Secret must not be empty")));

        return Task.Run<Result<VoidResult, Error>>(() =>
        {
            try
            {
                // CredentialManager targets windows5.1.2600 (XP+); .NET 10 only runs on far newer
                // Windows, so the version floor this class declares (unversioned "windows", matching
                // MacOsCredentialStore's "osx" convention) is always satisfied at runtime.
#pragma warning disable CA1416
                CredentialManager.WriteCredential(key, key, secret, CredentialPersistence.LocalMachine);
#pragma warning restore CA1416
                return Result<VoidResult, Error>.Success(VoidResult.Value);
            }
            catch (Win32Exception ex)
            {
                return Result<VoidResult, Error>.Failure(Error.CredentialWriteFailed(ex.Message));
            }
        }, ct);
    }

    public Task<Result<string, Error>> GetCredentialAsync(
        string key, CancellationToken ct = default)
    {
        return Task.Run<Result<string, Error>>(() =>
        {
            try
            {
#pragma warning disable CA1416
                var credential = CredentialManager.ReadCredential(key);
#pragma warning restore CA1416
                return credential?.Password is { } secret
                    ? Result<string, Error>.Success(secret)
                    : Result<string, Error>.Failure(Error.CredentialNotFound(key));
            }
            catch (Win32Exception ex)
            {
                return Result<string, Error>.Failure(Error.CredentialReadFailed(ex.Message));
            }
        }, ct);
    }

    public Task<Result<VoidResult, Error>> DeleteCredentialAsync(
        string key, CancellationToken ct = default)
    {
        return Task.Run<Result<VoidResult, Error>>(() =>
        {
            try
            {
#pragma warning disable CA1416
                CredentialManager.DeleteCredential(key);
#pragma warning restore CA1416
                return Result<VoidResult, Error>.Success(VoidResult.Value);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_NOT_FOUND)
            {
                return Result<VoidResult, Error>.Success(VoidResult.Value); // idempotent
            }
            catch (Win32Exception ex)
            {
                return Result<VoidResult, Error>.Failure(Error.CredentialDeleteFailed(ex.Message));
            }
        }, ct);
    }
}
