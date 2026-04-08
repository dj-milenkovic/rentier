namespace Rentier.Application.Interfaces;

/// <summary>
/// Abstracts OS-managed secure credential storage.
/// IMAP passwords MUST be stored exclusively via this interface.
/// Key format: Rentier/&lt;entity-type&gt;/&lt;entity-id&gt;/&lt;field&gt;
/// </summary>
public interface ICredentialStore
{
    Task SaveCredentialAsync(string key, string secret, CancellationToken ct = default);
    Task<string?> GetCredentialAsync(string key, CancellationToken ct = default);
    Task DeleteCredentialAsync(string key, CancellationToken ct = default);
}
