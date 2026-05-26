using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Rentier.Application.Common;
using Xunit;

namespace Rentier.UnitTests;

public class ErrorCodesTests
{
    private static readonly FieldInfo[] Fields =
        typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .ToArray();

    private static IEnumerable<string> AllValues() =>
        Fields.Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void AllCodes_AreUnique()
    {
        var values = AllValues().ToList();
        var duplicates = values
            .GroupBy(v => v)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        duplicates.Should().BeEmpty(
            because: $"duplicate error code values found: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void AllCodes_AreScreamingSnakeCase()
    {
        var invalid = AllValues()
            .Where(v => !Regex.IsMatch(v, @"^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$"))
            .ToList();

        invalid.Should().BeEmpty(
            because: $"error codes must match SCREAMING_SNAKE_CASE: {string.Join(", ", invalid)}");
    }

    [Fact]
    public void EntitySpecificCodes_FollowEntityActionReasonPattern()
    {
        // Entity-specific codes must have at least 2 segments (ENTITY_ACTION or ENTITY_ACTION_REASON)
        // Generic single-word codes are exempt.
        var singleSegmentExemptions = new HashSet<string>
        {
            "DOMAIN_ERROR", "NOT_FOUND", "INFRASTRUCTURE_ERROR",
            "CREDENTIAL_NOT_FOUND", "CREDENTIAL_WRITE_FAILED", "CREDENTIAL_READ_FAILED",
            "CREDENTIAL_DELETE_FAILED", "PROVIDER_UNAVAILABLE", "UNSUPPORTED_PLATFORM",
            "PAGINATION_VALIDATION_FAILED",
            "DATE_REQUIRED", "GROSS_REQUIRED", "TICKER_REQUIRED",
            "NET_EXCEEDS_GROSS", "NET_NEGATIVE", "NETWORK_FAILURE", "RATE_NOT_FOUND",
        };

        var entityPrefixes = new[] { "FILING_", "REPORT_", "DASHBOARD_", "IMPORTER_", "MAILBOX_", "HOLIDAY_" };

        var invalid = AllValues()
            .Where(v => entityPrefixes.Any(p => v.StartsWith(p, StringComparison.Ordinal)))
            .Where(v => !singleSegmentExemptions.Contains(v))
            .Where(v => v.Split('_').Length < 3)
            .ToList();

        invalid.Should().BeEmpty(
            because: $"entity-specific error codes must follow ENTITY_ACTION_REASON pattern: {string.Join(", ", invalid)}");
    }

    [Fact]
    public void ErrorCodes_HasExpectedCategoryGroups()
    {
        var values = AllValues().ToList();

        // Verify critical codes exist
        values.Should().Contain(ErrorCodes.DOMAIN_ERROR);
        values.Should().Contain(ErrorCodes.NOT_FOUND);
        values.Should().Contain(ErrorCodes.PAGINATION_VALIDATION_FAILED);
        values.Should().Contain(ErrorCodes.CREDENTIAL_NOT_FOUND);
        values.Should().Contain(ErrorCodes.FILING_BULK_DELETE_FAILED);
        values.Should().Contain(ErrorCodes.REPORT_QUERY_FAILED);
        values.Should().Contain(ErrorCodes.DASHBOARD_QUERY_FAILED);
    }
}
