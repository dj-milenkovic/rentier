namespace Rentier.Application.Common;

public sealed record Error(string Code, string Message)
{
    public static Error Domain(string message) => new("DOMAIN_ERROR", message);
    public static Error NotFound(string message) => new("NOT_FOUND", message);
    public static Error Infrastructure(string message) => new("INFRASTRUCTURE_ERROR", message);
}
