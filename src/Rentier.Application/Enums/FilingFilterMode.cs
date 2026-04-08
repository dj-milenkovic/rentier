namespace Rentier.Application.Enums;

/// <summary>Filter mode for paged filing queries.</summary>
public enum FilingFilterMode
{
    /// <summary>Show only Init and Filed filings (unpaid).</summary>
    Unpaid = 0,

    /// <summary>Show all filings regardless of status.</summary>
    All = 1
}
