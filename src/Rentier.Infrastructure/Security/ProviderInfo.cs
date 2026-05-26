namespace Rentier.Infrastructure.Security;

/// <summary>
/// Identifies the active credential store provider selected at startup.
/// </summary>
public sealed record ProviderInfo(string ProviderName, string Platform)
{
    public override string ToString() => $"{ProviderName} ({Platform})";
}
