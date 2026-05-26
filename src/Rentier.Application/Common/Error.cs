namespace Rentier.Application.Common;

public sealed record Error(string Code, string Message)
{
    public static Error Domain(string message) => new(ErrorCodes.DOMAIN_ERROR, message);
    public static Error NotFound(string message) => new(ErrorCodes.NOT_FOUND, message);
    public static Error Infrastructure(string message) => new(ErrorCodes.INFRASTRUCTURE_ERROR, message);

    /// <summary>Credential key was not found in the OS credential store.</summary>
    public static Error CredentialNotFound(string key) =>
        new(ErrorCodes.CREDENTIAL_NOT_FOUND, $"Credential '{key}' was not found in the OS credential store.");

    /// <summary>A write operation to the OS credential store failed.</summary>
    public static Error CredentialWriteFailed(string message) =>
        new(ErrorCodes.CREDENTIAL_WRITE_FAILED, message);

    /// <summary>A read operation from the OS credential store failed.</summary>
    public static Error CredentialReadFailed(string message) =>
        new(ErrorCodes.CREDENTIAL_READ_FAILED, message);

    /// <summary>A delete operation on the OS credential store failed.</summary>
    public static Error CredentialDeleteFailed(string message) =>
        new(ErrorCodes.CREDENTIAL_DELETE_FAILED, message);

    /// <summary>The credential store provider is unavailable (e.g. daemon not running).</summary>
    public static Error ProviderUnavailable(string reason) =>
        new(ErrorCodes.PROVIDER_UNAVAILABLE, reason);

    /// <summary>The current OS platform has no supported credential store provider.</summary>
    public static Error UnsupportedPlatform(string os) =>
        new(ErrorCodes.UNSUPPORTED_PLATFORM, $"No credential store provider is available for platform '{os}'.");
}
