using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary><c>tools/matui-check.js</c> on the archive of a white label release: it reads the product and the file names from the manifest (needs node, like the other tool tests).</summary>
public sealed class WhiteLabelToolTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
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
                throw new TimeoutException("node did not finish");
            }

            return (process.ExitCode, output.Result + error.Result);
        }
        catch (Win32Exception) when (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
        {
            return null;
        }
    }

    private static string Tool => Path.Combine(TestPaths.RepoRoot, "tools", "matui-check.js");

    private static string TempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "matdesign-wl-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    private string AppWithCopy(WhiteLabelSetting setting)
    {
        var result = factory.Services.GetRequiredService<ReleaseService>().TrialBuild(setting);
        var app = TempFolder();
        using var zip = new ZipArchive(new MemoryStream(result.Files.Single(f => f.Path.EndsWith(".zip", StringComparison.Ordinal)).Bytes));
        var folder = setting.FileStem + "/";
        foreach (var entry in zip.Entries.Where(e => e.Name.Length > 0))
        {
            var target = Path.Combine(app, "wwwroot", entry.FullName[folder.Length..]);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target);
        }

        return app;
    }

    [Fact]
    public void A_copy_of_a_white_label_archive_is_found_named_by_its_product_and_a_change_by_hand_is_found()
    {
        var app = AppWithCopy(new WhiteLabelSetting("acme", "AcmeUi"));
        if (Node(Tool, app) is not { } fresh)
        {
            return;
        }

        Assert.Equal(0, fresh.Code);
        Assert.Contains("AcmeUi in ", fresh.Output);
        Assert.Contains($"version {AppVersion.MatUi} of AcmeUi", fresh.Output);
        Assert.Contains("acmeui.css", fresh.Output);
        Assert.Contains("acmeui.boot.js", fresh.Output);
        Assert.Contains("up to date.", fresh.Output);

        File.AppendAllText(Path.Combine(app, "wwwroot", "acmeui.css"), "/* my change */");
        var changed = Node(Tool, app)!.Value;
        Assert.Equal(2, changed.Code);
        Assert.Contains("changed since it was copied: acmeui.css", changed.Output);
    }

    [Fact]
    public void The_data_of_the_check_names_the_product_and_files_of_other_names_are_not_taken_for_a_copy()
    {
        var app = AppWithCopy(new WhiteLabelSetting("zed", "Zed"));
        if (Node(Tool, app, "--json") is not { } result)
        {
            return;
        }

        var copy = System.Text.Json.JsonDocument.Parse(result.Output).RootElement.GetProperty("copies")[0];
        Assert.Equal("Zed", copy.GetProperty("product").GetString());
        Assert.Equal("zed", copy.GetProperty("stem").GetString());
        Assert.Equal(AppVersion.MatUi, copy.GetProperty("version").GetString());

        // a folder that has a manifest of another name than its product is not a copy of anything the tool knows, and a plain folder still says so
        var other = TempFolder();
        File.WriteAllText(Path.Combine(other, "other.manifest.json"), "{\"name\":\"Different\",\"files\":[]}");
        File.WriteAllText(Path.Combine(other, "other.css"), "/* x */");
        var none = Node(Tool, other)!.Value;
        Assert.Equal(64, none.Code);
        Assert.Contains("no copy of MatUi found", none.Output);
    }

    [Fact]
    public void A_copy_under_the_toolkits_own_names_is_still_checked_as_before()
    {
        var app = TempFolder();
        File.WriteAllText(Path.Combine(app, "matui.css"), "/* MatUi 0.0.1 (abc123) - bundled */\n:root{}\n");
        if (Node(Tool, app) is not { } old)
        {
            return;
        }

        Assert.Contains("version 0.0.1", old.Output);
        Assert.DoesNotContain(" of MatUi", old.Output);
    }
}
