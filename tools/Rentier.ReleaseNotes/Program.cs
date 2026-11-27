using System.Text.RegularExpressions;
using Rentier.Application.ReleaseNotes;
using Rentier.ReleaseNotes;

// Usage:
//   check --title <pr title> --body-file <file>
//   plan  --history <history.md> --version <new version> --tags-file <file>
//   build --history <history.md> --version <new version> --tags-file <file> --pr-bodies <dir> --out <file>
// <tags-file>: the repository's version tags, one per line.
// <pr-bodies>: <dir>/<version>/<pr number>.md, the descriptions of the PRs merged for each version
//              that "plan" printed.
return Cli.Run(args, Console.Out);

internal static partial class Cli
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
    /// Fails (exit 1) when a note is missing one of its languages; warns when a feat/fix PR has
    /// no note at all, since users would not hear about it.
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
            if (UserFacingType().IsMatch(title))
            {
                var what = note.HasSection ? "an empty release note" : "no \"## Release note\" section";
                output.WriteLine($"::warning title=Release note::This feat/fix PR has {what}, so users will not see it in the app's What's new. Fill in the EN: and SR: lines if users notice the change.");
            }
            else
                output.WriteLine("No release note: this PR will not appear in the app's What's new.");
            return 0;
        }

        output.WriteLine("Release note found (EN + SR). In the app it will read:");
        output.WriteLine(note.English);
        output.WriteLine(note.Serbian);
        return 0;
    }

    private static int Plan(Dictionary<string, string> options, TextWriter output)
    {
        var (history, versions) = Load(options);
        foreach (var version in ReleaseNotesBuilder.VersionsNeedingPrNotes(history, versions))
            output.WriteLine($"v{version}");
        return 0;
    }

    private static int Build(Dictionary<string, string> options, TextWriter output)
    {
        var (history, versions) = Load(options);
        var prDirectory = Require(options, "pr-bodies");

        var entries = ReleaseNotesBuilder.Build(history, versions, version =>
        {
            var dir = Path.Combine(prDirectory, $"v{version}");
            return Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.md")
                    .OrderBy(f => int.TryParse(Path.GetFileNameWithoutExtension(f), out var n) ? n : int.MaxValue)
                    .Select(File.ReadAllText)
                : [];
        });

        File.WriteAllText(Require(options, "out"), ReleaseNotesDocument.Format(entries));
        output.WriteLine($"Release notes for {entries.Count} of {versions.Count} version(s): {string.Join(", ", entries.Select(e => e.DisplayVersion))}");
        return 0;
    }

    private static (IReadOnlyList<ReleaseNotesEntry> History, IReadOnlyList<Version> Versions) Load(Dictionary<string, string> options)
    {
        var history = ReleaseNotesDocument.Parse(File.ReadAllText(Require(options, "history")));
        var tags = File.ReadAllLines(Require(options, "tags-file")).Where(l => l.Trim().Length > 0);
        return (history, ReleaseNotesBuilder.VersionsToCover(Require(options, "version"), tags));
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
        output.WriteLine("Usage: check --title <t> --body-file <f> | plan|build --history <f> --version <v> --tags-file <f> [--pr-bodies <dir> --out <f>]");
        return 2;
    }

    [GeneratedRegex(@"^(feat|fix|perf)(\([^)]*\))?!?:", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UserFacingType();
}
