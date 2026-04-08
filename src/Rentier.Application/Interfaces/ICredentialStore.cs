using Rentier.Application.Common;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Abstracts OS-managed secure credential storage.
/// IMAP passwords MUST be stored exclusively via this interface.
/// Key format: Rentier/&lt;entity-type&gt;/&lt;entity-id&gt;/&lt;field&gt;
/// </summary>
public interface ICredentialStore
{
    /// <summary>Saves (or overwrites) a credential in the OS credential store.</summary>
    Task<Result<VoidResult, Error>> SaveCredentialAsync(
        string key, string secret, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a credential from the OS credential store.
    /// Returns <see cref="Error.CredentialNotFound"/> when the key is absent.
    /// </summary>
    Task<Result<string, Error>> GetCredentialAsync(
        string key, CancellationToken ct = default);

    /// <summary>
    /// Deletes a credential from the OS credential store.
    /// Idempotent — succeeds even if the key does not exist.
    /// </summary>
    Task<Result<VoidResult, Error>> DeleteCredentialAsync(
        string key, CancellationToken ct = default);
}
