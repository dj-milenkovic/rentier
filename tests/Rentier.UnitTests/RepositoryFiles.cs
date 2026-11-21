namespace Rentier.UnitTests;

/// <summary>
/// Locates files in the repository checkout, for contract tests that read source or build files
/// (workflows, AXAML, package manifests) rather than compiled types.
/// </summary>
internal static class RepositoryFiles
{
    public static string Root { get; } = FindRoot();

    public static string Read(string relativePath) => File.ReadAllText(Path(relativePath));

    public static string Path(string relativePath) => System.IO.Path.Combine(Root, relativePath);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, "Rentier.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Rentier.slnx not found above the test output folder.");
    }
}
