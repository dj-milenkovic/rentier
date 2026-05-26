using System.Collections.Concurrent;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;

namespace Rentier.Tests.Common.Fakes;

/// <summary>
/// In-memory <see cref="ICredentialStore"/> for use in unit tests.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class FakeCredentialStore : ICredentialStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    /// <summary>Returns the set of keys currently in the fake store.</summary>
    public IEnumerable<string> StoredKeys => _store.Keys;

    /// <summary>Removes all stored credentials.</summary>
    public void Clear() => _store.Clear();

    public Task<Result<VoidResult, Error>> SaveCredentialAsync(
        string key, string secret, CancellationToken ct = default)
    {
        _store[key] = secret;
        return Task.FromResult(Result<VoidResult, Error>.Success(VoidResult.Value));
    }

    public Task<Result<string, Error>> GetCredentialAsync(
        string key, CancellationToken ct = default)
    {
        if (_store.TryGetValue(key, out var secret))
            return Task.FromResult(Result<string, Error>.Success(secret));

        return Task.FromResult(
            Result<string, Error>.Failure(Error.CredentialNotFound(key)));
    }

    public Task<Result<VoidResult, Error>> DeleteCredentialAsync(
        string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.FromResult(Result<VoidResult, Error>.Success(VoidResult.Value));
    }
}
