using System.Text.RegularExpressions;

namespace MatPlatform.Tests;

/// <summary>Where the files of the repository are.</summary>
internal static class Paths
{
    public static string Root { get; } = FindRoot();

    public static string Package => Path.Combine(Root, "src", "MatPlatform");

    public static string Sprite => Path.Combine(Root, "src", "MatUi", "wwwroot", "img", "matui-icons.svg");

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

/// <summary>The package as the apps copy it (CODE-03): one version, files that compile anywhere, nothing of an app in them.</summary>
public sealed partial class PackageTests
{
    [Fact]
    public void The_version_of_the_project_and_of_the_code_are_the_same()
    {
        var project = File.ReadAllText(Path.Combine(Paths.Package, "MatPlatform.csproj"));
        var version = VersionRegex().Match(project).Groups[1].Value;
        Assert.Equal(MatPlatformInfo.Version, version);
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }

    [Fact]
    public void Every_file_is_in_the_namespace_and_knows_no_app()
    {
        var files = Directory.GetFiles(Paths.Package, "*.cs");
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.Matches("(?m)^namespace MatPlatform;\r?$", text);
            Assert.DoesNotMatch(@"MatSplit|MatIdentity\.Web|Mattiverse\.", text);
            // The copy tool writes its own first line; a file of the source never carries one.
            Assert.DoesNotContain("a copy from MatDesign", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_copy_tool_reads_the_version_from_the_project()
    {
        var tool = File.ReadAllText(Path.Combine(Paths.Root, "tools", "matplatform-copy.js"));
        Assert.Contains("MatPlatformVersion", tool, StringComparison.Ordinal);
        Assert.Contains("a copy from MatDesign, do not edit here", tool, StringComparison.Ordinal);
    }

    [GeneratedRegex("<MatPlatformVersion>([^<]+)</MatPlatformVersion>")]
    private static partial Regex VersionRegex();
}
