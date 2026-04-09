namespace Rentier.Application.Common;

public sealed record Error(string Code, string Message)
{
    public static Error Domain(string message) => new("DOMAIN_ERROR", message);
    public static Error NotFound(string message) => new("NOT_FOUND", message);
    public static Error Infrastructure(string message) => new("INFRASTRUCTURE_ERROR", message);

    /// <summary>Credential key was not found in the OS credential store.</summary>
    public static Error CredentialNotFound(string key) =>
        new("CREDENTIAL_NOT_FOUND", $"Credential '{key}' was not found in the OS credential store.");

    /// <summary>A write operation to the OS credential store failed.</summary>
    public static Error CredentialWriteFailed(string message) =>
        new("CREDENTIAL_WRITE_FAILED", message);

    /// <summary>A read operation from the OS credential store failed.</summary>
    public static Error CredentialReadFailed(string message) =>
        new("CREDENTIAL_READ_FAILED", message);

    /// <summary>A delete operation on the OS credential store failed.</summary>
    public static Error CredentialDeleteFailed(string message) =>
        new("CREDENTIAL_DELETE_FAILED", message);

    /// <summary>The credential store provider is unavailable (e.g. daemon not running).</summary>
    public static Error ProviderUnavailable(string reason) =>
        new("PROVIDER_UNAVAILABLE", reason);

    /// <summary>The current OS platform has no supported credential store provider.</summary>
    public static Error UnsupportedPlatform(string os) =>
        new("UNSUPPORTED_PLATFORM", $"No credential store provider is available for platform '{os}'.");
}
