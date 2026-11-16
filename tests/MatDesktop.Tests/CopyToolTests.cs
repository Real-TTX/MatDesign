using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace MatDesktop.Tests;

/// <summary>tools/matdesktop-copy.js (CODE-03): the copy of a program carries the version on every file and a manifest, an older copy is cleaned up, and a WinForms tray takes the tokens only.</summary>
public sealed class CopyToolTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "matdesktop-copy-" + Guid.NewGuid().ToString("N")[..8]);

    public CopyToolTests()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "Example.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Avalonia\" Version=\"11.3.22\" /></ItemGroup></Project>");
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>Runs the tool with node; null when node is not on this machine (CI sets it up before the tests).</summary>
    private static (int Code, string Output)? Run(params string[] args)
    {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Paths.Root };
        start.ArgumentList.Add(Path.Combine(Paths.Root, "tools", "matdesktop-copy.js"));
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void A_copy_marks_every_file_writes_the_manifest_and_removes_what_an_older_copy_left()
    {
        var copy = Path.Combine(_folder, "MatDesktop");
        Directory.CreateDirectory(Path.Combine(copy, "Controls"));
        File.WriteAllText(Path.Combine(copy, "Controls", "MatOld.cs"), $"// MatDesktop 0.0.1 – a copy from MatDesign, do not edit here\nnamespace MatDesktop;\n");
        File.WriteAllText(Path.Combine(copy, "Mine.cs"), "namespace Example;\n");

        if (Run(_folder) is not { } result)
        {
            return; // no node on this machine
        }

        Assert.Equal(0, result.Code);
        Assert.Contains("Avalonia.Themes.Fluent", result.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(copy, "Controls", "MatOld.cs")));
        Assert.True(File.Exists(Path.Combine(copy, "Mine.cs")));

        var manifest = File.ReadAllLines(Path.Combine(copy, "matdesktop.version"));
        Assert.StartsWith($"MatDesktop {MatDesktopInfo.Version} – a copy from MatDesign", manifest[0], StringComparison.Ordinal);
        var listed = manifest.Skip(1).Select(l => l.Split("  ", 2)).ToDictionary(p => p[1], p => p[0]);
        Assert.Contains("Tokens/MatTokens.g.cs", listed.Keys);
        Assert.Contains("Themes/Controls.axaml", listed.Keys);
        Assert.Contains("Setup/MatSetupWindow.cs", listed.Keys);
        foreach (var (file, hash) in listed)
        {
            var text = File.ReadAllText(Path.Combine(copy, file));
            Assert.Equal(hash, Sha(text));
            Assert.StartsWith(file.EndsWith(".axaml", StringComparison.Ordinal) ? $"<!-- MatDesktop {MatDesktopInfo.Version} – a copy" : $"// MatDesktop {MatDesktopInfo.Version} – a copy", text, StringComparison.Ordinal);
            Assert.Equal(File.ReadAllText(Path.Combine(Paths.Package, file)).Replace("\r\n", "\n", StringComparison.Ordinal), text[(text.IndexOf('\n', StringComparison.Ordinal) + 1)..]);
        }
    }

    [Fact]
    public void A_winforms_tray_takes_the_tokens_and_the_colour_maths_only()
    {
        if (Run(_folder, "--tokens-only") is not { } result)
        {
            return;
        }

        Assert.Equal(0, result.Code);
        var copy = Path.Combine(_folder, "MatDesktop");
        var files = Directory.GetFiles(copy, "*.cs", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(copy, f).Replace('\\', '/')).Order(StringComparer.Ordinal);
        Assert.Equal(SourceTests.AvaloniaFree.Take(3).Order(StringComparer.Ordinal), files);
        Assert.Contains("--tokens-only", File.ReadAllLines(Path.Combine(copy, "matdesktop.version"))[0], StringComparison.Ordinal);
        Assert.DoesNotContain("needs <PackageReference", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_tool_refuses_a_folder_without_a_project_and_reads_the_version_from_the_project()
    {
        var empty = Path.Combine(_folder, "empty");
        Directory.CreateDirectory(empty);
        if (Run(empty) is { } result)
        {
            Assert.Equal(66, result.Code);
        }

        var tool = File.ReadAllText(Path.Combine(Paths.Root, "tools", "matdesktop-copy.js"));
        Assert.Contains("MatDesktopVersion", tool, StringComparison.Ordinal);
        Assert.Contains("a copy from MatDesign, do not edit here", tool, StringComparison.Ordinal);
    }
}
