using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>The white label setting in the Studio (CODE-09): its page, history and checks, and the release pipeline with and without it. A release without a setting is what it always was, byte for byte.</summary>
public sealed class WhiteLabelStudioTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string Page = "/Studio/Releases/WhiteLabel";

    private readonly StudioHarness _studio;

    public WhiteLabelStudioTests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    private ToolkitFiles Toolkit => _studio.Factory.Services.GetRequiredService<ToolkitFiles>();

    private IconLibrary Icons => _studio.Factory.Services.GetRequiredService<IconLibrary>();

    private static Dictionary<string, string> Form(string prefix, string name) => new() { ["Input.Prefix"] = prefix, ["Input.Name"] = name };

    private Task<HttpResponseMessage> Save(string prefix, string name, string query = "") => _studio.Post(Page, Page + query, Form(prefix, name));

    private async Task SaveOk(string prefix, string name)
        => Assert.Equal(HttpStatusCode.Redirect, (await Save(prefix, name)).StatusCode);

    private string Dir(string version) => Path.Combine(_studio.DataDirectory, "releases", version);

    private async Task<PipelineRun> Released(string version)
    {
        var response = await _studio.Post("/Studio/Releases/New", "/Studio/Releases/New", new Dictionary<string, string> { ["Version"] = version, ["Note"] = string.Empty });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return _studio.Releases.Run(response.Headers.Location!.OriginalString.Split("id=")[1])!;
    }

    private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

    // ------------------------------------------------------------------------------------------------------ the page

    [Fact]
    public async Task A_visitor_is_led_to_the_sign_in_and_so_is_a_call_of_the_renamer()
    {
        Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync(Page)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync("/Studio/History?kind=whitelabel&id=whitelabel")).StatusCode);
        var rename = await _studio.Client.PostAsync(WhiteLabelEndpoints.RenameUrl + "?prefix=acme&name=AcmeUi", new StringContent(".mat-btn"));
        Assert.Equal(HttpStatusCode.Redirect, rename.StatusCode);
    }

    [Fact]
    public async Task The_page_has_one_title_the_fields_the_examples_and_a_place_in_the_menu_and_the_overview()
    {
        await _studio.SignIn();
        var html = await _studio.Get(Page);

        Assert.Single(Regex.Matches(html, "<h1"));
        Assert.Contains("<title>White label", html);
        Assert.Contains("name=\"Input.Prefix\"", html);
        Assert.Contains("name=\"Input.Name\"", html);
        Assert.Contains("value=\"mat\"", html);
        Assert.Contains("value=\"MatUi\"", html);
        Assert.Contains("data-wl-examples", html);
        Assert.Contains("<code>mat-btn mat-btn--primary</code>", html);
        Assert.Contains("/js/whitelabel.js", html);
        Assert.Contains("Check a trial build", html);
        Assert.Contains("Not set", html);
        Assert.DoesNotContain("data-confirm-ok=\"Reset\"", html);
        Assert.Contains("<a class=\"mat-nav__item is-active\" href=\"/Studio/Releases/WhiteLabel\" aria-current=\"page\">", html);
        Assert.Contains("href=\"/Studio/Releases/WhiteLabel\"", await _studio.Get("/Studio"));
        Assert.Contains("href=\"/Studio/Releases/WhiteLabel\"", await _studio.Get("/Studio/Releases"));
        Assert.DoesNotMatch(new Regex(@"\sstyle\s*=", RegexOptions.IgnoreCase), html);
        Assert.Contains("White label", await _studio.Get("/Studio/Checks"));
    }

    [Fact]
    public async Task A_valid_setting_is_saved_with_a_history_and_the_page_shows_it_in_force()
    {
        await _studio.SignIn();
        var response = await Save("acme", "AcmeUi");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = _studio.Store.Get<WhiteLabelSetting>("whitelabel", "whitelabel")!;
        Assert.Equal(new WhiteLabelSetting("acme", "AcmeUi"), saved);
        Assert.Equal(new WhiteLabelSetting("acme", "AcmeUi"), WhiteLabelStore.Active(_studio.Factory.Services.GetRequiredService<StudioService>()));
        var html = await _studio.Get(Page);
        Assert.Contains("value=\"acme\"", html);
        Assert.Contains("value=\"AcmeUi\"", html);
        Assert.Contains("In force", html);
        Assert.Contains("<code>acme-btn acme-btn--primary</code>", html);
        Assert.Contains("data-confirm-ok=\"Reset\"", html);
        Assert.Contains("kind=whitelabel&amp;id=whitelabel", html);
        Assert.Contains("White label", await _studio.Get("/Studio/Activity"));
    }

    [Theory]
    [InlineData("data", "AcmeUi", "Prefix", "taken")]
    [InlineData("ACME!", "AcmeUi", "Prefix", "lower-case letters")]
    [InlineData("a", "AcmeUi", "Prefix", "2 to 8")]
    [InlineData("xmlx", "AcmeUi", "Prefix", "xml")]
    [InlineData("acme", "acmeui", "Name", "capital")]
    [InlineData("acme", "MatUi", "Name", "name of the toolkit itself")]
    [InlineData("acme", "", "Name", "3 to 24")]
    [InlineData("", "AcmeUi", "Prefix", "2 to 8")]
    public async Task A_setting_that_is_not_valid_is_refused_with_the_reason_and_nothing_is_saved(string prefix, string name, string field, string reason)
    {
        await _studio.SignIn();
        var response = await Save(prefix, name);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Please check the setting.", html);
        Assert.Contains(reason, WebUtility.HtmlDecode(html));
        Assert.Contains($"href=\"#Input_{field}\"", html);
        Assert.Null(_studio.Store.Current("whitelabel", "whitelabel"));
    }

    [Fact]
    public async Task The_toolkits_own_prefix_and_name_are_saved_as_no_rename_and_a_release_ignores_them()
    {
        await _studio.SignIn();
        await SaveOk("mat", "MatUi");

        Assert.Contains("No rename", await _studio.Get(Page));
        Assert.Null(WhiteLabelStore.Active(_studio.Factory.Services.GetRequiredService<StudioService>()));
        Assert.True(_studio.Checks.Run().Results.Single(r => r.Check.Id == "whitelabel").Passed);
    }

    [Fact]
    public async Task Check_runs_a_trial_build_shows_the_checks_and_the_counts_and_saves_nothing()
    {
        await _studio.SignIn();
        var response = await Save("acme", "AcmeUi", "?handler=Check");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The trial build passes.", html);
        Assert.Contains("Nothing was saved yet.", html);
        Assert.Contains("data-wl-checks", html);
        foreach (var check in WhiteLabelChecks.All)
        {
            Assert.Contains($"<code>{check.Id}</code>", html);
        }

        Assert.Equal(WhiteLabelChecks.All.Count, Regex.Matches(html, "mat-pill mat-pill--ok\">Passes").Count);
        Assert.Contains("data-wl-counts", html);
        Assert.Contains("<code>acmeui.css</code>", html);
        Assert.Contains("<code>design.json</code>", html);
        Assert.Contains("All files", html);
        Assert.Null(_studio.Store.Current("whitelabel", "whitelabel"));

        // an invalid setting is not tried
        var invalid = await (await Save("data", "AcmeUi", "?handler=Check")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("data-wl-checks", invalid);
        Assert.Contains("taken", invalid);
        var none = await (await Save("mat", "MatUi", "?handler=Check")).Content.ReadAsStringAsync();
        Assert.Contains("renames nothing", none);
    }

    [Fact]
    public async Task Reset_takes_the_setting_away_and_history_restores_an_earlier_one()
    {
        await _studio.SignIn();
        await SaveOk("acme", "AcmeUi");
        await SaveOk("zed", "Zed");
        Assert.Equal(new WhiteLabelSetting("zed", "Zed"), _studio.Store.Get<WhiteLabelSetting>("whitelabel", "whitelabel"));

        var history = await _studio.Get("/Studio/History?kind=whitelabel&id=whitelabel");
        Assert.Contains("Created: acme- / AcmeUi", WebUtility.HtmlDecode(history));
        Assert.Contains("zed- / Zed", WebUtility.HtmlDecode(history));
        Assert.Contains("Restore", history);

        var restore = await _studio.Post("/Studio/History?kind=whitelabel&id=whitelabel", "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = "whitelabel", ["id"] = "whitelabel", ["revision"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, restore.StatusCode);
        Assert.Equal(new WhiteLabelSetting("acme", "AcmeUi"), _studio.Store.Get<WhiteLabelSetting>("whitelabel", "whitelabel"));

        var reset = await _studio.Post(Page, Page + "?handler=Reset", new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Null(_studio.Store.Get<WhiteLabelSetting>("whitelabel", "whitelabel"));
        Assert.Null(WhiteLabelStore.Saved(_studio.Factory.Services.GetRequiredService<StudioService>()));
        Assert.Contains("Not set", await _studio.Get(Page));
    }

    [Fact]
    public async Task A_revision_that_is_not_valid_any_more_cannot_be_restored()
    {
        await _studio.SignIn();
        _studio.Store.Save("whitelabel", "whitelabel", new WhiteLabelSetting("data", "AcmeUi"), "admin", "By hand");
        await SaveOk("acme", "AcmeUi");

        var history = _studio.Factory.Services.GetRequiredService<StudioHistory>();
        var old = _studio.Store.History("whitelabel", "whitelabel").Single(r => r.Number == 1);
        Assert.Contains(history.CanRestore("whitelabel", "whitelabel", old), p => p.Message.Contains("taken", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------------------------------------ the renamer as an address

    [Fact]
    public async Task The_renamer_answers_the_text_renamed_and_refuses_a_setting_that_is_not_valid()
    {
        await _studio.SignIn();
        var ok = await _studio.Client.PostAsync(WhiteLabelEndpoints.RenameUrl + "?prefix=acme&name=AcmeUi", new StringContent("<i class=\"mat-btn\">MatUi matui.css format-x https://x/mat-y</i>"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("<i class=\"acme-btn\">AcmeUi acmeui.css format-x https://x/mat-y</i>", await ok.Content.ReadAsStringAsync());

        var refused = await _studio.Client.PostAsync(WhiteLabelEndpoints.RenameUrl + "?prefix=data&name=AcmeUi", new StringContent("x"));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("taken", await refused.Content.ReadAsStringAsync());
    }

    // ------------------------------------------------------------------------------------------------------ releases

    [Fact]
    public async Task A_release_without_a_setting_is_what_it_was_before_byte_for_byte()
    {
        await _studio.SignIn();
        var run = await Released("0.1.0");
        Assert.True(run.Succeeded);
        Assert.DoesNotContain(run.Stages[2].Log, l => l.Contains("White label", StringComparison.Ordinal));

        var dir = Dir("0.1.0");
        void Same(string path, string expected) => Assert.True(File.ReadAllBytes(Path.Combine(dir, path)).AsSpan().SequenceEqual(Bytes(expected)), path + " is not what the toolkit gives");
        Same("matui.css", Toolkit.ReleaseBundle("0.1.0"));
        Same("matui.js", Toolkit.ReleaseScript("0.1.0"));
        Same("matui.boot.js", Toolkit.ReleaseBootScript("0.1.0"));
        Same("matui-icons.svg", Toolkit.Sprite);
        Same("CHANGELOG.md", Changelog.LoadText());
        foreach (var part in ToolkitFiles.CssParts)
        {
            Same("css/" + part, Toolkit.Css(part)!);
        }

        Same("iconlib/matui-iconlib.svg", Icons.Sprite);
        Same("iconlib/iconlib.json", Icons.Catalogue);
        Same("iconlib/LICENSE.txt", Icons.License);
        Same(Styleguide.CssName, Styleguide.Css());
        Same(Styleguide.SampleName, Styleguide.SampleImage());
        Same(Styleguide.HtmlName, Styleguide.Html(_studio.Design, Toolkit, "0.1.0", Toolkit.Hash));

        // the archive: the same entries with the same content as the archive the toolkit builds
        using var released = new ZipArchive(File.OpenRead(Path.Combine(dir, "matui-0.1.0.zip")));
        using var expectedZip = new ZipArchive(new MemoryStream(Toolkit.ReleaseZip("0.1.0")));
        Assert.Equal(expectedZip.Entries.Select(e => e.FullName), released.Entries.Select(e => e.FullName));
        foreach (var entry in expectedZip.Entries)
        {
            using var a = new StreamReader(entry.Open());
            using var b = new StreamReader(released.GetEntry(entry.FullName)!.Open());
            Assert.Equal(a.ReadToEnd(), b.ReadToEnd());
        }

        // the manifest names the toolkit's hash and carries no white label
        var text = File.ReadAllText(Path.Combine(dir, "manifest.json"));
        Assert.DoesNotContain("whiteLabel", text);
        var manifest = JsonSerializer.Deserialize<ReleaseManifest>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(Toolkit.Hash, manifest.Hash);
        Assert.Null(manifest.WhiteLabel);
        Assert.Equal(["matui.css", "matui.js", "matui.boot.js", "CHANGELOG.md", "matui-icons.svg"], manifest.Files.Select(f => f.Path).Take(5));
    }

    [Fact]
    public async Task A_setting_that_renames_nothing_gives_the_same_release_as_none()
    {
        await _studio.SignIn();
        await Released("0.1.0");
        await SaveOk("mat", "MatUi");
        var run = await Released("0.1.1");
        Assert.True(run.Succeeded);

        foreach (var file in Directory.EnumerateFiles(Dir("0.1.0"), "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Dir("0.1.0"), file).Replace('\\', '/');
            if (relative is "manifest.json" or "matui-0.1.0.zip" or "matui.css" or "matui.js" or "matui.boot.js" or "styleguide.html" or "matui.manifest.json")
            {
                continue;
            }

            Assert.True(File.ReadAllBytes(file).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(Dir("0.1.1"), relative))), relative + " differs");
        }

        Assert.False(File.Exists(Path.Combine(Dir("0.1.1"), "acmeui.css")));
        Assert.DoesNotContain("whiteLabel", File.ReadAllText(Path.Combine(Dir("0.1.1"), "manifest.json")));
        Assert.Contains("/* MatUi 0.1.1 (", File.ReadAllText(Path.Combine(Dir("0.1.1"), "matui.css")));
    }

    [Fact]
    public async Task A_release_with_a_setting_carries_the_renamed_files_the_log_and_a_manifest_of_what_it_wrote()
    {
        await _studio.SignIn();
        await SaveOk("acme", "AcmeUi");
        var run = await Released("0.1.0");

        Assert.True(run.Succeeded, string.Join("\n", run.Stages.SelectMany(s => s.Log)));
        var build = run.Stages[2].Log;
        Assert.Contains(build, l => l.StartsWith("White label: prefix mat- to acme-, name MatUi to AcmeUi: ", StringComparison.Ordinal));
        Assert.Contains(build, l => l.Contains("acmeui.css: ", StringComparison.Ordinal) && l.Contains("replacements", StringComparison.Ordinal));
        Assert.Contains(build, l => l.Contains("acmeui.js: ", StringComparison.Ordinal));
        Assert.Contains(build, l => l.Contains("design.json: ", StringComparison.Ordinal));
        Assert.Contains(build, l => l.StartsWith("The checks of the toolkit run on the copy: All 9 checks pass.", StringComparison.Ordinal));
        Assert.Contains(build, l => l.StartsWith("acmeui.css  ", StringComparison.Ordinal));
        Assert.DoesNotContain(build, l => l.StartsWith("matui", StringComparison.Ordinal));

        var dir = Dir("0.1.0");
        var names = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/')).ToList();
        Assert.Contains("acmeui.css", names);
        Assert.Contains("acmeui-0.1.0.zip", names);
        Assert.Contains("css/acmeui.tokens.css", names);
        Assert.Contains("iconlib/acmeui-iconlib.svg", names);
        Assert.DoesNotContain(names, n => n.Contains("matui", StringComparison.Ordinal));

        var manifest = JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(Path.Combine(dir, "manifest.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(new WhiteLabelSetting("acme", "AcmeUi"), manifest.WhiteLabel);
        Assert.Contains("\"whiteLabel\"", File.ReadAllText(Path.Combine(dir, "manifest.json")));
        Assert.NotEqual(Toolkit.Hash, manifest.Hash);
        var css = File.ReadAllText(Path.Combine(dir, "acmeui.css"));
        Assert.StartsWith($"/* AcmeUi 0.1.0 ({manifest.Hash}) - build ", css);
        Assert.Contains(".acme-btn", css);
        Assert.DoesNotContain("mat-btn", css);
        foreach (var file in manifest.Files)
        {
            var path = Path.Combine(dir, file.Path);
            Assert.Equal(file.Sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
            Assert.Equal(new FileInfo(path).Length, file.Bytes);
        }

        // the archive and its own manifest
        using var zip = new ZipArchive(File.OpenRead(Path.Combine(dir, "acmeui-0.1.0.zip")));
        using var inner = new StreamReader(zip.GetEntry("acmeui/acmeui.manifest.json")!.Open());
        var innerManifest = JsonDocument.Parse(inner.ReadToEnd()).RootElement;
        Assert.Equal("AcmeUi", innerManifest.GetProperty("name").GetString());
        Assert.Equal(manifest.Hash, innerManifest.GetProperty("hash").GetString());
        Assert.Equal(File.ReadAllText(Path.Combine(dir, "acmeui.js")), new StreamReader(zip.GetEntry("acmeui/acmeui.js")!.Open()).ReadToEnd());

        // served, and the list of releases links the archive by its name
        Assert.StartsWith("/* AcmeUi 0.1.0 (", await _studio.Get("/releases/0.1.0/acmeui.css"));
        Assert.Equal(HttpStatusCode.NotFound, (await _studio.Client.GetAsync("/releases/0.1.0/matui.css")).StatusCode);
        Assert.Contains("href=\"/releases/0.1.0/acmeui-0.1.0.zip\"", await _studio.Get("/Studio/Releases"));
        Assert.Contains("<script src=\"acmeui.js\" defer></script>", await _studio.Get("/releases/0.1.0/styleguide.html"));
        Assert.Contains("\"whiteLabel\"", await _studio.Get("/releases/latest.json"));
    }

    [Fact]
    public async Task A_later_release_without_the_setting_goes_back_to_the_toolkits_names_and_the_first_stays_as_it_was()
    {
        await _studio.SignIn();
        await SaveOk("acme", "AcmeUi");
        await Released("0.1.0");
        var before = File.ReadAllBytes(Path.Combine(Dir("0.1.0"), "acmeui.css"));
        var reset = await _studio.Post(Page, Page + "?handler=Reset", new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);

        var run = await Released("0.1.1");

        Assert.True(run.Succeeded);
        Assert.True(File.Exists(Path.Combine(Dir("0.1.1"), "matui.css")));
        Assert.False(File.Exists(Path.Combine(Dir("0.1.1"), "acmeui.css")));
        Assert.True(File.ReadAllBytes(Path.Combine(Dir("0.1.0"), "acmeui.css")).AsSpan().SequenceEqual(before));
        Assert.Equal(["0.1.1", "0.1.0"], _studio.Releases.Releases().Select(m => m.Version));
    }

    [Fact]
    public async Task A_setting_on_the_volume_that_is_not_valid_stops_the_release_at_the_check_and_is_reported()
    {
        await _studio.SignIn();
        // edited by hand into something the build cannot use
        _studio.Store.Save("whitelabel", "whitelabel", new WhiteLabelSetting("data", "AcmeUi"), "admin", "By hand");
        _studio.Design.Reload();

        var result = _studio.Checks.Run();
        Assert.Contains(result.Results.Single(r => r.Check.Id == "whitelabel").Findings, f => f.Severity == Severity.Error && f.Kind == "whitelabel");
        var run = await Released("0.1.0");

        Assert.False(run.Succeeded);
        Assert.Equal(StageStatus.Failed, run.Stages[0].Status);
        Assert.Contains(run.Stages[0].Log, l => l.Contains("White label setting", StringComparison.Ordinal) || l.Contains("white label", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(StageStatus.Skipped, run.Stages[2].Status);
        Assert.False(Directory.Exists(Dir("0.1.0")));
        Assert.Contains("/Studio/Releases/WhiteLabel", (await _studio.Get("/Studio/Checks")));
    }

    [Fact]
    public async Task A_valid_setting_passes_the_check_of_the_studio_and_the_report_has_it()
    {
        await _studio.SignIn();
        await SaveOk("acme", "AcmeUi");
        var report = _studio.Checks.Run();

        var check = report.Results.Single(r => r.Check.Id == "whitelabel");
        Assert.True(check.Passed);
        Assert.Equal("Release", check.Check.Area);
        Assert.True(report.Passed);
        Assert.Equal("/Studio/Releases/WhiteLabel", _studio.Checks.UrlOf(new Finding("whitelabel", Severity.Error, "x", "whitelabel", "whitelabel")));
    }

    [Fact]
    public async Task The_trial_build_changes_nothing_on_the_volume_and_the_toolkit_files_of_the_site_stay_the_toolkits()
    {
        await _studio.SignIn();
        var directory = Path.Combine(_studio.DataDirectory, "releases");
        var before = Directory.Exists(directory) ? Directory.GetFileSystemEntries(directory).Length : 0;
        var bundle = Toolkit.CssBundle;

        await Save("acme", "AcmeUi", "?handler=Check");

        Assert.Equal(before, Directory.Exists(directory) ? Directory.GetFileSystemEntries(directory).Length : 0);
        Assert.Equal(bundle, Toolkit.CssBundle);
        Assert.StartsWith("/* MatUi ", Toolkit.CssBundle);
        Assert.Contains("/ui/matui.css", await _studio.Get("/Studio"));
        Assert.DoesNotContain("acme-", await _studio.Get("/Studio"));
    }
}
