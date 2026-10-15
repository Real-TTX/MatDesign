using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>
/// Stage 12 of docs/PRODUCT.md in the Studio: the page of the component properties, the overrides of an app, what a brand sets inside a colour scheme; each reaches the bundle, a release and the archive in the
/// order of the cascade (the toolkit, the brand, the app), is refused outside its limits, has a history and a restore, and is held by the checks and by the release pipeline.
/// </summary>
public sealed partial class StudioBrandStage12Tests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    public StudioBrandStage12Tests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    private ToolkitFiles Toolkit => _studio.Factory.Services.GetRequiredService<ToolkitFiles>();

    [GeneratedRegex(@"style\s*=")]
    private static partial Regex StyleAttribute();

    // ---- the forms

    private static List<KeyValuePair<string, string>> Components(Dictionary<string, string> values)
        => values.Select(p => new KeyValuePair<string, string>("component:" + p.Key, p.Value)).ToList();

    private Task<HttpResponseMessage> SaveComponents(Dictionary<string, string> values, string query = "")
        => _studio.Post("/Studio/Brand/Components", "/Studio/Brand/Components" + query, Components(values));

    private async Task SaveComponentsOk(Dictionary<string, string> values)
        => Assert.Equal(HttpStatusCode.Redirect, (await SaveComponents(values)).StatusCode);

    private static List<KeyValuePair<string, string>> BrandForm(Dictionary<string, string>? light = null, Dictionary<string, Dictionary<string, string>>? schemes = null)
    {
        var fields = new List<KeyValuePair<string, string>> { new("Input.Company", "Acme"), new("Input.ShortName", "Acme"), new("Input.Tagline", "Anvils") };
        fields.AddRange((light ?? []).Select(p => new KeyValuePair<string, string>("light:" + p.Key, p.Value)));
        foreach (var (scheme, values) in schemes ?? [])
        {
            fields.AddRange(values.Select(p => new KeyValuePair<string, string>($"scheme:{scheme}:{p.Key}", p.Value)));
        }

        return fields;
    }

    private Task<HttpResponseMessage> SaveBrand(List<KeyValuePair<string, string>> form) => _studio.Post("/Studio/Brand", "/Studio/Brand", form);

    private Task<HttpResponseMessage> SaveApp(Dictionary<string, string> overrides, string id = "matx", string original = "", string query = "", bool marker = true)
    {
        var fields = StudioHarness.AppForm(id, original).AsEnumerable().ToList();
        if (marker)
        {
            fields.Add(new("overrides-present", "1"));
        }

        fields.AddRange(overrides.Select(p => new KeyValuePair<string, string>(p.Key, p.Value)));
        return _studio.Post("/Studio/Apps/Edit" + (original.Length > 0 ? "?id=" + original : string.Empty), "/Studio/Apps/Edit" + query, fields);
    }

    private async Task SaveAppOk(Dictionary<string, string> overrides, string id = "matx", string original = "")
        => Assert.Equal(HttpStatusCode.Redirect, (await SaveApp(overrides, id, original)).StatusCode);

    // ---- the page of the component properties

    [Fact]
    public async Task The_page_is_behind_the_sign_in_and_has_a_row_for_every_property_a_preview_for_every_group_and_the_tabs()
    {
        Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync("/Studio/Brand/Components")).StatusCode);
        await _studio.SignIn();
        var html = await _studio.Get("/Studio/Brand/Components");

        Assert.Equal(1, html.Split("<h1").Length - 1);
        Assert.DoesNotMatch(StyleAttribute(), html);
        Assert.Contains("68 properties in 16 components", html);
        foreach (var property in Toolkit.EditableProperties)
        {
            Assert.Contains($"data-prop=\"{property.Name}\"", html);
            Assert.Contains($"name=\"component:{property.Name}\"", html);
            Assert.Contains($"id=\"component-{property.Key}\"", html);
        }

        Assert.Equal(16, Regex.Matches(html, "data-prop-group=\"").Count);
        Assert.Equal(5, Regex.Matches(html, "data-touch=\"true\"").Count);
        // the controls of the preview cannot submit the editor, and the inputs belong to the form by its id
        Assert.Contains("form=\"pv-none\"", html);
        Assert.Contains("form=\"components-form\"", html);
        var form = html[html.IndexOf("<form id=\"components-form\"", StringComparison.Ordinal)..html.IndexOf("</form>", StringComparison.Ordinal)];
        Assert.DoesNotContain("data-brand-preview", form);
        // the page and the tabs
        Assert.Contains("<a class=\"mat-tab is-active\" href=\"/Studio/Brand/Components\" aria-current=\"page\">", html);
        Assert.Contains("<a class=\"mat-tab \" href=\"/Studio/Brand\"", html);
        Assert.Contains("<a class=\"mat-nav__item is-active\" href=\"/Studio/Brand\" aria-current=\"page\">", html);
        Assert.Contains("/js/brand.js", html);
        // the brand page links to it and shows the same tabs
        var brand = await _studio.Get("/Studio/Brand");
        Assert.Contains("<a class=\"mat-tab is-active\" href=\"/Studio/Brand\" aria-current=\"page\">", brand);
        Assert.Contains("href=\"/Studio/Brand/Components\"", brand);
    }

    [Fact]
    public async Task A_saved_property_reaches_the_bundle_the_part_the_archive_and_the_hash_after_the_tokens_of_the_brand()
    {
        await _studio.SignIn();
        var hash = Toolkit.Hash;
        Assert.DoesNotContain("Component properties of the brand", await _studio.Get("/ui/matui.css"));

        Assert.Equal(HttpStatusCode.Redirect, (await SaveBrand(BrandForm(new() { ["--mat-radius"] = "10px" }))).StatusCode);
        await SaveComponentsOk(new() { ["--mat-btn-radius"] = "0", ["--mat-card-pad"] = "1rem", ["--mat-dialog-width"] = "480px" });

        var css = await _studio.Get("/ui/matui.css");
        Assert.Contains("  --mat-btn-radius: 0px;\n  --mat-card-pad: 1rem;\n  --mat-dialog-width: 480px;\n}", css);
        Assert.NotEqual(hash, Toolkit.Hash);
        // the order of the cascade inside the bundle: the tokens file, the brand's tokens, the brand's properties
        Assert.True(css.IndexOf("--mat-radius-xl: 12px;", StringComparison.Ordinal) < css.IndexOf("Brand set in the Studio", StringComparison.Ordinal));
        Assert.True(css.IndexOf("Brand set in the Studio", StringComparison.Ordinal) < css.IndexOf("Component properties of the brand", StringComparison.Ordinal));
        Assert.Contains("--mat-btn-radius: 0px;", await _studio.Get("/ui/css/matui.apps.css"));
        using var zip = new ZipArchive(new MemoryStream(await _studio.Client.GetByteArrayAsync("/ui/matui.zip")));
        foreach (var name in new[] { "matui/matui.css", "matui/css/matui.apps.css" })
        {
            using var reader = new StreamReader(zip.GetEntry(name)!.Open());
            Assert.Contains("--mat-btn-radius: 0px;", await reader.ReadToEndAsync());
        }

        var manifest = JsonDocument.Parse(await _studio.Get("/ui/manifest.json")).RootElement;
        Assert.Contains(manifest.GetProperty("files").EnumerateArray(), f => f.GetProperty("name").GetString() == "css/matui.apps.css");
        Assert.Equal(10, ToolkitFiles.CssParts.Count);
        Assert.Equal("0px", _studio.Design.Brand.ComponentValues["--mat-btn-radius"]);
    }

    [Fact]
    public async Task A_height_under_44_px_is_written_for_a_fine_pointer_only_and_one_of_44_px_or_more_everywhere()
    {
        await _studio.SignIn();
        await SaveComponentsOk(new() { ["--mat-btn-height"] = "36px", ["--mat-input-height"] = "48px", ["--mat-tabs-height"] = "2.25rem" });

        var blocks = CssTokens.ParseBlocks(Toolkit.Css("matui.apps.css")!);
        var fine = blocks.Single(b => b.Media == "(pointer: fine)");
        Assert.Equal("--mat-btn-height", fine.Declarations[0].Name);
        Assert.Equal(["--mat-btn-height", "--mat-tabs-height"], fine.Declarations.Select(d => d.Name));
        var everywhere = blocks.Single(b => b.Selector == ":root" && b.Media is null && b.Declarations.Any(d => d.Name == "--mat-input-height"));
        Assert.Equal(["--mat-input-height"], everywhere.Declarations.Select(d => d.Name));
        // nothing that is under 44 px is outside the media query, and the toolkit's own coarse rule is still the one that gives a touch screen 44 px
        Assert.DoesNotContain("--mat-btn-height: 36px", Toolkit.Css("matui.apps.css")!.Replace("@media (pointer: fine) {\n  :root {\n    --mat-btn-height: 36px;", string.Empty), StringComparison.Ordinal);
        Assert.Contains("@media (pointer: coarse) {\n  :root {\n    --mat-control-h: 44px;", Toolkit.TokensCss);
    }

    [Theory]
    [InlineData("--mat-btn-radius", "30px", "above the most")]
    [InlineData("--mat-btn-height", "31px", "MOBILE-04")]
    [InlineData("--mat-dialog-width", "100px", "below the least")]
    [InlineData("--mat-card-pad", "60px", "above the most")]
    [InlineData("--mat-card-border-width", "9px", "above the most")]
    [InlineData("--mat-btn-font-weight", "heavy", "A number without a unit")]
    [InlineData("--mat-btn-radius", "4px; } body { injected-marker: 1", "does not belong")]
    [InlineData("--mat-bg", "#ffffff", "not a documented component property")]
    public async Task A_value_outside_its_type_or_limits_is_refused_at_its_field_with_the_reason_and_nothing_is_saved(string name, string value, string message)
    {
        await _studio.SignIn();
        var response = await SaveComponents(new() { [name] = value });
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Please check the properties.", html);
        Assert.Null(_studio.Store.Current("brand", "brand"));
        Assert.DoesNotContain("injected-marker", await _studio.Get("/ui/matui.css"));
    }

    [Fact]
    public async Task Check_shows_the_result_and_saves_nothing_and_each_page_keeps_what_the_other_one_saved()
    {
        await _studio.SignIn();
        var check = await SaveComponents(new() { ["--mat-btn-radius"] = "0" }, "?check=true");
        Assert.Contains("All checks pass.", await check.Content.ReadAsStringAsync());
        Assert.Null(_studio.Store.Current("brand", "brand"));

        // a brand with tokens and a scheme, then properties: the tokens and the scheme stay
        Assert.Equal(HttpStatusCode.Redirect, (await SaveBrand(BrandForm(new() { ["--mat-radius"] = "10px" }, new() { ["paper"] = new() { ["light:--mat-bg"] = "#fbf6ea" } }))).StatusCode);
        await SaveComponentsOk(new() { ["--mat-btn-radius"] = "0" });
        var brand = _studio.Design.Brand;
        Assert.Equal("10px", brand.Light["--mat-radius"]);
        Assert.Equal("#fbf6ea", brand.SchemeFor("paper")!.Light["--mat-bg"]);
        Assert.Equal("0px", brand.ComponentValues["--mat-btn-radius"]);

        // and the brand page keeps the properties
        Assert.Equal(HttpStatusCode.Redirect, (await SaveBrand(BrandForm(new() { ["--mat-radius"] = "12px" }, new() { ["paper"] = new() { ["light:--mat-bg"] = "#fbf6ea" } }))).StatusCode);
        Assert.Equal("0px", _studio.Design.Brand.ComponentValues["--mat-btn-radius"]);
        Assert.Equal("12px", _studio.Design.Brand.Light["--mat-radius"]);
        Assert.Equal(2, _studio.Store.History("brand", "brand").Count - 1);
    }

    [Fact]
    public async Task Clearing_every_property_writes_nothing_and_the_files_are_what_they_were_before()
    {
        await _studio.SignIn();
        var before = await _studio.Get("/ui/matui.css");
        await SaveComponentsOk(new() { ["--mat-btn-radius"] = "0" });
        Assert.NotEqual(before, await _studio.Get("/ui/matui.css"));
        await SaveComponentsOk(new() { ["--mat-btn-radius"] = string.Empty, ["--mat-card-pad"] = string.Empty });
        Assert.Equal(before, await _studio.Get("/ui/matui.css"));
        Assert.Equal(TestPaths.ReadToolkit("css/matui.apps.css"), Toolkit.Css("matui.apps.css"));
    }

    // ---- inside a colour scheme

    [Fact]
    public async Task What_a_brand_sets_inside_a_scheme_is_written_after_the_scheme_with_the_dark_set_twice_and_identical()
    {
        await _studio.SignIn();
        var page = await _studio.Get("/Studio/Brand");
        Assert.Contains("Inside the colour schemes", page);
        Assert.Contains("name=\"scheme:paper:light:--mat-bg\"", page);
        Assert.Contains("name=\"scheme:paper:dark:--mat-bg\"", page);
        Assert.Contains("name=\"scheme:oled:dark:--mat-bg\"", page);
        Assert.DoesNotContain("name=\"scheme:oled:light:", page);
        Assert.DoesNotContain("name=\"scheme:paper:light:--mat-radius\"", page);

        var response = await SaveBrand(BrandForm(schemes: new()
        {
            ["paper"] = new() { ["light:--mat-bg"] = "#fbf6ea", ["dark:--mat-bg"] = "#1c1810", ["dark:--mat-surface"] = "#242014" },
            ["oled"] = new() { ["dark:--mat-surface"] = "#0c0c0c" },
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var css = Toolkit.Css("matui.apps.css")!;
        var blocks = CssTokens.ParseBlocks(css);
        var dark = blocks.Single(b => b.Selector == ":root[data-scheme=\"paper\"][data-theme=\"dark\"]");
        var system = blocks.Single(b => b.Selector == ":root[data-scheme=\"paper\"]:not([data-theme=\"light\"]):not([data-theme=\"dark\"])");
        Assert.Equal(dark.Declarations.Select(d => (d.Name, d.Value)), system.Declarations.Select(d => (d.Name, d.Value)));
        Assert.Equal(["--mat-bg", "--mat-surface"], dark.Declarations.Select(d => d.Name));
        Assert.Contains(blocks, b => b.Selector == ":root[data-scheme=\"paper\"]" && b.Declarations.Single().Value == "#fbf6ea");
        Assert.DoesNotContain(blocks, b => b.Selector == ":root[data-scheme=\"oled\"]");
        // after the scheme blocks of the tokens file, in the bundle
        var bundle = await _studio.Get("/ui/matui.css");
        Assert.True(bundle.LastIndexOf(":root[data-scheme=\"paper\"] {", StringComparison.Ordinal) > bundle.IndexOf("--mat-bg: #f4ecda", StringComparison.Ordinal) || bundle.Contains("#fbf6ea"));
        Assert.True(bundle.IndexOf("Tokens of the brand inside a colour scheme", StringComparison.Ordinal) > bundle.IndexOf(":root[data-scheme=\"oled\"][data-theme=\"dark\"] {", StringComparison.Ordinal));
        Assert.Equal("#0c0c0c", _studio.Design.Brand.SchemeFor("oled")!.Dark["--mat-surface"]);
    }

    [Fact]
    public async Task A_scheme_value_that_breaks_the_contrast_of_that_scheme_is_refused_with_the_numbers()
    {
        await _studio.SignIn();
        var response = await SaveBrand(BrandForm(schemes: new() { ["paper"] = new() { ["light:--mat-text"] = "#c8c8c0" } }));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("In the paper scheme.", html);
        Assert.Contains(":1, needs 4.50:1", html);
        Assert.Contains("value=\"#c8c8c0\"", html);
        Assert.Null(_studio.Store.Current("brand", "brand"));
    }

    [Fact]
    public async Task A_token_a_scheme_does_not_set_and_a_light_value_of_oled_are_refused()
    {
        await _studio.SignIn();
        var response = await SaveBrand(BrandForm(schemes: new() { ["paper"] = new() { ["light:--mat-radius"] = "4px" }, ["oled"] = new() { ["light:--mat-bg"] = "#000000" } }));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("not one of the tokens the paper scheme sets", html);
        Assert.Contains("no light values", html);
        Assert.Null(_studio.Store.Current("brand", "brand"));
    }

    // ---- history, restore, checks, the release

    [Fact]
    public async Task The_history_names_the_properties_and_a_restore_brings_the_old_ones_back_as_a_new_revision()
    {
        await _studio.SignIn();
        await SaveComponentsOk(new() { ["--mat-btn-radius"] = "0" });
        await SaveComponentsOk(new() { ["--mat-btn-radius"] = "4px", ["--mat-card-pad"] = "1rem" });

        var history = await _studio.Get("/Studio/History?kind=brand&id=brand");
        Assert.Contains("<code>components.--mat-btn-radius</code>", history);
        Assert.Contains("<code>components.--mat-card-pad</code>", history);
        Assert.Contains(_studio.Store.Current("brand", "brand")!.Note, new[] { "Changed component --mat-btn-radius, component --mat-card-pad" });

        var restored = await _studio.Post("/Studio/Brand/Components", "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = "brand", ["id"] = "brand", ["revision"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, restored.StatusCode);
        Assert.Equal(3, _studio.Store.History("brand", "brand").Count);
        Assert.Equal("0px", _studio.Design.Brand.ComponentValues["--mat-btn-radius"]);
        Assert.False(_studio.Design.Brand.ComponentValues.ContainsKey("--mat-card-pad"));
        Assert.Contains("Restored", await _studio.Get("/Studio/Activity?kind=brand"));
    }

    private void HandEditBrand(Brand brand)
    {
        _studio.Store.Save("brand", "brand", brand, "admin", "Edited by hand");
        _studio.Design.Reload();
    }

    [Fact]
    public async Task A_revision_with_a_property_that_is_outside_its_limits_is_not_restored_and_the_checks_and_the_release_refuse_it()
    {
        await _studio.SignIn();
        await SaveComponentsOk(new() { ["--mat-btn-radius"] = "0" });
        HandEditBrand(Brand.BuiltIn with { Components = new Dictionary<string, string> { ["--mat-btn-height"] = "20px" } });
        await SaveComponentsOk(new() { ["--mat-btn-radius"] = "2px" });

        var refused = await _studio.Post("/Studio/Brand/Components", "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = "brand", ["id"] = "brand", ["revision"] = "2" });
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        var html = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());
        Assert.Contains("cannot be restored as it is today", html);
        Assert.Contains("MOBILE-04", html);
        Assert.Equal(3, _studio.Store.History("brand", "brand").Count);

        // the bad revision as the current one: the checks find it, the site does not write it and a release is refused
        HandEditBrand(Brand.BuiltIn with { Components = new Dictionary<string, string> { ["--mat-btn-height"] = "20px", ["--mat-card-pad"] = "70px" } });
        var report = _studio.Checks.Run();
        Assert.False(report.Passed);
        Assert.Contains(report.Findings, f => f.Check == "brand" && f.Severity == Severity.Error && f.Message.Contains("Component:--mat-btn-height", StringComparison.Ordinal) && f.Message.Contains("MOBILE-04", StringComparison.Ordinal));
        Assert.Contains(report.Findings, f => f.Check == "brand" && f.Message.Contains("Component:--mat-card-pad", StringComparison.Ordinal));
        Assert.DoesNotContain("--mat-btn-height: 20px", await _studio.Get("/ui/matui.css"));

        var run = _studio.Releases.Execute(Ver.First, string.Empty, "admin");
        Assert.False(run.Succeeded);
        Assert.Equal(StageStatus.Failed, run.Stages[0].Status);
        Assert.Empty(_studio.Releases.Releases());
    }

    [Fact]
    public async Task A_scheme_value_on_the_volume_that_fails_the_contrast_is_found_by_the_checks()
    {
        await _studio.SignIn();
        HandEditBrand(Brand.BuiltIn with { Schemes = new Dictionary<string, SchemeValues> { ["paper"] = new(new Dictionary<string, string> { ["--mat-text"] = "#c8c8c0" }, new Dictionary<string, string>()) } });
        var report = _studio.Checks.Run();
        Assert.Contains(report.Findings, f => f.Check == "brand" && f.Message.Contains("In the paper scheme", StringComparison.Ordinal));
        Assert.False(_studio.Releases.Execute(Ver.First, string.Empty, "admin").Succeeded);
    }

    [Fact]
    public async Task A_brand_on_the_volume_with_an_unsafe_property_or_scheme_value_is_left_out()
    {
        await _studio.SignIn();
        HandEditBrand(Brand.BuiltIn with { Components = new Dictionary<string, string> { ["--mat-btn-radius"] = "0; } * { injected-marker: 1" } });
        Assert.Null(_studio.Design.StudioBrand);
        HandEditBrand(Brand.BuiltIn with { Schemes = new Dictionary<string, SchemeValues> { ["paper"] = new(new Dictionary<string, string> { ["--mat-bg"] = "red; }" }, new Dictionary<string, string>()) } });
        Assert.Null(_studio.Design.StudioBrand);
        Assert.DoesNotContain("injected-marker", await _studio.Get("/ui/matui.css"));
        Assert.Contains(_studio.Checks.Run().Findings, f => f.Check == "volume" && f.Id == "brand");
    }

    [Fact]
    public async Task A_release_carries_the_properties_the_schemes_and_the_overrides_of_an_app_in_the_bundle_the_archive_and_the_design_data()
    {
        await _studio.SignIn();
        Assert.Equal(HttpStatusCode.Redirect, (await SaveBrand(BrandForm(schemes: new() { ["paper"] = new() { ["light:--mat-bg"] = "#fbf6ea" } }))).StatusCode);
        await SaveComponentsOk(new() { ["--mat-btn-radius"] = "0", ["--mat-btn-height"] = "36px" });
        await SaveAppOk(new() { ["light:--mat-radius"] = "2px", ["property:--mat-card-pad"] = "1rem" });

        var run = _studio.Releases.Execute(Ver.First, "Stage 12.", "admin");
        Assert.True(run.Succeeded, string.Join("\n", run.Stages.SelectMany(s => s.Log)));

        var css = await _studio.Get($"/releases/{Ver.First}/matui.css");
        Assert.Contains("  --mat-btn-radius: 0px;", css);
        Assert.Contains("@media (pointer: fine) {\n  :root {\n    --mat-btn-height: 36px;", css);
        Assert.Contains(":root[data-scheme=\"paper\"] {\n  --mat-bg: #fbf6ea;", css);
        Assert.Contains("[data-app=\"matx\"] {\n  --mat-radius: 2px;\n}", css);
        Assert.Contains("[data-app=\"matx\"] {\n  --mat-card-pad: 1rem;\n}", css);
        // brand first, then the app
        Assert.True(css.IndexOf("Component properties of the brand", StringComparison.Ordinal) < css.IndexOf("Overrides of apps set in the Studio", StringComparison.Ordinal));
        using var zip = new ZipArchive(new MemoryStream(await _studio.Client.GetByteArrayAsync($"/releases/{Ver.First}/matui-{Ver.First}.zip")));
        foreach (var name in new[] { "matui/matui.css", "matui/css/matui.apps.css" })
        {
            using var reader = new StreamReader(zip.GetEntry(name)!.Open());
            var text = await reader.ReadToEndAsync();
            Assert.Contains("--mat-btn-radius: 0px;", text);
            Assert.Contains("[data-app=\"matx\"]", text);
        }

        var design = JsonDocument.Parse(await _studio.Get($"/releases/{Ver.First}/design.json")).RootElement;
        var brand = design.GetProperty("brand");
        Assert.Equal("0px", brand.GetProperty("components").GetProperty("--mat-btn-radius").GetString());
        Assert.Equal("#fbf6ea", brand.GetProperty("schemes").GetProperty("paper").GetProperty("light").GetProperty("--mat-bg").GetString());
        var app = design.GetProperty("apps").EnumerateArray().Single(a => a.GetProperty("id").GetString() == "matx");
        Assert.Equal("2px", app.GetProperty("overrides").GetProperty("light").GetProperty("--mat-radius").GetString());
        Assert.Equal("1rem", app.GetProperty("overrides").GetProperty("properties").GetProperty("--mat-card-pad").GetString());
        Assert.Equal(JsonValueKind.Null, design.GetProperty("apps").EnumerateArray().Single(a => a.GetProperty("id").GetString() == "matmail").GetProperty("overrides").ValueKind);

        // the changelog of the next release names what was touched
        Assert.Contains(_studio.Releases.Latest!.Changelog, l => l.StartsWith("Brand brand:", StringComparison.Ordinal) && l.Contains("component --mat-btn-radius", StringComparison.Ordinal));
        Assert.Contains(_studio.Releases.Latest!.Changelog, l => l.StartsWith("App matx:", StringComparison.Ordinal));
        // the manifest holds the hash of the part that carries all of it
        var directory = Path.Combine(_studio.DataDirectory, "releases", Ver.First);
        var manifest = JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(Path.Combine(directory, "manifest.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var entry = manifest.Files.Single(f => f.Path == "css/matui.apps.css");
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, "css", "matui.apps.css")))).ToLowerInvariant(), entry.Sha256);
        Assert.Contains("[data-app=\"matx\"]", File.ReadAllText(Path.Combine(directory, "css", "matui.apps.css")));
    }

    // ---- the overrides of an app

    [Fact]
    public async Task The_edit_page_of_an_app_has_the_section_with_its_tokens_the_properties_and_the_previews_scoped_to_the_app()
    {
        await _studio.SignIn();
        await SaveAppOk(new());
        var html = await _studio.Get("/Studio/Apps/Edit?id=matx");

        Assert.Equal(1, html.Split("<h1").Length - 1);
        Assert.Contains("Overrides of this app", html);
        Assert.Contains("name=\"overrides-present\"", html);
        foreach (var token in AppOverrides.Tokens(Toolkit.EditableTokens))
        {
            Assert.Contains($"name=\"light:{token.Name}\"", html);
        }

        Assert.DoesNotContain("name=\"light:--mat-bg\"", html);
        foreach (var property in Toolkit.EditableProperties)
        {
            Assert.Contains($"name=\"property:{property.Name}\"", html);
        }

        Assert.Equal(16, Regex.Matches(html, "data-app=\"matx\"").Count(m => m.Index > 0) - Regex.Matches(html, "<html[^>]*data-app=\"matx\"").Count);
        Assert.Contains("/js/brand.js", html);
        Assert.DoesNotMatch(StyleAttribute(), html);
        // a new app has no id to scope a preview to
        var fresh = await _studio.Get("/Studio/Apps/Edit");
        var at = fresh.IndexOf("data-prop-group=\"btn\" data-app", StringComparison.Ordinal);
        Assert.True(at < 0, at < 0 ? string.Empty : fresh.Substring(at - 100, 300));
    }

    [Fact]
    public async Task An_apps_overrides_reach_the_bundle_on_data_app_after_the_brand_and_win_only_for_that_app()
    {
        await _studio.SignIn();
        Assert.Equal(HttpStatusCode.Redirect, (await SaveBrand(BrandForm(new() { ["--mat-radius"] = "10px" }))).StatusCode);
        await SaveComponentsOk(new() { ["--mat-btn-radius"] = "8px" });
        await SaveAppOk(new() { ["light:--mat-radius"] = "2px", ["property:--mat-btn-radius"] = "0", ["property:--mat-btn-height"] = "34px" });

        var css = await _studio.Get("/ui/matui.css");
        var blocks = CssTokens.ParseBlocks(css);
        Assert.Equal("2px", blocks.Single(b => b.Selector == "[data-app=\"matx\"]" && b.Declarations.Any(d => d.Name == "--mat-radius")).Declarations.Single(d => d.Name == "--mat-radius").Value);
        var fine = blocks.Single(b => b.Selector == "[data-app=\"matx\"]" && b.Media == "(pointer: fine)");
        Assert.Equal(["--mat-btn-height"], fine.Declarations.Select(d => d.Name));
        // toolkit, brand tokens, brand properties, then the app: the order of the cascade at equal specificity
        var toolkit = css.IndexOf("--mat-radius-xl: 12px;", StringComparison.Ordinal);
        var brandTokens = css.IndexOf(":root {\n  --mat-radius: 10px;", StringComparison.Ordinal);
        var brandProps = css.IndexOf("  --mat-btn-radius: 8px;", StringComparison.Ordinal);
        var app = css.IndexOf("[data-app=\"matx\"] {\n  --mat-radius: 2px;", StringComparison.Ordinal);
        Assert.True(toolkit < brandTokens && brandTokens < brandProps && brandProps < app, $"{toolkit} {brandTokens} {brandProps} {app}");
        // the built-in colours of the apps stay where they were, before all of it
        Assert.True(css.IndexOf("[data-app=\"matmail\"]", StringComparison.Ordinal) < brandTokens);
        // no other app is touched
        Assert.DoesNotContain("[data-app=\"matmail\"] {\n  --mat-radius", css);
        Assert.Equal("2px", _studio.Design.App("matx")!.Tokens!.Light["--mat-radius"]);
    }

    [Fact]
    public async Task An_app_that_sets_nothing_writes_nothing_and_the_bundle_is_byte_identical_to_one_without_the_feature()
    {
        await _studio.SignIn();
        await SaveAppOk(new());
        var withApp = await _studio.Get("/ui/css/matui.apps.css");
        Assert.Contains("[data-app=\"matx\"] { --mat-app-accent: #0f766e; }", withApp);
        Assert.DoesNotContain("Overrides of apps", withApp);

        // what it writes is the colours of the app and nothing else: the part is the built-in one with that line
        var expected = TestPaths.ReadToolkit("css/matui.apps.css") + ToolkitFiles.AppsOverlay([_studio.Design.App("matx")!]);
        Assert.Equal(expected, withApp);
        Assert.Equal(string.Empty, ToolkitFiles.AppOverridesOverlay(_studio.Design.StudioApps, Toolkit.EditableTokens, Toolkit.EditableProperties));

        // the stored item has no override fields at all
        var stored = _studio.Store.Current("apps", "matx")!.Data;
        Assert.False(stored.TryGetProperty("tokens", out _));
        Assert.False(stored.TryGetProperty("properties", out _));
    }

    [Theory]
    [InlineData("light:--mat-radius", "30px", "above the most")]
    [InlineData("light:--mat-bg", "#ffffff", "belongs to the brand")]
    [InlineData("light:--mat-fs-md", "11px", "Body text")]
    [InlineData("dark:--mat-radius", "4px", "no dark value")]
    [InlineData("property:--mat-btn-height", "20px", "MOBILE-04")]
    [InlineData("property:--mat-card-pad", "99px", "above the most")]
    [InlineData("property:--mat-btn-radius", "1px; } x { y: 1", "does not belong")]
    [InlineData("property:--mat-nope", "1px", "not a documented component property")]
    public async Task An_apps_value_outside_its_limits_is_refused_at_its_field_and_nothing_is_saved(string field, string value, string message)
    {
        await _studio.SignIn();
        var response = await SaveApp(new() { [field] = value });
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Please check the form.", html);
        Assert.Null(_studio.Store.Current("apps", "matx"));
        Assert.DoesNotContain("[data-app=\"matx\"]", await _studio.Get("/ui/matui.css"));
    }

    [Fact]
    public async Task A_post_without_the_marker_leaves_the_overrides_alone_and_clearing_the_fields_removes_them()
    {
        await _studio.SignIn();
        await SaveAppOk(new() { ["light:--mat-radius"] = "2px", ["property:--mat-btn-radius"] = "0" });
        Assert.Equal(HttpStatusCode.Redirect, (await SaveApp(new(), id: "matx", original: "matx", marker: false)).StatusCode);
        Assert.Equal("2px", _studio.Design.App("matx")!.Tokens!.Light["--mat-radius"]);

        await SaveAppOk(new() { ["light:--mat-radius"] = string.Empty, ["property:--mat-btn-radius"] = string.Empty }, "matx", "matx");
        var app = _studio.Design.App("matx")!;
        Assert.Null(app.Tokens);
        Assert.Null(app.Properties);
        Assert.False(app.HasOverrides);
        Assert.DoesNotContain("Overrides of apps", await _studio.Get("/ui/matui.css"));
    }

    [Fact]
    public async Task A_built_in_app_can_carry_overrides_and_resetting_it_brings_the_bundle_back()
    {
        await _studio.SignIn();
        var before = await _studio.Get("/ui/matui.css");
        var form = StudioHarness.AppForm("matmail", "matmail");
        form["Input.Name"] = "MatMail";
        form["Input.Tagline"] = "Mail gateway with a web mail client";
        form["Input.Accent"] = "#475569";
        form["Input.Glyph"] = "mail-m";
        form["Input.Port"] = "9933";
        var fields = form.AsEnumerable().Concat([new("overrides-present", "1"), new("property:--mat-tag-radius", "0")]).ToList();
        var response = await _studio.Post("/Studio/Apps/Edit?id=matmail", "/Studio/Apps/Edit", fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("[data-app=\"matmail\"] {\n  --mat-tag-radius: 0px;\n}", await _studio.Get("/ui/matui.css"));

        Assert.Equal(HttpStatusCode.Redirect, (await _studio.Post("/Studio/Apps/Edit?id=matmail", "/Studio/Apps/Edit?handler=Delete", StudioHarness.AppForm("matmail", "matmail"))).StatusCode);
        Assert.Equal(before, await _studio.Get("/ui/matui.css"));
    }

    [Fact]
    public async Task The_history_of_an_app_names_the_overrides_and_a_restore_brings_them_back_and_a_bad_revision_is_refused()
    {
        await _studio.SignIn();
        await SaveAppOk(new() { ["property:--mat-btn-radius"] = "0" });
        await SaveAppOk(new() { ["property:--mat-btn-radius"] = "4px", ["light:--mat-radius"] = "2px" }, "matx", "matx");

        var history = await _studio.Get("/Studio/History?kind=apps&id=matx");
        Assert.Contains("<code>properties.--mat-btn-radius</code>", history);
        Assert.Contains("<code>tokens.light.--mat-radius</code>", history);
        Assert.Equal("Changed light --mat-radius, property --mat-btn-radius", _studio.Store.Current("apps", "matx")!.Note);

        var restored = await _studio.Post("/Studio/Apps/Edit?id=matx", "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = "apps", ["id"] = "matx", ["revision"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, restored.StatusCode);
        Assert.Equal("0px", _studio.Design.App("matx")!.Properties!["--mat-btn-radius"]);
        Assert.Null(_studio.Design.App("matx")!.Tokens);

        // a revision with an override outside the limits: hand-edited, then refused by the restore and found by the checks
        var bad = _studio.Design.App("matx")! with { Properties = new Dictionary<string, string> { ["--mat-btn-height"] = "20px" } };
        _studio.Store.Save("apps", "matx", bad, "admin", "Edited by hand");
        _studio.Design.Reload();
        await SaveAppOk(new() { ["property:--mat-btn-radius"] = "2px" }, "matx", "matx");
        var refused = await _studio.Post("/Studio/Apps/Edit?id=matx", "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = "apps", ["id"] = "matx", ["revision"] = "4" });
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("cannot be restored as it is today", await refused.Content.ReadAsStringAsync());

        _studio.Store.Save("apps", "matx", bad, "admin", "Edited by hand");
        _studio.Design.Reload();
        var report = _studio.Checks.Run();
        Assert.Contains(report.Findings, f => f.Check == "apps" && f.Id == "matx" && f.Message.Contains("Property --mat-btn-height", StringComparison.Ordinal) && f.Message.Contains("MOBILE-04", StringComparison.Ordinal));
        Assert.False(_studio.Releases.Execute(Ver.First, string.Empty, "admin").Succeeded);
    }

    [Fact]
    public async Task An_app_on_the_volume_with_an_unsafe_override_is_left_out_and_the_checks_say_so()
    {
        await _studio.SignIn();
        var app = AppRegistry.Get("matmail")! with { Id = "matz", Name = "MatZ", Port = 9944, Properties = new Dictionary<string, string> { ["--mat-btn-radius"] = "0; } * { injected-marker: 1" } };
        _studio.Store.Save("apps", "matz", app, "admin", "Edited by hand");
        _studio.Design.Reload();

        Assert.Null(_studio.Design.App("matz"));
        Assert.DoesNotContain("injected-marker", await _studio.Get("/ui/matui.css"));
        Assert.Contains(_studio.Checks.Run().Findings, f => f.Check == "volume" && f.Id == "matz");
    }
}
