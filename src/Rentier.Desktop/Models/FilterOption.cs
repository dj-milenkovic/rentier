namespace Rentier.Desktop.Models;

/// <summary>A labeled dropdown option that may carry a nullable typed value.</summary>
public sealed record FilterOption<T>(string Label, T Value);
