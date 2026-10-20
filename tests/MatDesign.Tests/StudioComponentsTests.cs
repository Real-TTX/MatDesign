using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>The components of the Studio: what is saved is on its page of the documentation, in its group and its index, live with its markup; what breaks the contract is refused with the reason.</summary>
public sealed partial class StudioComponentsTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    public StudioComponentsTests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    [GeneratedRegex(@"\sid=""([^""]+)""")]
    private static partial Regex IdAttribute();

    [GeneratedRegex(@"\shref=""#([^""]+)""")]
    private static partial Regex FragmentLink();

    private const string Pill = """<span class="mat-pill mat-pill--ok">Online</span>""";

    /// <summary>A valid form with one example; the checkbox sends its true and the hidden field its false, as a browser does.</summary>
    private static List<KeyValuePair<string, string>> Form(string id = "qa-sample", string original = "", string html = Pill, bool live = true, string group = "Content")
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("Input.Original", original),
            new("Input.Id", id),
            new("Input.Name", "QA sample"),
            new("Input.Group", group),
            new("Input.Icon", "toggle"),
            new("Input.Summary", "A component for the tests, with one example."),
            new("Input.Rules", "BUTTON-01, SHELL-01"),
            new("Input.Behaviour", "One line.\nAnother line with `mat-pill`."),
            new("Input.Examples[0].Js", ""),
            new("Input.Examples[0].Demo", ""),
            new("Input.Examples[0].Frame", ""),
            new("Input.Examples[0].Title", "Pills"),
            new("Input.Examples[0].Html", html),
            new("Input.Examples[0].Note", "A note."),
        };
        if (live)
        {
            fields.Add(new("Input.Examples[0].Live", "true"));
        }

        fields.Add(new("Input.Examples[0].Live", "false"));
        return fields;
    }

    private static List<KeyValuePair<string, string>> With(List<KeyValuePair<string, string>> form, string name, string value)
    {
        form.RemoveAll(f => f.Key == name);
        form.Add(new(name, value));
        return form;
    }

    private async Task<HttpResponseMessage> Save(List<KeyValuePair<string, string>> form, string query = "")
        => await _studio.Post("/Studio/Components/Edit" + (form.First(f => f.Key == "Input.Original").Value is { Length: > 0 } o ? "?id=" + o : string.Empty), "/Studio/Components/Edit" + query, form);

    private static async Task<string> Body(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    [Fact]
    public async Task The_pages_lead_a_visitor_to_the_sign_in()
    {
        foreach (var url in new[] { "/Studio/Components", "/Studio/Components/Edit", "/Studio/Components/Edit?id=buttons" })
        {
            Assert.Equal(HttpStatusCode.Redirect, (await _studio.Client.GetAsync(url)).StatusCode);
        }
    }

    [Fact]
    public async Task The_list_has_the_built_in_components_its_toolbar_and_its_three_dialogs()
    {
        await _studio.SignIn();

        var html = await _studio.Get("/Studio/Components");
        Assert.Contains("Buttons", html);
        Assert.Contains("class=\"mat-toolbar\"", html);
        foreach (var dialog in new[] { "components-search", "components-filter", "components-sort" })
        {
            Assert.Contains($"id=\"{dialog}\"", html);
        }

        Assert.Contains("href=\"/Studio/Components/Edit\"", html);
        Assert.Contains("href=\"/Components/buttons\"", html);
        Assert.Contains("No component matches", await _studio.Get("/Studio/Components?q=zzzzz"));
        var layout = await _studio.Get("/Studio/Components?group=Layout");
        Assert.Contains("Breakpoints", layout);
        Assert.DoesNotContain(">Buttons<", layout);
    }

    [Fact]
    public async Task The_form_shows_one_example_and_a_preview_that_waits_for_markup()
    {
        await _studio.SignIn();

        var html = await _studio.Get("/Studio/Components/Edit");
        Assert.Contains("New component", html);
        Assert.Contains("name=\"Input.Examples[0].Html\"", html);
        Assert.DoesNotContain("name=\"Input.Examples[1].Html\"", html);
        Assert.Contains("The examples appear here", html);
        Assert.Contains("<form class=\"mat-form\" method=\"post\" action=\"/Studio/Components/Edit\"", html);
    }

    [Fact]
    public async Task A_new_component_is_saved_and_is_on_its_page_in_its_group_in_the_index_and_live()
    {
        await _studio.SignIn();
        var before = (await _studio.Get("/Components/All")).Split("class=\"doc-component mat-anchor\"").Length - 1;

        var saved = await Save(Form());
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal("/Studio/Components", saved.Headers.Location!.OriginalString);

        var list = await _studio.Get("/Studio/Components?q=qa-sample");
        Assert.Contains("QA sample", list);
        Assert.Contains("data-toast=\"QA sample was added.\"", list);

        var page = await _studio.Get("/Components/All");
        Assert.Equal(before + 1, page.Split("class=\"doc-component mat-anchor\"").Length - 1);
        Assert.Contains("<section class=\"doc-component mat-anchor\" id=\"qa-sample\"", page);
        Assert.Contains(Pill, page);
        Assert.Contains("BUTTON-01", page);
        Assert.Contains("<code>mat-pill</code>", page);
        // in the index beside the content and in the dialog, and after the last component of its group
        Assert.True(Regex.Matches(page, "mat-toc__link( is-active)?\" href=\"#qa-sample\"").Count >= 2);
        var components = _studio.Design.ComponentsOf("Content").Select(c => c.Id).ToList();
        Assert.Equal("qa-sample", components[^1]);
        Assert.Equal(ItemSource.Added, _studio.Design.ComponentSourceOf("qa-sample"));
        Assert.Contains($">{ComponentCatalog.In(ComponentCatalog.Sections[0]).Count() + 1}<", page);

        // and as a tile of the overview and a page of its own (LIST-18), live, with the rules it names and between its neighbours
        var overview = await _studio.Get("/Components");
        Assert.Contains("data-component=\"qa-sample\"", overview);
        Assert.Contains("href=\"/Components/qa-sample\"", overview);
        var own = await _studio.Get("/Components/qa-sample");
        Assert.Contains("<h1>QA sample</h1>", own);
        Assert.Contains(Pill, own);
        Assert.Contains("href=\"/Rules/Detail?id=BUTTON-01\"", own);
        Assert.Contains("rel=\"prev\"", own);
        // the last component of the last group has no Next
        Assert.DoesNotContain("rel=\"next\"", await _studio.Get("/Components/" + _studio.Design.ComponentsOf("Feedback").Last().Id));

        // the page stays whole: ids unique, every fragment link leads somewhere
        var ids = IdAttribute().Matches(page).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
        Assert.Empty(ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key));
        var known = ids.ToHashSet();
        Assert.Empty(FragmentLink().Matches(page).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).Where(f => !known.Contains(f)).Distinct());

        // the other pages do not show it
        Assert.DoesNotContain("id=\"qa-sample\"", await _studio.Get("/Layout"));
        Assert.Contains("Created", await _studio.Get("/Studio/Activity"));
    }

    [Fact]
    public async Task What_breaks_the_contract_is_refused_with_the_reason_and_nothing_is_saved()
    {
        await _studio.SignIn();
        var otherId = ComponentCatalog.All.SelectMany(c => c.Examples.Where(e => e.Live).SelectMany(e => MarkupChecks_IdsIn(e.Html))).First();
        var form = Form(
            id: "Bad Id",
            html: $"""<div class="nope" style="color:red"><button>x</button><input class="mat-input" id="{otherId}" name="a" /><img src="https://example.com/a.png" alt="" onerror="x()" /><table class="mat-table"><tr><td>A</td></tr></table></div>""",
            group: "Toys");
        With(form, "Input.Rules", "NOPE-99");
        With(form, "Input.Summary", "short");

        var response = await Save(form);
        var html = await Body(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Please check the form.", html);
        Assert.Contains("Use 2 to 40 lower-case letters", html);
        Assert.Contains("Choose one of the groups.", html);
        Assert.Contains("Say in 10 to 300 characters", html);
        Assert.Contains("The rulebook has no rule NOPE-99.", html);
        Assert.Contains("no class &#x27;nope&#x27;", html);
        Assert.Contains("an inline style", html);
        Assert.Contains("an inline event handler", html);
        Assert.Contains("without a type", html);
        Assert.Contains("an address of another site", html);
        Assert.Contains("a &lt;td&gt; without data-label", html);
        Assert.Contains($"the id &#x27;{otherId}&#x27; is already used by", html);
        Assert.Contains("href=\"#Input_Examples_0_Html\"", html);
        Assert.Contains("value=\"Bad Id\"", html);
        Assert.Empty(_studio.Store.List("components"));

        // what must never be drawn is not drawn in the preview, but is still in the box to be corrected
        Assert.Contains("Not shown: the markup breaks a check that keeps the page safe.", html);
        Assert.DoesNotContain("<img src=\"https://example.com", html);
        Assert.DoesNotContain("onerror=\"x()\" />", html);
    }

    private static IEnumerable<string> MarkupChecks_IdsIn(string html) => MatDesign.Core.Studio.MarkupChecks.IdsIn(html);

    [Fact]
    public async Task An_example_needs_a_title_and_markup_and_Check_shows_the_example_live_and_saves_nothing()
    {
        await _studio.SignIn();
        var form = Form();
        With(form, "Input.Examples[0].Title", "");
        Assert.Contains("give it a title", await Body(await Save(form)));

        var check = await Save(Form(), "?check=true");
        var html = await Body(check);
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        Assert.Contains("All checks pass.", html);
        Assert.Contains("doc-example__live", html);
        Assert.Contains(Pill, html);
        Assert.Empty(_studio.Store.List("components"));
    }

    [Fact]
    public async Task Examples_are_added_and_removed_by_buttons_that_save_nothing()
    {
        await _studio.SignIn();

        var added = await Body(await _studio.Post("/Studio/Components/Edit", "/Studio/Components/Edit?handler=AddExample", Form()));
        Assert.Contains("name=\"Input.Examples[1].Html\"", added);
        Assert.Contains("name=\"Input.Examples[0].Html\"", added);
        Assert.Contains("Remove", added);
        Assert.Contains(Pill, added.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\""));

        var form = Form();
        form.AddRange([new("Input.Examples[1].Js", ""), new("Input.Examples[1].Demo", ""), new("Input.Examples[1].Frame", ""), new("Input.Examples[1].Title", "Second"), new("Input.Examples[1].Html", "<b>second</b>"), new("Input.Examples[1].Note", ""), new("Input.Examples[1].Live", "true"), new("Input.Examples[1].Live", "false")]);
        var removed = await Body(await _studio.Post("/Studio/Components/Edit", "/Studio/Components/Edit?handler=RemoveExample&index=0", form));
        Assert.Contains("name=\"Input.Examples[0].Html\"", removed);
        Assert.DoesNotContain("name=\"Input.Examples[1].Html\"", removed);
        Assert.Contains("Second", removed);
        Assert.DoesNotContain("A note.", removed);
        Assert.Empty(_studio.Store.List("components"));
    }

    [Fact]
    public async Task An_example_that_is_not_live_is_kept_as_such_and_is_not_drawn_on_the_page()
    {
        await _studio.SignIn();
        Assert.Equal(HttpStatusCode.Redirect, (await Save(Form(live: false))).StatusCode);

        var stored = _studio.Design.Component("qa-sample")!;
        Assert.False(stored.Examples[0].Live);
        var page = await _studio.Get("/Components/qa-sample");
        Assert.DoesNotContain($"doc-example__live\">{Pill}", page);
        Assert.Contains("doc-example__body doc-example__body--alone", page);
        Assert.Contains("<span class=\"doc-code__lang\">HTML</span>", page);
    }

    [Fact]
    public async Task A_built_in_component_that_is_saved_becomes_a_changed_one_and_a_reset_brings_the_built_in_one_back()
    {
        await _studio.SignIn();
        var original = ComponentCatalog.Get("pills")!;
        var form = new List<KeyValuePair<string, string>>
        {
            new("Input.Original", "pills"), new("Input.Id", "pills"), new("Input.Name", original.Name), new("Input.Group", original.Group), new("Input.Icon", original.Icon),
            new("Input.Summary", "A new summary for the pills, written in the Studio."), new("Input.Rules", string.Join(", ", original.Rules)), new("Input.Behaviour", string.Join('\n', original.Behaviour)),
        };
        for (var i = 0; i < original.Examples.Count; i++)
        {
            var e = original.Examples[i];
            form.AddRange([new($"Input.Examples[{i}].Js", e.Js ?? ""), new($"Input.Examples[{i}].Demo", e.Demo ?? ""), new($"Input.Examples[{i}].Frame", e.Frame ?? ""), new($"Input.Examples[{i}].Title", e.Title), new($"Input.Examples[{i}].Html", e.Html), new($"Input.Examples[{i}].Note", e.Note ?? "")]);
            if (e.Live)
            {
                form.Add(new($"Input.Examples[{i}].Live", "true"));
            }

            form.Add(new($"Input.Examples[{i}].Live", "false"));
        }

        Assert.Equal(HttpStatusCode.Redirect, (await Save(form)).StatusCode);

        Assert.Equal(ComponentCatalog.All.Count, _studio.Design.Components.Count);
        Assert.Equal(ItemSource.Changed, _studio.Design.ComponentSourceOf("pills"));
        Assert.Contains("A new summary for the pills", await _studio.Get("/Components/All"));
        Assert.Contains("A new summary for the pills", await _studio.Get("/Components/pills"));
        Assert.Contains("A new summary for the pills", await _studio.Get("/Components"));
        Assert.Equal("Changed summary", _studio.Store.Current("components", "pills")!.Note);
        Assert.Equal(original.Examples.Count, _studio.Design.Component("pills")!.Examples.Count);
        var edit = await _studio.Get("/Studio/Components/Edit?id=pills");
        Assert.Contains("Reset to built-in", edit);

        var reset = await _studio.Post("/Studio/Components/Edit?id=pills", "/Studio/Components/Edit?handler=Delete", form);
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Equal(ItemSource.BuiltIn, _studio.Design.ComponentSourceOf("pills"));
        Assert.Equal(original.Summary, _studio.Design.Component("pills")!.Summary);
        Assert.Equal(["Reset to the built-in component", "Changed summary"], _studio.Store.History("components", "pills").Select(r => r.Note));
    }

    [Fact]
    public async Task A_component_that_was_added_can_be_deleted_and_leaves_its_page()
    {
        await _studio.SignIn();
        await Save(Form());
        var edit = await _studio.Get("/Studio/Components/Edit?id=qa-sample");
        Assert.Contains("data-confirm=\"Delete QA sample?", edit);
        Assert.Contains("readonly", edit);
        Assert.Contains("href=\"/Components/qa-sample\"", edit);

        var deleted = await _studio.Post("/Studio/Components/Edit?id=qa-sample", "/Studio/Components/Edit?handler=Delete", Form(original: "qa-sample"));

        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.DoesNotContain("id=\"qa-sample\"", await _studio.Get("/Components/All"));
        Assert.DoesNotContain("qa-sample", await _studio.Get("/Components"));
        Assert.Equal(HttpStatusCode.NotFound, (await _studio.Client.GetAsync("/Components/qa-sample")).StatusCode);
        Assert.Equal(ComponentCatalog.All.Count, _studio.Design.Components.Count);
        Assert.Equal(["Deleted", "Created"], _studio.Store.History("components", "qa-sample").Select(r => r.Note));
    }

    [Fact]
    public async Task A_built_in_component_cannot_be_deleted_and_an_unknown_one_is_not_found()
    {
        await _studio.SignIn();

        Assert.Equal(HttpStatusCode.BadRequest, (await _studio.Post("/Studio/Components/Edit?id=buttons", "/Studio/Components/Edit?handler=Delete", Form(id: "buttons", original: "buttons"))).StatusCode);
        await _studio.Get("/Studio/Components/Edit?id=nothere", HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, (await _studio.Post("/Studio/Components/Edit", "/Studio/Components/Edit", Form(id: "gone", original: "gone"))).StatusCode);
    }

    [Fact]
    public async Task The_id_of_a_component_that_exists_cannot_be_changed_by_the_form()
    {
        await _studio.SignIn();
        await Save(Form());

        var form = Form(id: "other", original: "qa-sample");
        With(form, "Input.Name", "QA sample, renamed");
        Assert.Equal(HttpStatusCode.Redirect, (await Save(form)).StatusCode);

        Assert.Equal("QA sample, renamed", _studio.Design.Component("qa-sample")!.Name);
        Assert.Null(_studio.Design.Component("other"));
    }

    [Fact]
    public async Task A_rule_that_a_component_of_the_studio_names_cannot_be_deleted_until_the_component_lets_go_of_it()
    {
        await _studio.SignIn();
        var rule = new Rule("FORM-90", "FORM", "A rule for the tests", "Every test rule is deleted again when the test ends.", RuleLevel.Should, RuleBook.Studio);
        _studio.Store.Save("rules", rule.Id, rule, "test");
        _studio.Design.Reload();
        await Save(With(Form(), "Input.Rules", "FORM-90"));
        var ruleForm = new Dictionary<string, string>
        {
            ["Input.Original"] = "FORM-90", ["Input.Id"] = "FORM-90", ["Input.Area"] = "FORM", ["Input.Title"] = rule.Title, ["Input.Statement"] = rule.Statement,
            ["Input.Level"] = "Should", ["Input.Source"] = "Studio", ["Input.Why"] = "", ["Input.Do"] = "", ["Input.Dont"] = "",
        };

        var refused = await _studio.Post("/Studio/Rules/Edit?id=FORM-90", "/Studio/Rules/Edit?handler=Delete", ruleForm);
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("is named by QA sample", await Body(refused));
        Assert.NotNull(_studio.Design.Rule("FORM-90"));

        await _studio.Post("/Studio/Components/Edit?id=qa-sample", "/Studio/Components/Edit?handler=Delete", Form(original: "qa-sample"));
        var done = await _studio.Post("/Studio/Rules/Edit?id=FORM-90", "/Studio/Rules/Edit?handler=Delete", ruleForm);
        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        Assert.Null(_studio.Design.Rule("FORM-90"));
    }

    [Fact]
    public async Task A_component_edited_by_hand_into_something_unsafe_is_left_out_and_the_pages_go_on()
    {
        await _studio.SignIn();
        ComponentDoc Doc(string id, string group, string html) => new(id, "Hand " + id, group, "toggle", "Edited by hand, on the volume.", ["BUTTON-01"], [new ComponentExample("Example", html)], []);
        _studio.Store.Save("components", "evil-script", Doc("evil-script", "Content", "<script>alert(1)</script>"), "hand");
        _studio.Store.Save("components", "evil-style", Doc("evil-style", "Content", """<p style="x:y">x</p>"""), "hand");
        _studio.Store.Save("components", "evil-group", Doc("evil-group", "Toys", "<b>x</b>"), "hand");
        _studio.Store.Save("components", "wrong-id", Doc("another-id", "Content", "<b>x</b>"), "hand");
        _studio.Store.Save("components", "fine-one", Doc("fine-one", "Content", """<span class="mat-pill">x</span>"""), "hand");
        _studio.Design.Reload();

        Assert.Equal(ComponentCatalog.All.Count + 1, _studio.Design.Components.Count);
        Assert.NotNull(_studio.Design.Component("fine-one"));
        var page = await _studio.Get("/Components/All");
        Assert.DoesNotContain("alert(1)", page);
        Assert.DoesNotContain("evil-", page);
        await _studio.Get("/Studio/Components");
    }
}
