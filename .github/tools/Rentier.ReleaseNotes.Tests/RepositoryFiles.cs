namespace Rentier.ReleaseNotes.Tests;

/// <summary>
/// Locates files in the repository checkout, for contract tests that read the workflows and the
/// PR template rather than compiled types.
/// </summary>
internal static class RepositoryFiles
{
    public static string Root { get; } = FindRoot();

    /// <summary>
    /// Reads a file with line endings normalized to <c>\n</c>: Windows checkouts get CRLF
    /// (<c>.gitattributes</c> text=auto), and contract-test patterns must match on every OS.
    /// </summary>
    public static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath)).ReplaceLineEndings("\n");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Rentier.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Rentier.slnx not found above the test output folder.");
    }
}
