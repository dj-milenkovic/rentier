using Rentier.Application.ReleaseNotes;
using Rentier.ReleaseNotes;

// Usage:
//   check --title <pr title> --body-file <file>
//   plan  --since <version> --version <new version> --tags-file <file>
//   build --since <version> --version <new version> --tags-file <file> --pr-bodies <dir> --out <file>
//         [--allow-no-user-changes true]
// <since>:     the last release made before PR descriptions carried release notes; older versions are left out.
// <tags-file>: the repository's version tags, one per line.
// <pr-bodies>: <dir>/<version>/<pr number>.md, the descriptions of the PRs merged for each version
//              that "plan" printed.
return Cli.Run(args, Console.Out);

internal static class Cli
{
    public static int Run(string[] args, TextWriter output)
    {
        if (args.Length == 0)
            return Usage(output);

        var options = ParseOptions(args.Skip(1));
        try
        {
            return args[0] switch
            {
                "check" => Check(Require(options, "title"), File.ReadAllText(Require(options, "body-file")), output),
                "plan" => Plan(options, output),
                "build" => Build(options, output),
                _ => Usage(output),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            output.WriteLine($"::error::{ex.Message}");
            return 2;
        }
    }

    /// <summary>
    /// Fails (exit 1) when a note is missing one of its languages, or when a user-facing PR
    /// (<see cref="PrTitle.IsUserFacing"/>) has no note at all.
    /// </summary>
    internal static int Check(string title, string body, TextWriter output)
    {
        var note = PrReleaseNote.Parse(body);

        foreach (var problem in note.Problems)
            output.WriteLine($"::error title=Release note::{problem}");
        if (note.Problems.Count > 0)
            return 1;

        if (note.IsEmpty)
        {
            if (!PrTitle.IsUserFacing(title))
            {
                output.WriteLine("Internal change: no release note needed, and this PR will not appear in the app's What's new.");
                return 0;
            }

            var scopes = string.Join(", ", PrTitle.InternalScopes.Order(StringComparer.Ordinal));
            output.WriteLine("::error title=Release note::This feat/fix/perf PR has no release note. Write the changes under "
                + "\"### EN\" and \"### SR\" in the \"## Release note\" section, as users will notice them. If users will not notice the change, give the title "
                + $"an internal scope instead, e.g. \"fix(ci): ...\" (internal scopes: {scopes}).");
            return 1;
        }

        output.WriteLine("Release note found (EN + SR). In the app it will read:");
        output.WriteLine(note.English);
        output.WriteLine(note.Serbian);
        return 0;
    }

    private static int Plan(Dictionary<string, string> options, TextWriter output)
    {
        foreach (var version in Load(options))
            output.WriteLine($"v{version}");
        return 0;
    }

    /// <summary>
    /// Writes the document. Fails (exit 1) when the version being released has no user-facing
    /// change, unless <c>--allow-no-user-changes true</c> (the release workflow's override): an
    /// update that changes nothing users notice should be a deliberate choice.
    /// </summary>
    private static int Build(Dictionary<string, string> options, TextWriter output)
    {
        var versions = Load(options);
        var prDirectory = Require(options, "pr-bodies");
        var allowNoUserChanges = options.TryGetValue("allow-no-user-changes", out var allow)
            && bool.TryParse(allow, out var allowed) && allowed;

        var exitCode = BuildDocument(versions, version =>
        {
            var dir = Path.Combine(prDirectory, $"v{version}");
            return Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.md")
                    .OrderBy(f => int.TryParse(Path.GetFileNameWithoutExtension(f), out var n) ? n : int.MaxValue)
                    .Select(File.ReadAllText)
                : [];
        }, allowNoUserChanges, output, out var document);

        if (exitCode == 0)
            File.WriteAllText(Require(options, "out"), document);
        return exitCode;
    }

    /// <summary>The decision part of <c>build</c>, without file access.</summary>
    internal static int BuildDocument(
        IReadOnlyList<Version> versions,
        Func<Version, IEnumerable<string>> prBodies,
        bool allowNoUserChanges,
        TextWriter output,
        out string document)
    {
        var result = ReleaseNotesBuilder.Build(versions, prBodies);
        document = string.Empty;

        var newest = versions[0];
        if (result.WithoutUserFacingChanges.Contains(newest) && !allowNoUserChanges)
        {
            output.WriteLine($"::error title=No user-facing changes::No PR merged for v{newest} has a release note, so users would get an update "
                + "that changes nothing they notice. The release stops here. To release anyway (e.g. a dependency security fix), "
                + "run it again with \"Release without user-facing changes\" ticked; the app then shows a generic line.");
            return 1;
        }

        document = ReleaseNotesDocument.Format(result.Entries);
        output.WriteLine($"Release notes for {result.Entries.Count} of {versions.Count} version(s): {string.Join(", ", result.Entries.Select(e => e.DisplayVersion))}");
        if (result.WithoutUserFacingChanges.Count > 0)
            output.WriteLine($"Generic line for: {string.Join(", ", result.WithoutUserFacingChanges.Select(v => $"v{v}"))}");
        return 0;
    }

    private static IReadOnlyList<Version> Load(Dictionary<string, string> options)
    {
        var tags = File.ReadAllLines(Require(options, "tags-file")).Where(l => l.Trim().Length > 0);
        return ReleaseNotesBuilder.VersionsToCover(Require(options, "version"), tags, Require(options, "since"));
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        string? key = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
                key = arg[2..];
            else if (key is not null)
            {
                options[key] = arg;
                key = null;
            }
        }

        return options;
    }

    private static string Require(Dictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && value.Length > 0
            ? value
            : throw new ArgumentException($"Missing --{name}.");

    private static int Usage(TextWriter output)
    {
        output.WriteLine("Usage: check --title <t> --body-file <f> | plan|build --since <v> --version <v> --tags-file <f> [--pr-bodies <dir> --out <f>]");
        return 2;
    }
}
