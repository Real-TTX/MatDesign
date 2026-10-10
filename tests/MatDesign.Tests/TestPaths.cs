namespace MatDesign.Tests;

/// <summary>Where the files of the repository are: the tests read the toolkit's CSS and script, the pages and the audit data from the source tree.</summary>
internal static class TestPaths
{
    public static string RepoRoot { get; } = FindRoot();

    public static string Toolkit(string relative) => Path.Combine(RepoRoot, "src", "MatUi", "wwwroot", relative);

    public static string ReadToolkit(string relative) => File.ReadAllText(Toolkit(relative));

    public static string Web(string relative) => Path.Combine(RepoRoot, "src", "MatDesign.Web", relative);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MatDesign.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("The repository root (MatDesign.slnx) was not found above " + AppContext.BaseDirectory);
    }
}
