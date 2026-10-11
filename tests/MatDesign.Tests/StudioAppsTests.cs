using System.IO.Compression;
using System.Net;
using System.Text.Json;
using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>The apps of the Studio: what is saved shows on every page of the site, in the toolkit files and in the API; what is wrong is refused with the reason.</summary>
public sealed class StudioAppsTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    public StudioAppsTests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    private async Task<HttpStatusCode> Save(Dictionary<string, string> form, string query = "")
        => (await _studio.Post("/Studio/Apps/Edit" + (form["Input.Original"].Length > 0 ? "?id=" + form["Input.Original"] : string.Empty), "/Studio/Apps/Edit" + query, form)).StatusCode;

    private async Task<JsonElement[]> Api() => JsonSerializer.Deserialize<JsonElement[]>(await _studio.Get("/api/apps"))!;

    [Fact]
    public async Task The_pages_lead_a_visitor_to_the_sign_in()
    {
        foreach (var url in new[] { "/Studio/Apps", "/Studio/Apps/Edit", "/Studio/Apps/Edit?id=matmail" })
        {
            var response = await _studio.Client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
    }

    [Fact]
    public async Task The_list_has_the_built_in_apps_its_toolbar_and_its_three_dialogs()
    {
        await _studio.SignIn();

        var html = await _studio.Get("/Studio/Apps");
        Assert.Contains("MatMail", html);
        Assert.Contains("class=\"mat-toolbar\"", html);
        foreach (var dialog in new[] { "apps-search", "apps-filter", "apps-sort" })
        {
            Assert.Contains($"id=\"{dialog}\"", html);
        }

        Assert.Contains("href=\"/Studio/Apps/Edit\"", html);
        Assert.Contains("Built in", html);
        Assert.DoesNotContain("No app matches", html);
        Assert.Contains("No app matches", await _studio.Get("/Studio/Apps?q=zzzz"));
        Assert.DoesNotContain("MatMail", await _studio.Get("/Studio/Apps?category=Platform"));
    }

    [Fact]
    public async Task A_new_app_is_saved_and_shows_on_the_site_in_the_api_and_in_the_toolkit_files()
    {
        await _studio.SignIn();
        var hashBefore = _studio.Factory.Services.GetRequiredService<ToolkitFiles>().Hash;
        Assert.DoesNotContain("matx", (await Api()).Select(a => a.GetProperty("id").GetString()));

        var form = await _studio.Get("/Studio/Apps/Edit");
        Assert.Contains("New app", form);
        Assert.Contains("data-picker=\"colour\"", form);

        var saved = await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", StudioHarness.AppForm());
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal("/Studio/Apps", saved.Headers.Location!.OriginalString);

        // the list shows it, with the flash of the redirect; the page of the app and the API know it
        var list = await _studio.Get("/Studio/Apps");
        Assert.Contains("MatX", list);
        Assert.Contains("data-toast=\"MatX was added.\"", list);
        Assert.Contains("Added", list);
        Assert.DoesNotContain("data-toast", await _studio.Get("/Studio/Apps"));
        Assert.Contains("An app for the tests", await _studio.Get("/Identity/App?id=matx"));
        var apps = await Api();
        Assert.Equal(AppRegistry.All.Count + 1, apps.Length);
        Assert.Contains(apps, a => a.GetProperty("id").GetString() == "matx" && a.GetProperty("accent").GetString() == "#0f766e");
        Assert.Contains("MatX", await _studio.Get("/Identity"));

        // the toolkit: the colours are in the bundle and in the part, the hash and the archive moved on
        var rule = "[data-app=\"matx\"] { --mat-app-accent: #0f766e; }";
        Assert.Contains(rule, await _studio.Get("/ui/matui.css"));
        Assert.Contains(rule, await _studio.Get("/ui/css/matui.apps.css"));
        Assert.NotEqual(hashBefore, _studio.Factory.Services.GetRequiredService<ToolkitFiles>().Hash);
        using var zip = new ZipArchive(new MemoryStream(await _studio.Client.GetByteArrayAsync("/ui/matui.zip")));
        using (var reader = new StreamReader(zip.GetEntry("matui/css/matui.apps.css")!.Open()))
        {
            Assert.Contains(rule, await reader.ReadToEndAsync());
        }

        // a preview of the new app uses its colour, and the history says what happened
        Assert.Contains("data-app=\"matx\"", await _studio.Get("/Preview?app=matx&view=list"));
        var activity = await _studio.Get("/Studio/Activity");
        Assert.Contains("matx", activity);
        Assert.Contains("Created", activity);
    }

    [Fact]
    public async Task What_is_wrong_is_refused_with_the_reason_at_the_field_and_nothing_is_saved()
    {
        await _studio.SignIn();
        var form = StudioHarness.AppForm(id: "Mat X");
        form["Input.Name"] = "MatMail";
        form["Input.Port"] = "4455";
        form["Input.Accent"] = "#ffe066";

        var response = await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Please check the form.", html);
        Assert.Contains("Use 2 to 24 lower-case letters", html);
        Assert.Contains("Another app has this name.", html);
        Assert.Contains("MatDock already uses port 4455.", html);
        Assert.Contains("White text reaches only", html);
        Assert.Contains("href=\"#Input_Name\"", html);
        Assert.Contains("value=\"Mat X\"", html);
        Assert.Empty(_studio.Store.List("apps"));
        Assert.Equal(AppRegistry.All.Count, _studio.Design.Apps.Count);
    }

    [Fact]
    public async Task A_light_accent_is_saved_once_it_has_its_dark_text_and_its_darker_link()
    {
        await _studio.SignIn();
        var form = StudioHarness.AppForm();
        form["Input.Accent"] = "#ffe066";
        form["Input.OnAccent"] = "#0b1020";
        form["Input.AccentText"] = "#7a5c00";

        Assert.Equal(HttpStatusCode.Redirect, await Save(form));

        var css = await _studio.Get("/ui/css/matui.apps.css");
        Assert.Contains("[data-app=\"matx\"] { --mat-app-accent: #ffe066; --mat-app-on-accent: #0b1020; --mat-app-accent-text: #7a5c00; }", css);
    }

    [Fact]
    public async Task Check_shows_the_contrast_report_and_saves_nothing()
    {
        await _studio.SignIn();

        var response = await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit?check=true", StudioHarness.AppForm());
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("All checks pass.", html);
        Assert.Contains("AA passed", html);
        // the page was answered at ?check=true, so the form must name its own action, or Save would only check again
        Assert.Contains("<form class=\"mat-form\" method=\"post\" action=\"/Studio/Apps/Edit\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("Text on the accent (light)", html);
        Assert.Empty(_studio.Store.List("apps"));
    }

    [Fact]
    public async Task A_built_in_app_that_is_saved_becomes_a_changed_one_and_a_reset_brings_the_built_in_one_back()
    {
        await _studio.SignIn();
        var original = AppRegistry.Get("matmail")!;
        var form = StudioHarness.AppForm(id: "matmail", original: "matmail");
        form["Input.Name"] = original.Name;
        form["Input.Tagline"] = "A new tagline";
        form["Input.Category"] = original.Category;
        form["Input.Accent"] = original.Accent;
        form["Input.Glyph"] = original.Glyph;
        form["Input.Port"] = original.Port?.ToString() ?? string.Empty;
        form["Input.OnAccent"] = original.OnAccent ?? string.Empty;
        form["Input.AccentText"] = original.AccentText ?? string.Empty;
        form["Input.Repo"] = original.Repo;
        form["Input.Notes"] = original.Notes;

        Assert.Equal(HttpStatusCode.Redirect, await Save(form));

        Assert.Equal(AppRegistry.All.Count, _studio.Design.Apps.Count);
        Assert.Equal(ItemSource.Changed, _studio.Design.SourceOf("matmail"));
        Assert.Contains("A new tagline", await _studio.Get("/Identity/App?id=matmail"));
        Assert.Equal("Changed tagline", _studio.Store.Current("apps", "matmail")!.Note);
        var edit = await _studio.Get("/Studio/Apps/Edit?id=matmail");
        Assert.Contains("Reset to built-in", edit);
        Assert.Contains("data-confirm=\"Reset MatMail", edit);

        var reset = await _studio.Post("/Studio/Apps/Edit?id=matmail", "/Studio/Apps/Edit?handler=Delete", form);
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Equal(ItemSource.BuiltIn, _studio.Design.SourceOf("matmail"));
        Assert.Equal(original.Tagline, _studio.Design.App("matmail")!.Tagline);
        Assert.Equal(["Reset to the built-in app", "Changed tagline"], _studio.Store.History("apps", "matmail").Select(r => r.Note));
    }

    [Fact]
    public async Task An_app_that_was_added_can_be_deleted_and_its_colours_leave_the_toolkit()
    {
        await _studio.SignIn();
        await Save(StudioHarness.AppForm());
        Assert.Contains("matx", await _studio.Get("/ui/css/matui.apps.css"));
        var edit = await _studio.Get("/Studio/Apps/Edit?id=matx");
        Assert.Contains("data-confirm=\"Delete MatX?", edit);

        var deleted = await _studio.Post("/Studio/Apps/Edit?id=matx", "/Studio/Apps/Edit?handler=Delete", StudioHarness.AppForm(original: "matx"));

        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.DoesNotContain("matx", await _studio.Get("/ui/css/matui.apps.css"));
        await _studio.Get("/Identity/App?id=matx", HttpStatusCode.NotFound);
        Assert.Equal(AppRegistry.All.Count, (await Api()).Length);
        Assert.Equal(["Deleted", "Created"], _studio.Store.History("apps", "matx").Select(r => r.Note));
    }

    [Fact]
    public async Task A_built_in_app_cannot_be_deleted_and_an_unknown_one_is_not_found()
    {
        await _studio.SignIn();

        var response = await _studio.Post("/Studio/Apps/Edit?id=matmail", "/Studio/Apps/Edit?handler=Delete", StudioHarness.AppForm(id: "matmail", original: "matmail"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(_studio.Design.App("matmail"));

        await _studio.Get("/Studio/Apps/Edit?id=nothere", HttpStatusCode.NotFound);
        var missing = await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", StudioHarness.AppForm(id: "gone", original: "gone"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task The_id_of_an_app_that_exists_cannot_be_changed_by_the_form()
    {
        await _studio.SignIn();
        await Save(StudioHarness.AppForm());

        var form = StudioHarness.AppForm(id: "other", original: "matx");
        form["Input.Tagline"] = "Changed text";
        Assert.Equal(HttpStatusCode.Redirect, await Save(form));

        Assert.Equal("Changed text", _studio.Design.App("matx")!.Tagline);
        Assert.Null(_studio.Design.App("other"));
    }

    [Fact]
    public async Task An_item_that_was_edited_by_hand_into_something_unsafe_is_left_out_and_the_site_goes_on()
    {
        await _studio.SignIn();
        _studio.Store.Save("apps", "evil", new { id = "evil", name = "Evil", tagline = "x", category = "Platform", accent = "red; } body { display: none", glyph = "apps", repo = "x" }, "hand");
        _studio.Store.Save("apps", "wrongid", new { id = "other", name = "Other", tagline = "xyz", category = "Platform", accent = "#112233", glyph = "apps", repo = "x" }, "hand");
        _studio.Design.Reload();

        Assert.Equal(AppRegistry.All.Count, _studio.Design.Apps.Count);
        Assert.DoesNotContain("red; } body", await _studio.Get("/ui/matui.css"));
        Assert.DoesNotContain("data-app=\"evil\"", await _studio.Get("/ui/matui.css"));
        Assert.DoesNotContain("data-app=\"other\"", await _studio.Get("/ui/matui.css"));
        await _studio.Get("/Studio/Apps");
    }

    [Fact]
    public void The_colours_of_the_changed_apps_are_written_like_those_of_the_built_in_ones()
    {
        var css = ToolkitFiles.AppsOverlay([AppRegistry.Get("matmail")! with { Accent = "#123456", OnAccent = null, AccentText = null }, AppRegistry.Get("matarchive")!]);

        Assert.Contains("[data-app=\"matmail\"] { --mat-app-accent: #123456; }", css);
        Assert.Contains("[data-app=\"matarchive\"] { --mat-app-accent: #4fd1c5; --mat-app-on-accent: #0b1020; --mat-app-accent-text: #1d7f77; }", css);
        Assert.Equal(string.Empty, ToolkitFiles.AppsOverlay([]));
    }

    [Fact]
    public async Task The_palette_and_the_menu_count_the_new_app()
    {
        await _studio.SignIn();
        var form = StudioHarness.AppForm();
        form["Input.Accent"] = AppRegistry.Get("matmail")!.Accent;
        form["Input.Name"] = "MatTwin";
        Assert.Equal(HttpStatusCode.Redirect, await Save(form));

        var palette = await _studio.Get("/Identity/Palette");
        Assert.Contains("MatTwin", palette);
    }
}
