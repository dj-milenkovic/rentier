using System.Text.RegularExpressions;

namespace Rentier.ReleaseNotes;

/// <summary>
/// Decides from a Conventional Commits PR title whether users will notice the change, and so
/// whether the PR needs a release note.
/// </summary>
/// <remarks>
/// User-facing: <c>feat</c>, <c>fix</c> and <c>perf</c>, unless every scope is internal, e.g.
/// <c>fix(ci): …</c> or <c>feat(deps): …</c>. Every other type (<c>chore</c>, <c>ci</c>,
/// <c>refactor</c>, <c>test</c>, <c>docs</c>, …) is internal.
/// </remarks>
public static partial class PrTitle
{
    /// <summary>Scopes that mark a feat/fix/perf as invisible to users.</summary>
    public static readonly IReadOnlySet<string> InternalScopes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "build", "ci", "claude", "deps", "dev", "docs", "release", "test", "tests",
        };

    private static readonly HashSet<string> UserFacingTypes = new(StringComparer.Ordinal) { "feat", "fix", "perf" };

    /// <summary>Whether a PR with this title must carry a release note.</summary>
    public static bool IsUserFacing(string? title)
    {
        var match = Header().Match(title ?? string.Empty);
        if (!match.Success || !UserFacingTypes.Contains(match.Groups["type"].Value))
            return false;

        var scopes = match.Groups["scope"].Value
            .Split([',', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return scopes.Length == 0 || !scopes.All(InternalScopes.Contains);
    }

    [GeneratedRegex(@"^(?<type>[a-z]+)(\((?<scope>[^)]*)\))?!?:", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Header();
}
