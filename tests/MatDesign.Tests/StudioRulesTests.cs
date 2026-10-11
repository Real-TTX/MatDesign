using System.Net;
using System.Text.Json;
using MatDesign.Core;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>The rules of the Studio: what is saved is in the rulebook, on the rule pages and in the API, in the place of its area; what is wrong is refused with the reason.</summary>
public sealed class StudioRulesTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    public StudioRulesTests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    private static Dictionary<string, string> RuleForm(string id = "FORM-90", string original = "") => new()
    {
        ["Input.Original"] = original,
        ["Input.Id"] = id,
        ["Input.Area"] = "FORM",
        ["Input.Title"] = "A rule for the tests",
        ["Input.Statement"] = "Every test rule is deleted again when the test ends, and it is written with `mat-btn`.",
        ["Input.Level"] = "Should",
        ["Input.Source"] = "Studio",
        ["Input.Why"] = "",
        ["Input.Do"] = "",
        ["Input.Dont"] = "",
    };

    private async Task<HttpStatusCode> Save(Dictionary<string, string> form, string query = "")
        => (await _studio.Post("/Studio/Rules/Edit" + (form["Input.Original"].Length > 0 ? "?id=" + form["Input.Original"] : string.Empty), "/Studio/Rules/Edit" + query, form)).StatusCode;

    [Fact]
    public async Task The_pages_lead_a_visitor_to_the_sign_in()
    {
        foreach (var url in new[] { "/Studio/Rules", "/Studio/Rules/Edit", "/Studio/Rules/Edit?id=SHELL-01" })
        {
            Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync(url)).StatusCode);
        }
    }

    [Fact]
    public async Task The_list_has_the_rules_of_the_book_its_toolbar_and_its_three_dialogs()
    {
        await _studio.SignIn();

        var html = await _studio.Get("/Studio/Rules");
        Assert.Contains("SHELL-01", html);
        Assert.Contains("class=\"mat-toolbar\"", html);
        foreach (var dialog in new[] { "rules-search", "rules-filter", "rules-sort" })
        {
            Assert.Contains($"id=\"{dialog}\"", html);
        }

        Assert.Contains("href=\"/Studio/Rules/Edit\"", html);
        Assert.Contains("No rule matches", await _studio.Get("/Studio/Rules?q=zzzzz"));
        var shell = await _studio.Get("/Studio/Rules?area=SHELL");
        Assert.Contains("SHELL-01", shell);
        Assert.DoesNotContain("LIST-01", shell);
    }

    [Fact]
    public async Task A_new_rule_starts_with_the_next_free_id_of_its_area()
    {
        await _studio.SignIn();

        var html = await _studio.Get("/Studio/Rules/Edit?area=FORM");
        Assert.Contains("New rule", html);
        Assert.Contains($"value=\"{NextFormId()}\"", html);
        Assert.Contains("name=\"Input.Level\"", html);
    }

    private static string NextFormId() => MatDesign.Core.Studio.RuleValidator.NextId("FORM", RuleBook.All);

    [Fact]
    public async Task A_new_rule_is_saved_and_is_in_the_rulebook_in_its_area_on_its_page_and_in_the_api()
    {
        await _studio.SignIn();
        Assert.Null(_studio.Design.Rule("FORM-90"));

        var saved = await _studio.Post("/Studio/Rules/Edit?area=FORM", "/Studio/Rules/Edit", RuleForm());
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal("/Studio/Rules", saved.Headers.Location!.OriginalString);

        var list = await _studio.Get("/Studio/Rules?q=FORM-90");
        Assert.Contains("A rule for the tests", list);
        Assert.Contains("data-toast=\"FORM-90 was added.\"", list);
        Assert.Contains("Added", list);

        // the rulebook, the page of the rule (with the code span of the statement), the count and the API
        Assert.Contains("A rule for the tests", await _studio.Get("/Rules?q=FORM-90"));
        var page = await _studio.Get("/Rules/Detail?id=FORM-90");
        Assert.Contains("<code>mat-btn</code>", page);
        Assert.Contains($"{RuleBook.All.Count + 1} rules", await _studio.Get("/"));
        var api = JsonSerializer.Deserialize<JsonElement[]>(await _studio.Get("/api/rules"))!;
        Assert.Equal(RuleBook.All.Count + 1, api.Length);
        Assert.Contains(api, r => r.GetProperty("id").GetString() == "FORM-90" && r.GetProperty("level").GetString() == "Should" && r.GetProperty("check").GetString() == "Review");

        // in the book it stands after the last rule of its area, before the first rule of the next
        var rules = _studio.Design.Rules.Select(r => r.Id).ToList();
        var lastBuiltInForm = RuleBook.In("FORM").Last().Id;
        Assert.Equal(rules.IndexOf(lastBuiltInForm) + 1, rules.IndexOf("FORM-90"));
        Assert.Equal(ItemSource.Added, _studio.Design.RuleSourceOf("FORM-90"));

        // the next and previous links of its neighbours know it
        Assert.Contains("FORM-90", await _studio.Get($"/Rules/Detail?id={lastBuiltInForm}"));
        Assert.Contains("Created", await _studio.Get("/Studio/Activity"));
    }

    [Fact]
    public async Task What_is_wrong_is_refused_with_the_reason_at_the_field_and_nothing_is_saved()
    {
        await _studio.SignIn();
        var form = RuleForm(id: "LIST-90");
        form["Input.Title"] = "ab";
        form["Input.Statement"] = "short";
        form["Input.Level"] = "Always";

        var response = await _studio.Post("/Studio/Rules/Edit", "/Studio/Rules/Edit", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Please check the form.", html);
        Assert.Contains("starts with FORM-", html);
        Assert.Contains("Say in 3 to 80 characters", html);
        Assert.Contains("State the rule in 10 to 1200 characters.", html);
        Assert.Contains("Choose Must, Should or May.", html);
        Assert.Contains("href=\"#Input_Title\"", html);
        Assert.Contains("value=\"LIST-90\"", html);
        Assert.Empty(_studio.Store.List("rules"));

        var duplicate = RuleForm(id: "SHELL-01");
        duplicate["Input.Area"] = "SHELL";
        Assert.Contains("A rule with this id exists.", await (await _studio.Post("/Studio/Rules/Edit", "/Studio/Rules/Edit", duplicate)).Content.ReadAsStringAsync());
        Assert.Empty(_studio.Store.List("rules"));
    }

    [Fact]
    public async Task Check_shows_how_the_rule_reads_and_saves_nothing()
    {
        await _studio.SignIn();

        var response = await _studio.Post("/Studio/Rules/Edit", "/Studio/Rules/Edit?check=true", RuleForm());
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("All checks pass.", html);
        Assert.Contains("How it reads", html);
        Assert.Contains("<code>mat-btn</code>", html);
        Assert.Contains("<form class=\"mat-form\" method=\"post\" action=\"/Studio/Rules/Edit\"", html);
        Assert.Empty(_studio.Store.List("rules"));
    }

    [Fact]
    public async Task A_built_in_rule_that_is_saved_becomes_a_changed_one_and_a_reset_brings_the_built_in_one_back()
    {
        await _studio.SignIn();
        var original = RuleBook.Get("SHELL-09")!;
        var form = RuleForm(id: "SHELL-09", original: "SHELL-09");
        form["Input.Area"] = original.Area;
        form["Input.Title"] = "A new title";
        form["Input.Statement"] = original.Statement;
        form["Input.Level"] = original.Level.ToString();
        form["Input.Source"] = original.Source;
        form["Input.Why"] = original.Why ?? string.Empty;
        form["Input.Do"] = original.Do ?? string.Empty;
        form["Input.Dont"] = original.Dont ?? string.Empty;

        Assert.Equal(HttpStatusCode.Redirect, await Save(form));

        Assert.Equal(RuleBook.All.Count, _studio.Design.Rules.Count);
        Assert.Equal(ItemSource.Changed, _studio.Design.RuleSourceOf("SHELL-09"));
        Assert.Contains("A new title", await _studio.Get("/Rules/Detail?id=SHELL-09"));
        Assert.Equal("Changed title", _studio.Store.Current("rules", "SHELL-09")!.Note);
        Assert.Equal(original.Check, _studio.Design.Rule("SHELL-09")!.Check);
        var edit = await _studio.Get("/Studio/Rules/Edit?id=SHELL-09");
        Assert.Contains("Reset to built-in", edit);
        Assert.Contains("data-confirm=\"Reset SHELL-09", edit);

        var reset = await _studio.Post("/Studio/Rules/Edit?id=SHELL-09", "/Studio/Rules/Edit?handler=Delete", form);
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Equal(ItemSource.BuiltIn, _studio.Design.RuleSourceOf("SHELL-09"));
        Assert.Equal(original.Title, _studio.Design.Rule("SHELL-09")!.Title);
        Assert.Equal(["Reset to the built-in rule", "Changed title"], _studio.Store.History("rules", "SHELL-09").Select(r => r.Note));
    }

    [Fact]
    public async Task A_rule_that_was_added_can_be_deleted_and_leaves_the_rulebook()
    {
        await _studio.SignIn();
        await Save(RuleForm());
        var edit = await _studio.Get("/Studio/Rules/Edit?id=FORM-90");
        Assert.Contains("data-confirm=\"Delete FORM-90?", edit);
        Assert.Contains("readonly", edit);

        var deleted = await _studio.Post("/Studio/Rules/Edit?id=FORM-90", "/Studio/Rules/Edit?handler=Delete", RuleForm(original: "FORM-90"));

        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        await _studio.Get("/Rules/Detail?id=FORM-90", HttpStatusCode.NotFound);
        Assert.Equal(RuleBook.All.Count, _studio.Design.Rules.Count);
        Assert.Equal(["Deleted", "Created"], _studio.Store.History("rules", "FORM-90").Select(r => r.Note));
    }

    [Fact]
    public async Task A_built_in_rule_cannot_be_deleted_and_an_unknown_one_is_not_found()
    {
        await _studio.SignIn();

        var response = await _studio.Post("/Studio/Rules/Edit?id=SHELL-01", "/Studio/Rules/Edit?handler=Delete", RuleForm(id: "SHELL-01", original: "SHELL-01"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(_studio.Design.Rule("SHELL-01"));

        await _studio.Get("/Studio/Rules/Edit?id=NOPE-01", HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, (await _studio.Post("/Studio/Rules/Edit", "/Studio/Rules/Edit", RuleForm(id: "GONE-01", original: "GONE-01"))).StatusCode);
    }

    [Fact]
    public async Task The_id_and_the_area_of_a_rule_that_exists_cannot_be_changed_by_the_form()
    {
        await _studio.SignIn();
        await Save(RuleForm());

        var form = RuleForm(id: "LIST-77", original: "FORM-90");
        form["Input.Area"] = "LIST";
        form["Input.Title"] = "Changed title";
        Assert.Equal(HttpStatusCode.Redirect, await Save(form));

        Assert.Equal("Changed title", _studio.Design.Rule("FORM-90")!.Title);
        Assert.Equal("FORM", _studio.Design.Rule("FORM-90")!.Area);
        Assert.Null(_studio.Design.Rule("LIST-77"));
    }

    [Fact]
    public async Task The_file_of_a_rule_can_be_read_by_a_person_and_a_rule_edited_into_nonsense_is_left_out()
    {
        await _studio.SignIn();
        await Save(RuleForm());
        Assert.Contains("\"level\": \"Should\"", JsonSerializer.Serialize(_studio.Store.Current("rules", "FORM-90")!.Data, new JsonSerializerOptions { WriteIndented = true }));

        _studio.Store.Save("rules", "NOPE-01", new { id = "NOPE-01", area = "NOPE", title = "x y z", statement = "A statement that is long enough.", level = "Must", source = "hand", check = "Review" }, "hand");
        _studio.Store.Save("rules", "FORM-91", new { id = "FORM-91", area = "FORM", title = "x y z", statement = "A statement that is long enough.", level = "Always", source = "hand", check = "Review" }, "hand");
        _studio.Store.Save("rules", "FORM-92", new { id = "FORM-93", area = "FORM", title = "x y z", statement = "A statement that is long enough.", level = "Must", source = "hand", check = "Review" }, "hand");
        _studio.Design.Reload();

        Assert.Equal(RuleBook.All.Count + 1, _studio.Design.Rules.Count);
        Assert.Null(_studio.Design.Rule("NOPE-01"));
        Assert.Null(_studio.Design.Rule("FORM-91"));
        Assert.Null(_studio.Design.Rule("FORM-93"));
        await _studio.Get("/Studio/Rules");
        await _studio.Get("/Rules");
    }
}
