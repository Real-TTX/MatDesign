using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using MatDesign.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>
/// <c>tools/bump.js</c> (makes the next version) and <c>tools/matui-check.js</c> (checks an app's copy), run as they are run by a person: with node, on a copy of the
/// repository's files and on the archive the site serves. They need node (the browser checks need it too); without it these tests do nothing.
/// </summary>
public sealed class VersionToolsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string Tool(string name) => Path.Combine(TestPaths.RepoRoot, "tools", name);

    /// <summary>Runs node; null when there is no node on this machine.</summary>
    private static (int Code, string Output)? Node(params string[] args)
    {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60_000))
            {
                process.Kill(true);
                throw new TimeoutException("node did not finish: " + string.Join(' ', args));
            }

            return (process.ExitCode, output.Result + error.Result);
        }
        catch (Win32Exception) when (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
        {
            // no node on this machine: these tests have nothing to run (the build of the repository has node, so there they always run)
            return null;
        }
    }

    private static string TempFolder(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "matdesign-" + name + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    // ------------------------------------------------------------------------------------------------------ matui-check.js

    /// <summary>The archive of the toolkit as an app copies it, unpacked into an app folder.</summary>
    private async Task<string> AppWithCopy()
    {
        var app = TempFolder("app");
        using var zip = new ZipArchive(new MemoryStream(await _client.GetByteArrayAsync("/ui/matui.zip")));
        foreach (var entry in zip.Entries.Where(e => e.Name.Length > 0))
        {
            var target = Path.Combine(app, "wwwroot", entry.FullName["matui/".Length..]);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target);
        }

        return app;
    }

    private static string ChangelogWith(string versions)
    {
        var path = Path.Combine(TempFolder("log"), "CHANGELOG.md");
        File.WriteAllText(path, versions);
        return path;
    }

    [Fact]
    public async Task A_fresh_copy_of_the_newest_version_is_current_and_a_hand_made_change_is_found()
    {
        var app = await AppWithCopy();
        if (Node(Tool("matui-check.js"), app) is not { } fresh)
        {
            return;
        }

        Assert.Equal(0, fresh.Code);
        Assert.Contains($"version {AppVersion.MatUi}", fresh.Output);
        Assert.Contains("up to date.", fresh.Output);

        // the line ends of git on Windows are not a change
        var css = Path.Combine(app, "wwwroot", "matui.css");
        File.WriteAllText(css, File.ReadAllText(css).Replace("\n", "\r\n"));
        Assert.Equal(0, Node(Tool("matui-check.js"), app)!.Value.Code);

        File.AppendAllText(css, "/* my change */");
        var changed = Node(Tool("matui-check.js"), app)!.Value;
        Assert.Equal(2, changed.Code);
        Assert.Contains("changed since it was copied: matui.css", changed.Output);
    }

    [Fact]
    public async Task An_older_copy_is_told_what_an_update_takes_with_it_and_a_break_comes_with_its_steps()
    {
        var app = await AppWithCopy();
        var log = ChangelogWith($"""
            # Changelog

            ## [Unreleased]

            ## [{Ver.NextMajor}] - 2026-12-01

            ### Breaking

            - The class `mat-old` is gone.

            ### Migration

            - Replace `mat-old` by `mat-new`.

            ## [{AppVersion.MatUi}] - 2026-10-12

            ### Added

            - The start.
            """);
        if (Node(Tool("matui-check.js"), app, "--changelog", log) is null)
        {
            return;
        }

        // the copy is the one of the build; the changelog knows a newer version, so it is behind
        var behind = Node(Tool("matui-check.js"), app, "--changelog", log)!.Value;
        Assert.Equal(2, behind.Code);
        Assert.Contains(Ver.NextMajor, behind.Output);
        Assert.Contains("BREAKING", behind.Output);
        Assert.Contains("breaks: The class `mat-old` is gone.", behind.Output);
        Assert.Contains("do:     Replace `mat-old` by `mat-new`.", behind.Output);
        Assert.Contains($"1 newer version: copy ALL the files of {Ver.NextMajor} again and do the steps above.", behind.Output);

        // a newer version that breaks nothing is advice (1)
        var soft = ChangelogWith($"## [{Ver.Next(1)}] - 2026-12-01\n\n### Fixed\n\n- A bug.\n\n## [{AppVersion.MatUi}]\n\n### Added\n\n- The start.\n");
        var advice = Node(Tool("matui-check.js"), app, "--changelog", soft)!.Value;
        Assert.Equal(1, advice.Code);
        Assert.DoesNotContain("BREAKING", advice.Output);
    }

    [Fact]
    public async Task Files_of_different_versions_a_copy_without_a_version_and_a_folder_without_a_copy_are_named()
    {
        var app = await AppWithCopy();
        var script = Path.Combine(app, "wwwroot", "matui.js");
        File.WriteAllText(script, File.ReadAllText(script).Replace($"Version {AppVersion.MatUi} of the toolkit MatUi", $"Version {Ver.Next(7)} of the toolkit MatUi").Replace($"version: '{AppVersion.MatUi}'", $"version: '{Ver.Next(7)}'"));
        if (Node(Tool("matui-check.js"), app) is not { } mixed)
        {
            return;
        }

        Assert.Equal(2, mixed.Code);
        Assert.Contains("mixed versions", mixed.Output);

        // from before the toolkit had a version: the first line of the stylesheet names a build, not a version
        var old = TempFolder("old");
        File.WriteAllText(Path.Combine(old, "matui.css"), "/* MatUi 0.1.42-20261010 (abc123) - bundled from matui.tokens.css */\n:root{}\n");
        var before = Node(Tool("matui-check.js"), old)!.Value;
        Assert.Equal(1, before.Code);
        Assert.Contains("from before the toolkit had a version", before.Output);

        var none = Node(Tool("matui-check.js"), TempFolder("empty"))!.Value;
        Assert.Equal(64, none.Code);
        Assert.Contains("no copy of MatUi found", none.Output);
    }

    [Fact]
    public async Task The_check_can_report_as_data()
    {
        var app = await AppWithCopy();
        if (Node(Tool("matui-check.js"), app, "--json") is not { } result)
        {
            return;
        }

        var json = System.Text.Json.JsonDocument.Parse(result.Output).RootElement;
        Assert.Equal("current", json.GetProperty("status").GetString());
        Assert.Equal(AppVersion.MatUi, json.GetProperty("copies")[0].GetProperty("version").GetString());
    }

    // ------------------------------------------------------------------------------------------------------ bump.js

    private static string Repository(string unreleased)
    {
        var root = TempFolder("repo");
        File.Copy(Path.Combine(TestPaths.RepoRoot, "Directory.Build.props"), Path.Combine(root, "Directory.Build.props"));
        var js = Path.Combine(root, "src", "MatUi", "wwwroot", "js");
        Directory.CreateDirectory(js);
        File.Copy(TestPaths.Toolkit("js/matui.js"), Path.Combine(js, "matui.js"));
        File.Copy(TestPaths.Toolkit("js/matui.boot.js"), Path.Combine(js, "matui.boot.js"));
        File.WriteAllText(Path.Combine(root, "CHANGELOG.md"), $"# Changelog\n\n## [Unreleased]\n\n{unreleased}\n## [{AppVersion.MatUi}]\n\n### Added\n\n- The start.\n");
        return root;
    }

    [Fact]
    public void A_bump_makes_unreleased_the_new_version_and_writes_the_number_where_it_is_kept()
    {
        var root = Repository("### Added\n\n- A new thing.\n\n### Fixed\n\n- A bug.\n");
        if (Node(Tool("bump.js"), "minor", "--date", "2026-11-01", "--root", root) is not { } bump)
        {
            return;
        }

        Assert.Equal(0, bump.Code);
        var current = AppVersion.MatUi.Split('.').Select(int.Parse).ToArray();
        var next = $"{current[0]}.{current[1] + 1}.0";
        Assert.Contains($"MatUi {AppVersion.MatUi} -> {next}", bump.Output);

        var log = Changelog.Parse(File.ReadAllText(Path.Combine(root, "CHANGELOG.md")), out _);
        Assert.Empty(log.Problems());
        Assert.Equal(next, log.Latest!.Version);
        Assert.Equal("2026-11-01", log.Latest.Date);
        Assert.Equal(["A new thing."], log.Latest.Section("Added"));
        Assert.Equal(0, log.Unreleased!.ItemCount);
        Assert.Contains($"<MatUiVersion>{next}</MatUiVersion>", File.ReadAllText(Path.Combine(root, "Directory.Build.props")));
        var script = File.ReadAllText(Path.Combine(root, "src", "MatUi", "wwwroot", "js", "matui.js"));
        Assert.Contains($"version: '{next}'", script);
        Assert.Contains($"Version {next} of the toolkit MatUi", script);
        Assert.StartsWith($"/* MatUi boot {next}:", File.ReadAllText(Path.Combine(root, "src", "MatUi", "wwwroot", "js", "matui.boot.js")));

        // the next bump has nothing to release
        var again = Node(Tool("bump.js"), "patch", "--root", root)!.Value;
        Assert.Equal(1, again.Code);
        Assert.Contains("Unreleased says nothing", again.Output);
    }

    [Fact]
    public void A_bump_refuses_what_the_numbers_would_not_promise_and_writes_nothing_when_it_is_dry()
    {
        var breaking = Repository("### Breaking\n\n- A class is gone.\n");
        if (Node(Tool("bump.js"), "minor", "--root", breaking) is not { } withoutSteps)
        {
            return;
        }

        Assert.Equal(1, withoutSteps.Code);
        Assert.Contains("no Migration", withoutSteps.Output);

        var patchOfBreak = Repository("### Breaking\n\n- A class is gone.\n\n### Migration\n\n- Use the other.\n");
        var patch = Node(Tool("bump.js"), "patch", "--root", patchOfBreak)!.Value;
        Assert.Equal(1, patch.Code);
        // what a break asks for depends on the toolkit: before 1.0 a new minor, from 1.0 on a new major
        Assert.Contains(AppVersion.MatUi.StartsWith("0.", StringComparison.Ordinal) ? "at least a new minor" : "that is a major version", patch.Output);

        var dry = Repository("### Fixed\n\n- A bug.\n");
        var before = File.ReadAllText(Path.Combine(dry, "CHANGELOG.md"));
        var run = Node(Tool("bump.js"), "patch", "--dry", "--root", dry)!.Value;
        Assert.Equal(0, run.Code);
        Assert.Contains("nothing was written", run.Output);
        Assert.Equal(before, File.ReadAllText(Path.Combine(dry, "CHANGELOG.md")));

        Assert.Equal(1, Node(Tool("bump.js"), "0.0.1", "--root", dry)!.Value.Code);
        Assert.Equal(1, Node(Tool("bump.js"), "huge", "--root", dry)!.Value.Code);
    }
}
