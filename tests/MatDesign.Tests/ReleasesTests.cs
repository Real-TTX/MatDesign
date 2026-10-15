using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>The pipeline that makes a release of the design system, and the addresses an app takes a release from.</summary>
public sealed class ReleasesTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _plain;
    private readonly StudioHarness _studio;

    public ReleasesTests(WebApplicationFactory<Program> factory)
    {
        _plain = factory;
        _studio = new StudioHarness(factory);
    }

    public void Dispose() => _studio.Dispose();

    private async Task<HttpResponseMessage> Release(string version, string note = "")
        => await _studio.Post("/Studio/Releases/New", "/Studio/Releases/New", new Dictionary<string, string> { ["Version"] = version, ["Note"] = note });

    private static string RunId(HttpResponseMessage response)
        => response.Headers.Location!.OriginalString.Split("id=")[1];

    private async Task<PipelineRun> Released(string version, string note = "")
    {
        var response = await Release(version, note);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return _studio.Releases.Run(RunId(response))!;
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    [Fact]
    public async Task The_pages_lead_a_visitor_to_the_sign_in_and_the_addresses_of_the_releases_are_public()
    {
        foreach (var url in new[] { "/Studio/Releases", "/Studio/Releases/New", "/Studio/Releases/Run?id=r000001" })
        {
            Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync(url)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await _studio.Client.GetAsync("/releases/latest.json")).StatusCode);
        Assert.Equal("[]", (await _studio.Get("/api/releases")).Trim());
        Assert.Equal(HttpStatusCode.NotFound, (await _studio.Client.GetAsync("/api/releases/latest")).StatusCode);
    }

    [Fact]
    public async Task Without_the_studio_there_are_no_releases()
    {
        var client = _plain.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/releases/latest.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/releases/{Ver.First}/matui.css")).StatusCode);
        Assert.Equal("[]", (await client.GetStringAsync("/api/releases")).Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.a.0")]
    [InlineData("1.0.0-beta")]
    [InlineData("v1.0.0")]
    [InlineData("../1.0.0")]
    public void A_version_is_three_numbers(string version)
        => Assert.False(ReleaseService.IsVersion(version));

    [Theory]
    [InlineData("0.1.0")]
    [InlineData("1.10.200")]
    [InlineData("0.0.0")]
    public void Three_numbers_are_a_version(string version)
        => Assert.True(ReleaseService.IsVersion(version));

    [Fact]
    public async Task The_first_release_runs_every_stage_writes_the_files_with_their_hashes_and_serves_them()
    {
        await _studio.SignIn();
        Assert.Equal(Ver.First, _studio.Releases.SuggestNext());
        var form = await _studio.Get("/Studio/Releases/New");
        Assert.Contains($"value=\"{Ver.First}\"", form);
        Assert.Contains("All pass", form);
        Assert.Contains("data-confirm=\"Run the pipeline?", form);

        var response = await Release(Ver.First, "The first release.");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var run = _studio.Releases.Run(RunId(response))!;

        Assert.True(run.Succeeded);
        Assert.Equal(ReleaseService.StageNames, run.Stages.Select(s => s.Name));
        Assert.All(run.Stages, s => Assert.Equal(StageStatus.Succeeded, s.Status));
        Assert.Contains("Every check passes.", run.Stages[0].Log);
        Assert.Contains(run.Stages[1].Log, l => l.Contains("first release", StringComparison.Ordinal));
        Assert.Contains(run.Stages[3].Log, l => l.StartsWith($"Published as {Ver.First}", StringComparison.Ordinal));

        // the files are on the volume, and the manifest holds what they hash to
        var directory = Path.Combine(_studio.DataDirectory, "releases", Ver.First);
        var manifest = JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(Path.Combine(directory, "manifest.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(Ver.First, manifest.Version);
        Assert.Equal("The first release.", manifest.Note);
        Assert.Equal(0, manifest.Errors);
        Assert.Equal(_studio.Design.Apps.Count, manifest.Apps);
        Assert.Equal(run.Id, manifest.Run);
        foreach (var file in new[] { "matui.css", "matui.js", "matui.boot.js", "matui-icons.svg", $"matui-{Ver.First}.zip", "design.json", "styleguide.html", "styleguide.css", "sample.svg", "css/matui.tokens.css", "css/matui.apps.css", "iconlib/matui-iconlib.svg", "iconlib/iconlib.json", "iconlib/LICENSE.txt" })
        {
            var entry = manifest.Files.Single(f => f.Path == file);
            Assert.Equal(entry.Sha256, Hash(Path.Combine(directory, file)));
            Assert.Equal(new FileInfo(Path.Combine(directory, file)).Length, entry.Bytes);
        }

        // served: the bundle names the release, a release never goes out of date, any site may take it
        var css = await _studio.Client.GetAsync($"/releases/{Ver.First}/matui.css");
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.StartsWith($"/* MatUi {Ver.First} (", await css.Content.ReadAsStringAsync());
        Assert.Contains("immutable", css.Headers.CacheControl!.ToString());
        Assert.Equal("*", css.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("text/css", css.Content.Headers.ContentType!.MediaType);
        Assert.Contains("MatMail", await _studio.Get($"/releases/{Ver.First}/design.json"));
        // the script of a release says the version of the release, and the release carries the changelog and names the version of the toolkit it holds
        Assert.Contains($"version: '{Ver.First}'", await _studio.Get($"/releases/{Ver.First}/matui.js"));
        Assert.Equal(Changelog.LoadText(), await _studio.Get($"/releases/{Ver.First}/CHANGELOG.md"));
        Assert.Equal(AppVersion.MatUi, manifest.Toolkit);
        using var zip = new ZipArchive(new MemoryStream(await _studio.Client.GetByteArrayAsync($"/releases/{Ver.First}/matui-{Ver.First}.zip")));
        using (var readme = new StreamReader(zip.GetEntry("matui/README.txt")!.Open()))
        {
            Assert.StartsWith($"MatUi {Ver.First} (", await readme.ReadToEndAsync());
        }

        // and found: latest.json, the API, the list, the overview
        var latest = await _studio.Client.GetAsync("/releases/latest.json");
        Assert.Equal("no-cache", latest.Headers.CacheControl!.ToString());
        Assert.Equal(Ver.First, JsonDocument.Parse(await latest.Content.ReadAsStringAsync()).RootElement.GetProperty("version").GetString());
        Assert.Equal(Ver.First, JsonDocument.Parse(await _studio.Get("/api/releases/latest")).RootElement.GetProperty("version").GetString());
        var list = JsonDocument.Parse(await _studio.Get("/api/releases")).RootElement;
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal($"/releases/{Ver.First}/", list[0].GetProperty("url").GetString());
        var page = await _studio.Get("/Studio/Releases");
        Assert.Contains(Ver.First, page);
        Assert.Contains("mat-pill--ok mat-pill--plain\">Latest", page);
        Assert.Contains($"/Studio/Releases/Run?id={run.Id}", page);
        Assert.Contains(Ver.First, await _studio.Get("/Studio"));
        Assert.Contains("Release", await _studio.Get("/Studio/Activity"));
    }

    [Fact]
    public async Task The_run_page_shows_every_stage_with_its_log()
    {
        await _studio.SignIn();
        var run = await Released(Ver.First);

        var html = await _studio.Get($"/Studio/Releases/Run?id={run.Id}");

        Assert.Contains($"Version {Ver.First} is released.", html);
        foreach (var stage in ReleaseService.StageNames)
        {
            Assert.Contains($"id=\"stage-{stage}\"", html);
        }

        Assert.Contains("matui.css", html);
        await _studio.Get("/Studio/Releases/Run?id=r999999", HttpStatusCode.NotFound);
        await _studio.Get("/Studio/Releases/Run?id=../x", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_next_release_suggests_the_next_patch_and_its_changelog_is_what_the_studio_changed_since()
    {
        await _studio.SignIn();
        await Released(Ver.First);
        var form = StudioHarness.AppForm();
        await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", form);
        Assert.Equal(Ver.Next(1), _studio.Releases.SuggestNext());
        Assert.Contains("1 change", await _studio.Get("/Studio/Releases/New"));
        var first = JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(Path.Combine(_studio.DataDirectory, "releases", Ver.First, "manifest.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var run = await Released(Ver.Next(2), "With an app.");

        Assert.True(run.Succeeded);
        var second = _studio.Releases.Get(Ver.Next(2))!;
        Assert.Single(second.Changelog);
        Assert.Contains("App matx: created", second.Changelog[0]);
        Assert.Equal(_studio.Design.Apps.Count, second.Apps);
        Assert.Equal([Ver.Next(2), Ver.First], _studio.Releases.Releases().Select(m => m.Version));
        Assert.Equal(Ver.Next(2), _studio.Releases.Latest!.Version);
        // the apps part of the new release has the colours of the new app; the first release is as it was
        Assert.Contains("[data-app=\"matx\"]", await _studio.Get($"/releases/{Ver.Next(2)}/css/matui.apps.css"));
        Assert.DoesNotContain("[data-app=\"matx\"]", await _studio.Get($"/releases/{Ver.First}/css/matui.apps.css"));
        Assert.Equal(first.Files.Single(f => f.Path == "matui.js").Sha256, Hash(Path.Combine(_studio.DataDirectory, "releases", Ver.First, "matui.js")));
        Assert.Equal(first.Files.Select(f => (f.Path, f.Sha256)), _studio.Releases.Get(Ver.First)!.Files.Select(f => (f.Path, f.Sha256)));
        Assert.Equal(first.Hash, _studio.Releases.Get(Ver.First)!.Hash);
    }

    [Fact]
    public async Task A_run_that_fails_a_check_is_kept_publishes_nothing_and_skips_the_rest()
    {
        await _studio.SignIn();
        _studio.Store.Save("apps", "qa", new MatApp("qa", "QA port", "An app for the checks", AppRegistry.Productivity, "#0f766e", "apps", "qa", Port: 4455), "test");
        _studio.Design.Reload();
        Assert.Contains("2 errors", await _studio.Get("/Studio/Releases/New"));

        var response = await Release(Ver.First);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var run = _studio.Releases.Run(RunId(response))!;

        Assert.False(run.Succeeded);
        Assert.Equal([StageStatus.Failed, StageStatus.Skipped, StageStatus.Skipped, StageStatus.Skipped, StageStatus.Skipped], run.Stages.Select(s => s.Status));
        Assert.Contains(run.Stages[0].Log, l => l.Contains("already uses port 4455", StringComparison.Ordinal));
        Assert.Contains("2 error(s): a release needs every check to pass.", run.Stages[0].Log);
        Assert.Empty(_studio.Releases.Releases());
        Assert.False(Directory.Exists(Path.Combine(_studio.DataDirectory, "releases", Ver.First)));
        var releases = Path.Combine(_studio.DataDirectory, "releases");
        Assert.True(!Directory.Exists(releases) || Directory.GetDirectories(releases).All(d => !Path.GetFileName(d).StartsWith(".building", StringComparison.Ordinal)), "nothing is left half built");

        var page = await _studio.Get($"/Studio/Releases/Run?id={run.Id}");
        Assert.Contains("The run stopped at Check.", page);
        Assert.Contains("Nothing was published.", page);
        var list = await _studio.Get("/Studio/Releases");
        Assert.Contains("Stopped at Check", list);
        Assert.Contains("Nothing has been released yet", list);
    }

    [Fact]
    public async Task A_version_that_cannot_be_released_is_refused_before_a_run_is_made()
    {
        await _studio.SignIn();
        await Released(Ver.Next(5));
        var runs = _studio.Releases.Runs().Count;

        // the first and the last are formats, the others are the rules: released already, below the last release, below the toolkit (the toolkit is the floor of a release)
        foreach (var (version, message) in new[] { ("1.0", "three numbers"), (Ver.Next(5), "released already"), (Ver.Next(3), "the next version is higher"), (Ver.Below, "not lower than that"), ("1.0.0.1", "three numbers") })
        {
            var response = await Release(version);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(message, html);
            Assert.Contains($"value=\"{version}\"", html);
        }

        Assert.Contains("Keep the note to 500 characters.", await (await Release(Ver.Next(6), new string('x', 501))).Content.ReadAsStringAsync());
        Assert.Equal(runs, _studio.Releases.Runs().Count);
        Assert.Single(_studio.Releases.Releases());
    }

    [Fact]
    public async Task Only_one_run_at_a_time()
    {
        await _studio.SignIn();
        var gate = typeof(ReleaseService).GetField("_running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_studio.Releases)!;
        using var held = new ManualResetEventSlim();
        using var done = new ManualResetEventSlim();
        var holder = new Thread(() =>
        {
            lock (gate)
            {
                held.Set();
                done.Wait();
            }
        });
        holder.Start();
        held.Wait();
        try
        {
            Assert.Throws<InvalidOperationException>(() => _studio.Releases.Execute(Ver.First, string.Empty, "admin"));
            var busy = await Release(Ver.First);
            Assert.Equal(HttpStatusCode.OK, busy.StatusCode);
            Assert.Contains("A release is being made.", await busy.Content.ReadAsStringAsync());
        }
        finally
        {
            done.Set();
            holder.Join();
        }

        Assert.Empty(_studio.Releases.Releases());
    }

    [Theory]
    [InlineData("/releases/{0}/..%2f..%2fstudio%2freleases%2f{0}%2f000001.json")]
    [InlineData("/releases/{0}/%2e%2e/manifest.json")]
    [InlineData("/releases/{0}/css/..%5c..%5cmanifest.json")]
    [InlineData("/releases/{0}/nothing.css")]
    [InlineData("/releases/{0}/css")]
    [InlineData("/releases/{1}/matui.css")]
    [InlineData("/releases/latest/matui.css")]
    [InlineData("/releases/0.1/matui.css")]
    public async Task Only_a_file_of_a_release_is_served(string template)
    {
        await _studio.SignIn();
        await Released(Ver.First);

        // {0} is the release, {1} a version that was never released; a path that tries to leave the release is refused by the server itself (400) or finds nothing (404): never a file
        var url = string.Format(CultureInfo.InvariantCulture, template, Ver.First, Ver.Next(1));
        Assert.Contains((await _studio.Client.GetAsync(url)).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest });
    }

    [Fact]
    public async Task A_release_cannot_be_changed_or_deleted_through_the_site()
    {
        await _studio.SignIn();
        await Released(Ver.First);

        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch })
        {
            var response = await _studio.Client.SendAsync(new HttpRequestMessage(method, $"/releases/{Ver.First}/matui.css"));
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.StartsWith($"/* MatUi {Ver.First}", await _studio.Get($"/releases/{Ver.First}/matui.css"));
    }

    [Fact]
    public async Task A_release_whose_files_are_gone_is_not_offered()
    {
        await _studio.SignIn();
        await Released(Ver.First);
        Directory.Delete(Path.Combine(_studio.DataDirectory, "releases", Ver.First), recursive: true);

        Assert.Empty(_studio.Releases.Releases());
        Assert.Equal(HttpStatusCode.NotFound, (await _studio.Client.GetAsync("/releases/latest.json")).StatusCode);
        Assert.Equal(Ver.First, _studio.Releases.SuggestNext());
    }
}
