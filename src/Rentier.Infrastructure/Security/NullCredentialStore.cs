using System.Diagnostics.CodeAnalysis;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure.Security;

/// <summary>
/// Credential store implementation that always returns a failure, used when
/// <see cref="CredentialStoreFactory"/> cannot initialise a real provider at startup.
/// This ensures the application starts cleanly and surfaces a clear error on first credential access.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class NullCredentialStore : ICredentialStore
{
    private readonly Error _providerError;

    public NullCredentialStore(Error providerError)
    {
        _providerError = providerError;
    }

    public Task<Result<VoidResult, Error>> SaveCredentialAsync(
        string key, string secret, CancellationToken ct = default) =>
        Task.FromResult(Result<VoidResult, Error>.Failure(_providerError));

    public Task<Result<string, Error>> GetCredentialAsync(
        string key, CancellationToken ct = default) =>
        Task.FromResult(Result<string, Error>.Failure(_providerError));

    public Task<Result<VoidResult, Error>> DeleteCredentialAsync(
        string key, CancellationToken ct = default) =>
        Task.FromResult(Result<VoidResult, Error>.Failure(_providerError));
}
