using System.IO.Compression;
using System.Net;
using System.Text.Json;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>COLOR-11 in the Studio: the brand page, the overlay in matui.css (light, and the dark set twice and identical), releases and the archive, history and restore, checks.</summary>
public sealed class StudioBrandTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    public StudioBrandTests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    private ToolkitFiles Toolkit => _studio.Factory.Services.GetRequiredService<ToolkitFiles>();

    private static List<KeyValuePair<string, string>> Form(Dictionary<string, string>? light = null, Dictionary<string, string>? dark = null, string company = "Acme", string shortName = "Acme", string tagline = "Anvils for everyone")
    {
        var fields = new List<KeyValuePair<string, string>> { new("Input.Company", company), new("Input.ShortName", shortName), new("Input.Tagline", tagline) };
        fields.AddRange((light ?? []).Select(p => new KeyValuePair<string, string>("light:" + p.Key, p.Value)));
        fields.AddRange((dark ?? []).Select(p => new KeyValuePair<string, string>("dark:" + p.Key, p.Value)));
        return fields;
    }

    private Task<HttpResponseMessage> Save(List<KeyValuePair<string, string>> form, string query = "") => _studio.Post("/Studio/Brand", "/Studio/Brand" + query, form);

    private async Task SaveOk(List<KeyValuePair<string, string>> form)
        => Assert.Equal(HttpStatusCode.Redirect, (await Save(form)).StatusCode);

    private static Dictionary<string, string> Radius(string value) => new() { ["--mat-radius"] = value };

    [Fact]
    public async Task A_visitor_is_led_to_the_sign_in()
    {
        Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync("/Studio/Brand")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync("/Studio/History?kind=brand&id=brand")).StatusCode);
    }

    [Fact]
    public async Task The_page_has_the_names_a_row_for_every_token_and_the_preview_outside_the_form()
    {
        await _studio.SignIn();
        var html = await _studio.Get("/Studio/Brand");

        Assert.Equal(1, html.Split("<h1").Length - 1);
        Assert.Contains("value=\"Mat\"", html);
        foreach (var token in Toolkit.EditableTokens)
        {
            Assert.Contains($"data-token=\"{token.Name}\"", html);
            Assert.Contains($"name=\"light:{token.Name}\"", html);
            Assert.Equal(token.HasDark, html.Contains($"name=\"dark:{token.Name}\"", StringComparison.Ordinal));
        }

        // a colour has a picker, a length has none; every group of the CSS that a brand may set is a heading
        Assert.Contains("id=\"light-mat-bg-picker\"", html);
        Assert.DoesNotContain("id=\"light-mat-radius-picker\"", html);
        foreach (var group in Toolkit.EditableTokens.Select(t => t.Group).Distinct())
        {
            Assert.Contains(">" + group + "</h3>", html);
        }

        Assert.DoesNotContain("--mat-accent-hover</code>", html.Split("Tokens that are not set here")[0]);
        Assert.Contains("data-brand-preview", html);
        Assert.Contains("/js/brand.js", html);
        // the preview is not inside the form: nothing in it can submit the editor
        var form = html[html.IndexOf("<form class=\"mat-form\"", StringComparison.Ordinal)..html.IndexOf("</form>", StringComparison.Ordinal)];
        Assert.DoesNotContain("data-brand-preview", form);
        Assert.Contains("data-pairings=\"", html);
        Assert.Contains("Reset to built-in", await Saved());
    }

    private async Task<string> Saved()
    {
        await SaveOk(Form(Radius("8px")));
        return await _studio.Get("/Studio/Brand");
    }

    [Fact]
    public async Task A_saved_override_reaches_the_bundle_the_part_the_archive_and_the_hash()
    {
        await _studio.SignIn();
        var before = await _studio.Get("/ui/matui.css");
        var hashBefore = Toolkit.Hash;
        Assert.DoesNotContain("Brand set in the Studio", before);

        await SaveOk(Form(
            new() { ["--mat-radius"] = "10px", ["--mat-bg"] = "#fafafa", ["--mat-font"] = "Georgia, serif" },
            new() { ["--mat-bg"] = "#101010", ["--mat-text"] = "#f0f0f0" }));

        var css = await _studio.Get("/ui/matui.css");
        Assert.Contains(":root {\n  --mat-bg: #fafafa;\n  --mat-radius: 10px;\n  --mat-font: Georgia, serif;\n}", css);
        Assert.Contains(":root[data-theme=\"dark\"] {\n  --mat-bg: #101010;\n  --mat-text: #f0f0f0;\n}", css);
        Assert.NotEqual(hashBefore, Toolkit.Hash);
        Assert.StartsWith($"/* MatUi {AppVersion.MatUi} ({Toolkit.Hash})", css);
        // the part, the manifest and the archive follow
        Assert.Contains("--mat-radius: 10px;", await _studio.Get("/ui/css/matui.apps.css"));
        using var zip = new ZipArchive(new MemoryStream(await _studio.Client.GetByteArrayAsync("/ui/matui.zip")));
        foreach (var name in new[] { "matui/matui.css", "matui/css/matui.apps.css" })
        {
            using var reader = new StreamReader(zip.GetEntry(name)!.Open());
            Assert.Contains("--mat-radius: 10px;", await reader.ReadToEndAsync());
        }

        // it is the brand of the site now
        Assert.Equal("Acme", _studio.Design.Brand.Company);
        Assert.Equal(ItemSource.Changed, _studio.Design.BrandSource);
        Assert.Equal("10px", _studio.Store.Current("brand", "brand")!.Data.GetProperty("light").GetProperty("--mat-radius").GetString());
    }

    [Fact]
    public async Task The_dark_set_is_written_twice_and_identical_like_in_the_tokens()
    {
        await _studio.SignIn();
        await SaveOk(Form(Radius("10px"), new() { ["--mat-bg"] = "#101010", ["--mat-text"] = "#f0f0f0", ["--mat-border"] = "rgba(255, 255, 255, 0.2)" }));

        var blocks = CssTokens.ParseBlocks(_studio.Factory.Services.GetRequiredService<ToolkitFiles>().Css("matui.apps.css")!);
        var explicitDark = blocks.Single(b => b.Selector == ":root[data-theme=\"dark\"]" && b.Media is null);
        var system = blocks.Single(b => b.Selector == ":root:not([data-theme=\"light\"]):not([data-theme=\"dark\"])" && b.Media!.Contains("prefers-color-scheme: dark", StringComparison.Ordinal));
        Assert.Equal(3, explicitDark.Declarations.Count);
        Assert.Equal(explicitDark.Declarations.Select(d => (d.Name, d.Value)), system.Declarations.Select(d => (d.Name, d.Value)));
        Assert.Equal("rgba(255, 255, 255, 0.2)", explicitDark.Declarations.Single(d => d.Name == "--mat-border").Value);
        Assert.Equal(10, ToolkitFiles.CssParts.Count);
    }

    [Fact]
    public void The_overlay_is_empty_without_a_brand_and_for_a_brand_that_sets_nothing()
    {
        var tokens = Toolkit.EditableTokens;
        Assert.Equal(string.Empty, ToolkitFiles.BrandOverlay(null, tokens));
        Assert.Equal(string.Empty, ToolkitFiles.BrandOverlay(Brand.BuiltIn, tokens));
        Assert.Equal(string.Empty, ToolkitFiles.BrandOverlay(Brand.BuiltIn with { Light = new Dictionary<string, string>(), Dark = new Dictionary<string, string>() }, tokens));
    }

    [Fact]
    public void The_overlay_leaves_out_what_is_not_safe_or_not_a_value_of_its_token()
    {
        var tokens = Toolkit.EditableTokens;
        Assert.Equal(string.Empty, ToolkitFiles.BrandOverlay(Brand.BuiltIn with { Light = new Dictionary<string, string> { ["--mat-bg"] = "red; } body { display: none" } }, tokens));

        // structurally safe but outside the limits, or a token a brand cannot set: not written
        var css = ToolkitFiles.BrandOverlay(Brand.BuiltIn with
        {
            Light = new Dictionary<string, string> { ["--mat-radius"] = "30px", ["--mat-accent"] = "#ff0000", ["--mat-bg"] = "#fafafa" },
            Dark = new Dictionary<string, string> { ["--mat-radius"] = "8px" },
        }, tokens);
        Assert.Contains("--mat-bg: #fafafa;", css);
        Assert.DoesNotContain("--mat-radius", css);
        Assert.DoesNotContain("--mat-accent", css);
        Assert.DoesNotContain("data-theme", css);
    }

    [Fact]
    public async Task A_brand_that_is_reset_leaves_the_files_as_they_were_before_it()
    {
        await _studio.SignIn();
        var before = await _studio.Get("/ui/matui.css");
        var apps = Toolkit.Css("matui.apps.css");
        Assert.Equal(TestPaths.ReadToolkit("css/matui.apps.css"), apps);

        await SaveOk(Form(Radius("10px")));
        Assert.NotEqual(before, await _studio.Get("/ui/matui.css"));

        Assert.Equal(HttpStatusCode.Redirect, (await _studio.Post("/Studio/Brand", "/Studio/Brand?handler=Reset", Form())).StatusCode);
        Assert.Null(_studio.Design.StudioBrand);
        Assert.Equal(before, await _studio.Get("/ui/matui.css"));
        Assert.Equal(apps, Toolkit.Css("matui.apps.css"));
        Assert.Equal("Mat", _studio.Design.Brand.Company);
    }

    [Fact]
    public async Task Check_shows_the_result_and_saves_nothing_and_a_valid_form_is_not_refused()
    {
        await _studio.SignIn();
        var response = await Save(Form(Radius("10px")), "?check=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("All checks pass.", await response.Content.ReadAsStringAsync());
        Assert.Null(_studio.Store.Current("brand", "brand"));
        Assert.DoesNotContain("--mat-radius: 10px", await _studio.Get("/ui/matui.css"));
    }

    [Theory]
    [InlineData("--mat-radius", "30px", "above the most")]
    [InlineData("--mat-control-h", "43px", "Touch targets")]
    [InlineData("--mat-fs-md", "12px", "Body text")]
    [InlineData("--mat-lh", "1", "line height")]
    [InlineData("--mat-bg", "blue; } body { injected-marker: 1", "does not belong")]
    [InlineData("--mat-bg", "notacolour", "A colour")]
    public async Task A_value_outside_its_type_or_limits_is_refused_with_the_reason_at_its_field_and_nothing_is_saved(string token, string value, string message)
    {
        await _studio.SignIn();
        var response = await Save(Form(new() { [token] = value }));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, System.Net.WebUtility.HtmlDecode(html), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("is-invalid", html);
        Assert.Contains("Please check the brand.", html);
        Assert.Null(_studio.Store.Current("brand", "brand"));
        Assert.DoesNotContain("injected-marker", await _studio.Get("/ui/matui.css"));
    }

    [Fact]
    public async Task A_name_that_is_no_token_a_brand_may_set_is_refused()
    {
        await _studio.SignIn();
        var response = await Save(Form(new() { ["--mat-accent"] = "#ff0000", ["--mat-z-dialog"] = "1" }));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("is not a token a brand can set", html);
        Assert.Null(_studio.Store.Current("brand", "brand"));
    }

    [Fact]
    public async Task A_failing_contrast_is_refused_with_the_numbers_and_stays_on_the_page()
    {
        await _studio.SignIn();
        var response = await Save(Form(new() { ["--mat-text"] = "#c8c8c8" }));
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Text on the page (light):", html);
        Assert.Contains(":1, needs 4.50:1", html);
        Assert.Contains("mat-pill--error\" data-status>Fails", html);
        Assert.Contains("value=\"#c8c8c8\"", html);
        Assert.Null(_studio.Store.Current("brand", "brand"));
        Assert.DoesNotContain("#c8c8c8", await _studio.Get("/ui/matui.css"));
    }

    [Fact]
    public async Task The_names_are_required()
    {
        await _studio.SignIn();
        var response = await Save(Form(company: " ", shortName: string.Empty));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Name the company", html);
        Assert.Contains("Give a short name", html);
        Assert.Null(_studio.Store.Current("brand", "brand"));
    }

    [Fact]
    public async Task The_history_lists_the_revisions_compares_them_and_restores_one_as_a_new_revision()
    {
        await _studio.SignIn();
        await SaveOk(Form(Radius("10px")));
        await SaveOk(Form(Radius("12px"), tagline: "A second tagline"));
        await SaveOk(Form(new() { ["--mat-radius"] = "14px", ["--mat-bg"] = "#fafafa" }));

        var history = await _studio.Get("/Studio/History?kind=brand&id=brand");
        Assert.Contains("History of Acme", history);
        Assert.Contains("<code>light.--mat-radius</code>", history);
        Assert.Contains("<code>light.--mat-bg</code>", history);
        Assert.Contains("Created", history);
        Assert.Contains("href=\"/Studio/Brand\"", history);
        Assert.Contains("href=\"/Studio/Brand\" aria-current=\"page\"", history);

        var restored = await _studio.Post("/Studio/Brand", "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = "brand", ["id"] = "brand", ["revision"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, restored.StatusCode);
        Assert.Equal(4, _studio.Store.History("brand", "brand").Count);
        Assert.Equal("10px", _studio.Design.Brand.Light["--mat-radius"]);
        Assert.Equal("Anvils for everyone", _studio.Design.Brand.Tagline);
        Assert.Contains("--mat-radius: 10px;", await _studio.Get("/ui/matui.css"));
        Assert.DoesNotContain("#fafafa", await _studio.Get("/ui/matui.css"));
        Assert.StartsWith("Restored", _studio.Store.Current("brand", "brand")!.Note);

        var activity = await _studio.Get("/Studio/Activity?kind=brand");
        Assert.Contains("href=\"/Studio/History?kind=brand&amp;id=brand\"", activity);
        Assert.Contains("Restored", activity);
    }

    [Fact]
    public async Task A_revision_that_is_not_accepted_today_is_not_restored_and_the_checks_find_it()
    {
        await _studio.SignIn();
        await SaveOk(Form(Radius("10px")));
        // hand-edited into something that is safe to write but outside the limits
        _studio.Store.Save("brand", "brand", Brand.BuiltIn with { Light = new Dictionary<string, string> { ["--mat-radius"] = "40px" }, Dark = new Dictionary<string, string>() }, "admin", "Edited by hand");
        _studio.Design.Reload();
        await SaveOk(Form(Radius("12px")));

        var report = _studio.Checks.Run();
        Assert.True(report.Passed, string.Join("\n", report.Findings.Select(f => f.Message)));

        var refused = await _studio.Post("/Studio/Brand", "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = "brand", ["id"] = "brand", ["revision"] = "2" });
        var html = await refused.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("cannot be restored as it is today", html);
        Assert.Contains("above the most", html);
        Assert.Equal(3, _studio.Store.History("brand", "brand").Count);

        // the bad revision is the current one: the checks find it, and the site does not write it
        _studio.Store.Save("brand", "brand", Brand.BuiltIn with { Light = new Dictionary<string, string> { ["--mat-radius"] = "40px" }, Dark = new Dictionary<string, string>() }, "admin", "Edited by hand");
        _studio.Design.Reload();
        var bad = _studio.Checks.Run();
        Assert.False(bad.Passed);
        Assert.Contains(bad.Findings, f => f.Check == "brand" && f.Severity == Severity.Error && f.Id == "brand" && f.Message.Contains("above the most", StringComparison.Ordinal));
        Assert.DoesNotContain("--mat-radius: 40px", await _studio.Get("/ui/matui.css"));
        Assert.Contains("href=\"/Studio/Brand\"", await _studio.Get("/Studio/Checks"));
    }

    [Fact]
    public async Task A_brand_on_the_volume_that_is_not_safe_is_left_out_and_the_checks_say_so()
    {
        await _studio.SignIn();
        _studio.Store.Save("brand", "brand", Brand.BuiltIn with { Light = new Dictionary<string, string> { ["--mat-bg"] = "red; } * { injected-marker: 1" } }, "admin", "Edited by hand");
        _studio.Design.Reload();

        Assert.Null(_studio.Design.StudioBrand);
        Assert.DoesNotContain("injected-marker", await _studio.Get("/ui/matui.css"));
        var report = _studio.Checks.Run();
        Assert.Contains(report.Findings, f => f.Check == "volume" && f.Id == "brand");
        Assert.True((await _studio.Client.GetAsync("/Studio/Brand")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_release_carries_the_brand_in_the_bundle_the_part_the_archive_the_design_data_and_the_changelog()
    {
        await _studio.SignIn();
        await SaveOk(Form(new() { ["--mat-radius"] = "10px" }, new() { ["--mat-bg"] = "#101010" }, tagline: "Anvils"));

        var run = _studio.Releases.Execute(Ver.First, "With a brand.", "admin");
        Assert.True(run.Succeeded, string.Join("\n", run.Stages.SelectMany(s => s.Log)));

        var css = await _studio.Get($"/releases/{Ver.First}/matui.css");
        Assert.StartsWith($"/* MatUi {Ver.First} (", css);
        Assert.Contains(":root {\n  --mat-radius: 10px;\n}", css);
        Assert.Contains(":root[data-theme=\"dark\"] {\n  --mat-bg: #101010;\n}", css);
        Assert.Contains("--mat-radius: 10px;", await _studio.Get($"/releases/{Ver.First}/css/matui.apps.css"));
        using var zip = new ZipArchive(new MemoryStream(await _studio.Client.GetByteArrayAsync($"/releases/{Ver.First}/matui-{Ver.First}.zip")));
        foreach (var name in new[] { "matui/matui.css", "matui/css/matui.apps.css" })
        {
            using var reader = new StreamReader(zip.GetEntry(name)!.Open());
            Assert.Contains("--mat-radius: 10px;", await reader.ReadToEndAsync());
        }

        var design = JsonDocument.Parse(await _studio.Get($"/releases/{Ver.First}/design.json")).RootElement.GetProperty("brand");
        Assert.Equal("Acme", design.GetProperty("company").GetString());
        Assert.Equal("Anvils", design.GetProperty("tagline").GetString());
        Assert.Equal("10px", design.GetProperty("light").GetProperty("--mat-radius").GetString());
        Assert.Equal("#101010", design.GetProperty("dark").GetProperty("--mat-bg").GetString());

        // the changelog of the next release names the brand
        Assert.Contains(_studio.Releases.Latest!.Changelog, line => line.StartsWith("Brand brand:", StringComparison.Ordinal));
        await SaveOk(Form(new() { ["--mat-radius"] = "12px" }, tagline: "Anvils"));
        Assert.Contains(_studio.Releases.PendingChanges(), line => line.StartsWith("Brand brand: changed", StringComparison.Ordinal) && line.Contains("light --mat-radius", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_release_is_refused_while_the_brand_fails_a_check()
    {
        await _studio.SignIn();
        _studio.Store.Save("brand", "brand", Brand.BuiltIn with { Light = new Dictionary<string, string> { ["--mat-text"] = "#cccccc" }, Dark = new Dictionary<string, string>() }, "admin", "Edited by hand");
        _studio.Design.Reload();

        var run = _studio.Releases.Execute(Ver.First, string.Empty, "admin");
        Assert.False(run.Succeeded);
        Assert.Equal(StageStatus.Failed, run.Stages[0].Status);
        Assert.Contains(run.Stages[0].Log, l => l.Contains("Text on the page (light)", StringComparison.Ordinal));
        Assert.Empty(_studio.Releases.Releases());
    }

    [Fact]
    public async Task The_brand_is_in_the_menu_and_the_overview()
    {
        await _studio.SignIn();
        Assert.Contains("Brand", Navigation.StudioSet.Pages.Select(i => i.Title));
        var overview = await _studio.Get("/Studio");
        Assert.Contains("href=\"/Studio/Brand\"", overview);
        Assert.Contains("<a class=\"mat-nav__item is-active\" href=\"/Studio/Brand\" aria-current=\"page\">", await _studio.Get("/Studio/Brand"));
        await SaveOk(Form(Radius("10px")));
        Assert.Contains("brand: 1 item, 1 revision", await _studio.Get("/Studio"));
    }
}
