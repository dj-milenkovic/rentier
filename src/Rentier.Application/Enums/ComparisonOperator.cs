namespace Rentier.Application.Enums;

/// <summary>Comparison operator for date and numeric column filters.</summary>
public enum ComparisonOperator
{
    /// <summary>Exact match (=).</summary>
    Equals = 0,
    /// <summary>Greater than (&gt;).</summary>
    GreaterThan = 1,
    /// <summary>Less than (&lt;).</summary>
    LessThan = 2,
}
