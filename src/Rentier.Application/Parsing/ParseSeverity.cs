namespace Rentier.Application.Parsing;

/// <summary>How serious a <see cref="ParseError"/> is for the report it belongs to.</summary>
public enum ParseSeverity
{
    /// <summary>Income data was lost; the report counts this as a failed event.</summary>
    Error,

    /// <summary>Nothing taxable was lost; logged only.</summary>
    Warning,
}
