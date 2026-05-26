namespace Rentier.Application.Common;

/// <summary>
/// Single source of truth for credential key format.
/// Key format: Rentier/&lt;entity-type&gt;/&lt;entity-id&gt;/&lt;field&gt;
/// </summary>
public static class CredentialKeys
{
    /// <summary>Returns the credential store key for a mailbox IMAP password.</summary>
    public static string MailboxPassword(Guid mailboxId) =>
        $"Rentier/Mailbox/{mailboxId}/password";
}
