using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>The version of the toolkit MatUi: where the number comes from, where it is written, and the changelog that says what changed (docs/VERSIONING.md).</summary>
public sealed partial class MatUiVersionTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [GeneratedRegex(@"<MatUiVersion>(?<v>[^<]+)</MatUiVersion>")]
    private static partial Regex PropsVersion();

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private async Task<JsonElement> Json(string url) => JsonDocument.Parse(await _client.GetStringAsync(url)).RootElement;

    // ------------------------------------------------------------------------------------------------------ the number and the file

    [Fact]
    public void The_version_of_the_toolkit_is_three_numbers_and_comes_from_one_place()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", AppVersion.MatUi);
        var props = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "Directory.Build.props"));
        Assert.Equal(PropsVersion().Match(props).Groups["v"].Value, AppVersion.MatUi);
        Assert.Single(PropsVersion().Matches(props));
    }

    [Fact]
    public void The_changelog_is_in_its_format_and_the_build_carries_the_file_of_the_repository()
    {
        var log = Changelog.Load();
        Assert.True(log.Problems().Count == 0, string.Join("\n", log.Problems()));
        Assert.NotNull(log.Unreleased);
        Assert.Equal(File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "CHANGELOG.md")).Replace("\r\n", "\n"), Changelog.LoadText());
    }

    [Fact]
    public void The_newest_released_version_of_the_changelog_is_the_version_of_the_toolkit()
    {
        // a bump of MatUiVersion without the notes for it (or notes for a version that was not set) fails here: tools/bump.js does both
        var latest = Changelog.Load().Latest;
        Assert.NotNull(latest);
        Assert.Equal(AppVersion.MatUi, latest.Version);
    }

    [Fact]
    public void Every_version_in_the_changelog_says_what_changed_and_a_break_says_what_to_do()
    {
        foreach (var release in Changelog.Load().Releases)
        {
            Assert.True(release.ItemCount > 0, release.Version);
            Assert.True(!release.Breaking || release.Migration.Count > 0, $"{release.Version} breaks something and has no Migration");
        }
    }

    // ------------------------------------------------------------------------------------------------------ the parser

    private const string Sample = """
        # Changelog

        Some words before the first entry.

        ## [Unreleased]

        ### Added

        - A new thing.

        ## [1.2.0] - 2026-10-12

        A note for the version.

        ### Added

        - The one thing, which
          continues on a second line.
        - Another thing.

        ### Breaking

        - The class `mat-old` is gone.

        ### Migration

        - Replace `mat-old` by `mat-new`.

        ## [1.1.1] - 2026-09-30

        ### Fixed

        - A bug.

        ## [1.0.0]

        ### Added

        - The start.
        """;

    [Fact]
    public void The_parser_reads_versions_dates_notes_sections_and_bullets_that_go_on_in_the_next_line()
    {
        var log = Changelog.Parse(Sample, out var skipped);
        Assert.Equal(0, skipped);
        Assert.Equal(["Unreleased", "1.2.0", "1.1.1", "1.0.0"], log.Entries.Select(e => e.Version));
        Assert.True(log.Unreleased!.IsUnreleased);
        Assert.Null(log.Unreleased.Date);
        var entry = log.Entries[1];
        Assert.Equal("2026-10-12", entry.Date);
        Assert.Equal("A note for the version.", entry.Note);
        Assert.Equal(["The one thing, which continues on a second line.", "Another thing."], entry.Section("Added"));
        Assert.True(entry.Breaking);
        Assert.Equal(["Replace `mat-old` by `mat-new`."], entry.Migration);
        Assert.False(log.Entries[2].Breaking);
        Assert.Null(log.Entries[3].Date);
    }

    [Fact]
    public void The_releases_are_ordered_by_their_numbers_and_since_lists_what_an_app_takes_with_an_update()
    {
        var log = Changelog.Parse(Sample, out _);
        Assert.Equal("1.2.0", log.Latest!.Version);
        Assert.Equal(["1.2.0", "1.1.1"], log.Since("1.0.0").Select(e => e.Version));
        Assert.Equal(["1.2.0"], log.Since("1.1.1").Select(e => e.Version));
        Assert.Empty(log.Since("1.2.0"));
        Assert.Empty(log.Since("2.0.0"));
        Assert.Equal(["1.2.0", "1.1.1", "1.0.0"], log.Since("not a version").Select(e => e.Version));
        // 1.10.0 is higher than 1.9.0: numbers, not text
        Assert.True(Changelog.Compare("1.10.0", "1.9.0") > 0);
    }

    [Theory]
    [InlineData("## [1.0.0]\n\n### Added\n\n- x\n\n## [1.0.0]\n\n### Added\n\n- y", "twice")]
    [InlineData("## [1.0.0]\n\n### Added\n\n- x\n\n## [1.1.0]\n\n### Added\n\n- y", "newest version comes first")]
    [InlineData("## [1.0.0]\n\n### Added\n\n- x\n\n### Breaking\n\n- y", "no Migration")]
    [InlineData("## [1.0.0]\n\n### Invented\n\n- x", "not a section")]
    [InlineData("## [1.0.0]\n\n### Added\n\n", "says nothing")]
    [InlineData("## [1.0.0]\n\n### Added\n\n- x\n\n## [Unreleased]\n\n### Added\n\n- y", "first entry")]
    [InlineData("## [one.two]\n\n### Added\n\n- x", "three numbers")]
    public void The_problems_of_a_changelog_are_named(string markdown, string expected)
        => Assert.Contains(Changelog.Parse(markdown, out _).Problems(), p => p.Contains(expected, StringComparison.Ordinal));

    [Fact]
    public void A_changelog_without_problems_has_none()
        => Assert.Empty(Changelog.Parse(Sample, out _).Problems());

    // ------------------------------------------------------------------------------------------------------ what the site serves

    [Fact]
    public async Task The_script_and_the_boot_script_carry_the_version_of_the_toolkit_in_the_source_and_as_served()
    {
        var js = await _client.GetStringAsync("/ui/matui.js");
        Assert.Contains($"version: '{AppVersion.MatUi}'", js);
        Assert.Contains($"Version {AppVersion.MatUi} of the toolkit MatUi", js);
        var boot = await _client.GetStringAsync("/ui/matui.boot.js");
        Assert.StartsWith($"/* MatUi boot {AppVersion.MatUi}:", boot);

        // the source files say it themselves (an app that references the project gets them as they are): a bump of MatUiVersion that does not write it there fails here
        var source = TestPaths.ReadToolkit("js/matui.js");
        Assert.Contains($"version: '{AppVersion.MatUi}'", source);
        Assert.Contains($"Version {AppVersion.MatUi} of the toolkit MatUi", source);
        Assert.StartsWith($"/* MatUi boot {AppVersion.MatUi}:", TestPaths.ReadToolkit("js/matui.boot.js"));
        Assert.Single(Regex.Matches(source, @"version: '\d+\.\d+\.\d+'"));
    }

    [Fact]
    public async Task The_manifest_names_the_version_the_build_the_hash_and_every_file_with_its_sha256()
    {
        var manifest = await Json("/ui/manifest.json");
        Assert.Equal("MatUi", manifest.GetProperty("name").GetString());
        Assert.Equal(AppVersion.MatUi, manifest.GetProperty("version").GetString());
        Assert.Equal(AppVersion.Current, manifest.GetProperty("build").GetString());
        Assert.Equal("/ui/CHANGELOG.md", manifest.GetProperty("changelog").GetString());
        Assert.Equal((await Json("/api/version")).GetProperty("hash").GetString(), manifest.GetProperty("hash").GetString());
        var files = manifest.GetProperty("files").EnumerateArray().ToList();
        foreach (var name in new[] { "matui.css", "matui.js", "matui.boot.js", "matui-icons.svg" })
        {
            Assert.Contains(files, f => f.GetProperty("name").GetString() == name);
        }

        Assert.All(ToolkitFiles.CssParts, part => Assert.Contains(files, f => f.GetProperty("name").GetString() == $"css/{part}"));
        // what it says a file hashes to is what the file hashes to
        foreach (var file in files)
        {
            var text = await _client.GetStringAsync(file.GetProperty("url").GetString());
            Assert.Equal(file.GetProperty("sha256").GetString(), Sha(text));
            Assert.Equal(Encoding.UTF8.GetByteCount(text), file.GetProperty("bytes").GetInt64());
        }

        Assert.Equal("*", (await _client.GetAsync("/ui/manifest.json")).Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task The_changelog_is_served_as_the_file_and_as_data_from_a_version_on()
    {
        var response = await _client.GetAsync("/ui/CHANGELOG.md");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/markdown", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Changelog.LoadText(), await response.Content.ReadAsStringAsync());

        var all = await Json("/api/changelog");
        Assert.Equal(AppVersion.MatUi, all.GetProperty("version").GetString());
        Assert.Equal(Changelog.Load().Releases.Count, all.GetProperty("releases").GetArrayLength());
        Assert.False(all.GetProperty("upToDate").GetBoolean());
        Assert.Equal(JsonValueKind.Object, all.GetProperty("unreleased").ValueKind);

        var current = await Json($"/api/changelog?since={AppVersion.MatUi}");
        Assert.True(current.GetProperty("upToDate").GetBoolean());
        Assert.Equal(0, current.GetProperty("releases").GetArrayLength());
        Assert.False(current.GetProperty("breaking").GetBoolean());

        var bad = await _client.GetAsync("/api/changelog?since=old");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(AppVersion.MatUi, (await Json("/api/version")).GetProperty("matui").GetString());
    }

    [Fact]
    public async Task The_archive_an_app_copies_holds_the_changelog_a_manifest_with_the_hashes_of_its_files_and_how_to_update()
    {
        var response = await _client.GetAsync("/ui/matui.zip");
        Assert.Equal($"matui-{AppVersion.MatUi}.zip", response.Content.Headers.ContentDisposition!.FileName);
        using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        string Read(string name) { using var reader = new StreamReader(zip.GetEntry($"matui/{name}")!.Open()); return reader.ReadToEnd(); }

        Assert.Equal(Changelog.LoadText(), Read("CHANGELOG.md"));
        var manifest = JsonDocument.Parse(Read("matui.manifest.json")).RootElement;
        Assert.Equal(AppVersion.MatUi, manifest.GetProperty("version").GetString());
        var files = manifest.GetProperty("files").EnumerateArray().ToList();
        Assert.True(files.Count >= 14);
        foreach (var file in files)
        {
            var name = file.GetProperty("name").GetString()!;
            Assert.Equal(file.GetProperty("sha256").GetString(), Sha(Read(name)));
        }

        Assert.Contains(files, f => f.GetProperty("name").GetString() == "CHANGELOG.md");
        Assert.StartsWith($"MatUi {AppVersion.MatUi} (", Read("README.txt"));
        Assert.Contains("matui-check.js", Read("README.txt"));
        Assert.Contains("Update by copying ALL the files again", Read("README.txt"));
        // the CSS in the archive names the same version
        Assert.StartsWith($"/* MatUi {AppVersion.MatUi} (", Read("matui.css"));
        Assert.Contains($"version: '{AppVersion.MatUi}'", Read("matui.js"));
    }

    [Fact]
    public async Task The_site_names_the_version_of_the_toolkit_where_a_person_looks_for_it()
    {
        var download = await _client.GetStringAsync("/Download");
        Assert.Contains($"MatUi {AppVersion.MatUi}", download);
        Assert.Contains("/ui/manifest.json", download);
        Assert.Contains("/ui/CHANGELOG.md", download);
    }
}
