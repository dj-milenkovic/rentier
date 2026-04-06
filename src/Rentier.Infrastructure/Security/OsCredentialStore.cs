using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure.Security;

/// <summary>
/// OS-managed secure credential storage.
/// Full implementation deferred to IMAP mailbox feature.
/// </summary>
public sealed class OsCredentialStore : ICredentialStore
{
    public Task SaveCredentialAsync(string key, string secret, CancellationToken ct = default)
        => throw new NotImplementedException("Full OS credential store implementation deferred to IMAP mailbox feature");

    public Task<string?> GetCredentialAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("Full OS credential store implementation deferred to IMAP mailbox feature");

    public Task DeleteCredentialAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("Full OS credential store implementation deferred to IMAP mailbox feature");
}
